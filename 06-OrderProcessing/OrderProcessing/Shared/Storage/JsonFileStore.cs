
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderProcessing.Shared.Exceptions;

namespace OrderProcessing.Shared.Storage;

public sealed class JsonFileStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _dataDirectory;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public JsonFileStore(string? dataDirectory = null)
    {
        _dataDirectory = dataDirectory ??
            Path.Combine(AppContext.BaseDirectory, "Data");
    }

    private string GetPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            Path.GetFileName(fileName) != fileName)
        {
            throw new ArgumentException("Invalid data file name.", nameof(fileName));
        }

        return Path.Combine(_dataDirectory, fileName);
    }

    private SemaphoreSlim GetFileLock(string path)
    {
        return _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
    }

    public async Task<List<T>> ReadAsync<T>(
        string fileName,
        CancellationToken ct)
    {
        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        await fileLock.WaitAsync(ct);

        try
        {
            return await ReadCoreAsync<T>(fileName, path, ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public List<T> Read<T>(string fileName)
    {
        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        fileLock.Wait();

        try
        {
            return ReadCore<T>(fileName, path);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task WriteAsync<T>(
        string fileName,
        IEnumerable<T> records,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(records);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        await fileLock.WaitAsync(ct);

        try
        {
            await WriteCoreAsync(path, records.ToList(), ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Write<T>(string fileName, IEnumerable<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        fileLock.Wait();

        try
        {
            WriteCore(path, records.ToList());
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task UpdateAsync<T>(
        string fileName,
        Func<List<T>, List<T>> update,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        await fileLock.WaitAsync(ct);

        try
        {
            List<T> records =
                await ReadCoreAsync<T>(fileName, path, ct);

            List<T> updatedRecords = update(records);

            ArgumentNullException.ThrowIfNull(updatedRecords);

            await WriteCoreAsync(path, updatedRecords, ct);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public void Update<T>(
        string fileName,
        Func<List<T>, List<T>> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        string path = GetPath(fileName);
        SemaphoreSlim fileLock = GetFileLock(path);

        fileLock.Wait();

        try
        {
            List<T> records = ReadCore<T>(fileName, path);
            List<T> updatedRecords = update(records);

            ArgumentNullException.ThrowIfNull(updatedRecords);

            WriteCore(path, updatedRecords);
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static async Task<List<T>> ReadCoreAsync<T>(
        string fileName,
        string path,
        CancellationToken ct)
    {
        if (!File.Exists(path))
            return [];

        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(fileName, ex);
        }
    }

    private static List<T> ReadCore<T>(
        string fileName,
        string path)
    {
        if (!File.Exists(path))
            return [];

        string json = File.ReadAllText(path, Encoding.UTF8);

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new DataFileCorruptedException(fileName, ex);
        }
    }

    private async Task WriteCoreAsync<T>(
        string path,
        List<T> records,
        CancellationToken ct)
    {
        Directory.CreateDirectory(_dataDirectory);

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        string json = JsonSerializer.Serialize(records, JsonOptions);

        try
        {
            await File.WriteAllTextAsync(
                tempPath,
                json,
                Encoding.UTF8,
                ct);

            ct.ThrowIfCancellationRequested();

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private void WriteCore<T>(string path, List<T> records)
    {
        Directory.CreateDirectory(_dataDirectory);

        string tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        string json = JsonSerializer.Serialize(records, JsonOptions);

        try
        {
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
