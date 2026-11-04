
# Assignment 01 — Invoice Export

**Student name:** Seif Mohamed
**Group / ID:** Group#3

## Original Output

The original application processed five invoices sequentially.

- Execution time: 6.0 seconds
- Total invoices: 5
- All invoices were generated successfully.

## Problems Found

1. Thread.Sleep blocks the current thread during database and file operations.
2. File.ReadAllText and File.WriteAllText are synchronous operations.
3. Orders were processed sequentially, increasing the total execution time.
4. The application did not take advantage of asynchronous I/O.

## Changes Made

1. Replaced Thread.Sleep with Task.Delay in the simulated database operations.
2. Changed database methods to return Task and use async/await.
3. Replaced File.ReadAllText with File.ReadAllTextAsync.
4. Replaced File.WriteAllText with File.WriteAllTextAsync.
5. Changed the invoice export workflow to async/await.
6. Started all order-processing tasks before awaiting them individually.
7. Preserved the original business flow and invoice content.

## Performance Comparison

| Version | Execution Time |
|---|---:|
| Original synchronous version | 6.0 seconds |
| Updated asynchronous version | 1.9 seconds |

## Result

The updated application successfully generates the same five invoices.

The execution time decreased because the asynchronous operations can overlap instead of waiting for each order to finish before starting the next one.
