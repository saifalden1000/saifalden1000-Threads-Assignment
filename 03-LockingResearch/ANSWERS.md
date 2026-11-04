

\# Assignment 03 — Locking Research



\*\*Student name:\*\* Seif Mohamed  

\*\*Group / ID:\*\* Group #3



\## Q1. What is System.Threading.Monitor used for?



System.Threading.Monitor is used to synchronize access to shared resources between multiple threads.



It allows only one thread at a time to enter a critical section protected by the same lock object.



It provides methods such as Enter, Exit, TryEnter, Wait and Pulse to control access and coordinate threads.



\## Q2. What does the C# lock statement compile to?



When using a normal object as the lock target, the C# lock statement compiles into Monitor.Enter and Monitor.Exit calls.



The compiler also generates a try-finally block to guarantee that the lock is released even if an exception occurs.



For example:



```csharp

lock (\_lockObject)

{

&#x20;   counter++;

}

```



Is conceptually equivalent to:



```csharp

bool lockTaken = false;



try

{

&#x20;   Monitor.Enter(\_lockObject, ref lockTaken);

&#x20;   counter++;

}

finally

{

&#x20;   if (lockTaken)

&#x20;       Monitor.Exit(\_lockObject);

}

```



\## Q3. List at least two differences between using lock and calling Monitor APIs directly.



1\. The lock statement is easier to read and use because the compiler handles acquiring and releasing the lock automatically.



2\. Monitor provides additional methods such as TryEnter, Wait and Pulse, which are not directly available through a normal lock statement.



3\. When using Monitor.Enter and Monitor.Exit directly, the developer must make sure that Exit is called inside a finally block to avoid leaving the lock acquired after an exception.



\## Q4. When might a developer use Monitor.TryEnter, Monitor.Pulse or Monitor.Wait instead of a plain lock?



\- Monitor.TryEnter is useful when a thread should try to acquire a lock without waiting forever. It can also use a timeout.



\- Monitor.Wait is useful when a thread needs to wait until a specific condition becomes true. It temporarily releases the lock while waiting.



\- Monitor.Pulse is useful when one thread changes a shared condition and wants to notify another thread waiting for that condition.



These methods are useful in producer-consumer scenarios and other situations where threads need to coordinate their work.



\## Q5. Write the same critical section twice: once with lock, once with Monitor.Enter / Monitor.Exit.



\### Using lock



```csharp

private static readonly object \_lockObject = new();

private static int \_counter;



static void Increment()

{

&#x20;   lock (\_lockObject)

&#x20;   {

&#x20;       \_counter++;

&#x20;   }

}

```



\### Using Monitor.Enter and Monitor.Exit



```csharp

private static readonly object \_lockObject = new();

private static int \_counter;



static void Increment()

{

&#x20;   bool lockTaken = false;



&#x20;   try

&#x20;   {

&#x20;       Monitor.Enter(\_lockObject, ref lockTaken);

&#x20;       \_counter++;

&#x20;   }

&#x20;   finally

&#x20;   {

&#x20;       if (lockTaken)

&#x20;           Monitor.Exit(\_lockObject);

&#x20;   }

}

```



Both implementations protect the counter from concurrent updates. Only one thread can increment the counter at a time.



\## References



1\. Microsoft Learn — Monitor Class:

&#x20;  https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor



2\. Microsoft Learn — C# lock statement:

&#x20;  https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock



