# Order Processing System — Async, Threads & Thread Safety

**Student:** Seif Mohamed  
**Group:** 3  
**Framework:** .NET 10  
**Project:** Order Processing System — File Storage Edition

---

## 1. Project Overview

This project demonstrates asynchronous programming, concurrency, parallel processing, thread safety, cancellation, and error handling in C# using a simulated e-commerce order processing system.

The application processes customer orders through several steps:

1. Load and validate customer information.
2. Reserve product inventory safely.
3. Calculate order pricing, discounts, and shipping.
4. Process payments through a simulated payment gateway.
5. Save order information.
6. Send confirmation emails with retry support.

The application uses JSON files for persistence. Database queries, external API calls, payment operations, and email delivery are simulated with asynchronous delays.

The project targets **.NET 10** and uses only built-in .NET libraries.

## 2. How to Run the Project

Open a terminal in the `OrderProcessing` project directory.

Run the application:

```powershell
dotnet run
```

Without arguments, the application displays a numbered menu.

Run individual phases:

```powershell
dotnet run -- phase1
dotnet run -- phase2
dotnet run -- phase3
dotnet run -- phase4
```

Run all phases:

```powershell
dotnet run -- all
```

Run a phase with automatic cancellation:

```powershell
dotnet run -- phase4 --cancel-after 12000
```

The cancellation value is specified in milliseconds.

Pressing `Ctrl+C` also requests cancellation instead of immediately terminating the application.

### Runtime Data Location

The application creates its data directory using:

```csharp
Path.Combine(AppContext.BaseDirectory, "Data")
```

When running through `dotnet run`, the typical location is:

```text
OrderProcessing/bin/Debug/net10.0/Data/
```

The `DataSeeder` resets the data files at the beginning of each phase.

## 3. Project Structure

```text
OrderProcessing/
│
├── Shared/
│   ├── Exceptions/
│   ├── Logging/
│   ├── Models/
│   └── Storage/
│
├── Phase1/
│   ├── CustomerRepository.cs
│   ├── ProductRepository.cs
│   ├── CouponRepository.cs
│   ├── OrderRepository.cs
│   ├── IncomingOrderRepository.cs
│   ├── OrderValidator.cs
│   └── Phase1Runner.cs
│
├── Phase2/
│   ├── PricingService.cs
│   ├── OrderPricer.cs
│   ├── RaceConditionDemo.cs
│   ├── InvoiceGenerator.cs
│   ├── OrderProcessorV2.cs
│   ├── ReportGenerator.cs
│   └── Phase2Runner.cs
│
├── Phase3/
│   ├── IPaymentGateway.cs
│   ├── IEmailService.cs
│   ├── PaymentGateway.cs
│   ├── EmailService.cs
│   ├── CustomerService.cs
│   ├── InventoryService.cs
│   ├── OrderProcessor.cs
│   ├── SyncOrderProcessor.cs
│   ├── FireAndForgetRunner.cs
│   └── Phase3Runner.cs
│
├── Phase4/
│   ├── LiveCheckoutProgressRunner.cs
│   ├── CustomerDashboardService.cs
│   ├── CustomerDashboardDemo.cs
│   ├── IShippingCarrier.cs
│   ├── ShippingQuote.cs
│   ├── FastShip.cs
│   ├── CairoExpress.cs
│   ├── NileCourier.cs
│   ├── ShippingQuoteService.cs
│   ├── ShippingQuoteDemo.cs
│   ├── FraudScoringService.cs
│   ├── ParallelEmailDemo.cs
│   └── Phase4Runner.cs
│
└── Program.cs
```

### Main Responsibilities

**Shared**

- `Models`: Contains the application's entities, enums, and data models.
- `Storage/JsonFileStore`: Handles synchronous and asynchronous JSON file operations, updates, and thread-safe access.
- `Storage/DataSeeder`: Creates and resets the initial data files.
- `Storage/DataFiles`: Stores the fixed data file names.
- `Logging/Log`: Provides thread-safe logging with timestamps, thread IDs, and different log levels.
- `Exceptions`: Contains custom exceptions used throughout the application.

**Phase 1 — Async Data Access**

- Repositories handle loading and saving customer, product, coupon, incoming order, and processed order data.
- `OrderValidator` validates customer information, product availability, and coupon validity.
- `Phase1Runner` compares synchronous and asynchronous validation.

**Phase 2 — Concurrent Processing**

- `PricingService` calculates subtotal, discounts, and shipping costs.
- `OrderPricer` compares sequential and concurrent pricing operations.
- `RaceConditionDemo` demonstrates unsafe and safe inventory reservations.
- `InvoiceGenerator` uses `Task.Run` for CPU-bound invoice generation.
- `OrderProcessorV2` processes validated orders concurrently.
- `ReportGenerator` generates the final sales report.

**Phase 3 — Full Checkout Workflow**

- `IPaymentGateway` and `IEmailService` define external service contracts.
- `PaymentGateway` simulates payment authorization, capture, timeout, decline, and refunds.
- `EmailService` sends confirmation emails and retries failed attempts.
- `CustomerService` retrieves customer information.
- `InventoryService` reserves and releases product stock.
- `OrderProcessor` coordinates the complete checkout workflow and compensation logic.
- `SyncOrderProcessor` provides the synchronous comparison.
- `FireAndForgetRunner` demonstrates unawaited tasks and their corrected implementation.

**Phase 4 — Advanced Concurrency**

- `LiveCheckoutProgressRunner` uses `Task.WhenEach` to report orders as they finish.
- `CustomerDashboardService` loads dashboard information concurrently.
- Shipping carrier classes simulate competing delivery providers.
- `ShippingQuoteService` selects the first successful carrier response and cancels the remaining requests.
- `FraudScoringService` compares sequential and parallel CPU-bound processing.
- `ParallelEmailDemo` sends emails concurrently with a maximum concurrency of four.

## 4. Storage Design

The application uses `System.Text.Json` for JSON serialization and deserialization.

Each collection is stored as a JSON array in its own file.

| File | Purpose |
|---|---|
| `customers.json` | Customer information |
| `products.json` | Product information and stock quantities |
| `coupons.json` | Available coupons and expiry dates |
| `incoming_orders.json` | The 20 seeded incoming orders |
| `orders.json` | Processed order records |
| `payments.json` | Payment transactions |
| `emails.json` | Successfully sent email records |
| `email_retry.json` | Emails that exhausted all retry attempts |
| `invoices/` | Generated invoice files |
| `reports/` | Generated order reports |

### JSON Configuration

One shared `JsonSerializerOptions` instance is used throughout the application.

The configuration includes:

- `WriteIndented = true`
- `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- `JsonStringEnumConverter` for storing enum names as strings.
- UTF-8 encoding.

An empty collection is represented as:

```json
[]
```

### Thread Safety

Concurrent access to JSON files can cause lost updates or corrupted data if multiple operations read and write the same file at the same time.

To prevent this, `JsonFileStore` uses:

- `ConcurrentDictionary` to maintain a synchronization object for each file.
- `SemaphoreSlim` to serialize concurrent operations on the same file.
- A protected read-modify-write operation for collection updates.
- Temporary files followed by an atomic replacement to avoid exposing partially written JSON files.

The safe inventory reservation operation performs the stock check and update inside one protected operation.

This prevents two orders from reserving the same remaining item.

## 5. Performance Measurements

The measurements below come from the actual console runs where available. Approximate values are explicitly identified as estimates, not stopwatch measurements. Thread IDs observed in logs are not the same as `ThreadPool.ThreadCount`.

### Phase 1 — Synchronous vs Asynchronous Validation

| Operation | Time |
|---|---:|
| Synchronous validation | 36.5 seconds |
| Asynchronous validation | 35.0 seconds |
| Asynchronous validation including saves | 53.7 seconds |

Both validation approaches produced the same result:

- Total orders: 20
- Validated: 15
- Rejected: 5

The asynchronous version did not produce a significant speed improvement because orders were still validated one by one. Its main advantage is that it does not block a thread during asynchronous I/O waits.

### Phase 2 — Pricing Comparison

The original pricing comparison was not preserved as a stopwatch result in the saved output. The following values are approximate estimates based on the simulated operation delays.

| Operation | Approximate time |
|---|---:|
| Sequential pricing | 5.0 seconds |
| Concurrent pricing | 3.0 seconds |

The subtotal is calculated first. Discount and shipping calculations are then started together.

Concurrent execution reduces the waiting time because both operations can progress independently.

### Phase 2 — Concurrent Order Processing

The final report showed:

| Result | Value |
|---|---:|
| Total orders | 20 |
| Completed | 13 |
| Rejected | 6 |
| Failed | 1 |
| Total revenue | 5966.45 |
| Average order value | 458.96 |
| Top-selling product | USB-C Hub |
| Top-selling quantity | 5 |

The expected shipping provider failure was handled without stopping the remaining orders.

The overall processing-time and thread-pool figures were not captured in the saved Phase 2 output. The implementation uses concurrent validation and processing, with `Task.Run` for CPU-bound invoice generation.

### Phase 3 — Synchronous vs Asynchronous Processing

Both versions processed 10 orders with the same result: 8 completed and 2 rejected.

| Metric | Synchronous | Asynchronous |
|---|---:|---:|
| Processing time | 13.99 seconds | 9.81 seconds |
| Dedicated threads / distinct logged threads | 10 dedicated threads | 7 distinct threads |
| `ThreadPool.ThreadCount` | 4 → 4 | 4 → 8 |
| Unexpected errors | 0 | 0 |

The synchronous implementation creates a dedicated thread for each order.

The asynchronous implementation allows threads to return to the ThreadPool while operations are waiting for I/O.

### Phase 3 — Full Checkout

The full checkout run processed 20 orders.

| Status | Count |
|---|---:|
| Completed | 11 |
| Rejected | 6 |
| Payment Failed | 2 |
| Failed | 1 |

Additional results:

- Payment records: 13
- Captured payments: 11
- Declined payments: 1
- Refunded payments: 1
- Emails successfully sent: 10
- Emails queued for retry: 1
- Order #3 email succeeded on attempt 3.
- Order #17 exhausted four attempts and was queued for retry.
- Order #13 was marked Failed after the database save failure, and its payment was refunded.
- Mechanical Keyboard remaining stock: 0.
- Total processing time: approximately 15.5 seconds.

### Phase 4 — Advanced Concurrency

| Task | Time | Distinct logged threads | Result |
|---|---:|---:|---|
| 10.1 Live checkout | 15.82 s | 11 | 20 orders processed |
| 10.2 Customer dashboard | 0.66 s | 5 | Four concurrent loads |
| 10.3 Shipping quotes | 4.57 s | 4 | First successful carrier selected |
| 10.4A Fraud scoring — sequential | 5.03 s | 1 | 1,005 flagged |
| 10.4A Fraud scoring — parallel | 0.47 s | 14 | 1,005 flagged |
| 10.4B Email processing | 8.34 s | 4 | 30 emails sent |

For the Phase 4 tasks, the saved console output records thread IDs, but does not contain before-and-after `ThreadPool.ThreadCount` measurements. Those values should not be inferred from the distinct thread counts.

#### Task 10.1 — Live Checkout

All 20 orders were started concurrently.

The application printed each order as soon as it finished using `Task.WhenEach`.

The final result matched the Phase 3 full checkout result:

- Completed: 11
- Rejected: 6
- Payment Failed: 2
- Failed: 1

#### Task 10.2 — Customer Dashboard

Customer #3 dashboard:

- Name: Omar Hassan
- Orders: 2
- Order #3: Completed
- Order #13: Failed
- Payments: 2
- Order #3: Captured
- Order #13: Refunded
- Emails: 1
- Dashboard load time: 0.66 seconds

Customer #99 was used to test multiple failures. The dashboard reported both:

- Customer #99 not found.
- Payments store timeout.

#### Task 10.3 — Shipping Quote Race

Three carriers were started at the same time:

- FastShip
- CairoExpress
- NileCourier

The first successful quote was selected. The remaining carrier tasks were cancelled.

The run also tested the case where all three carriers fail. The application reported that all carriers failed.

#### Task 10.4A — Fraud Scoring

The application scored 10,000 orders using the specified CPU-intensive algorithm.

| Metric | Result |
|---|---:|
| Orders | 10,000 |
| Sequential flagged orders | 1,005 |
| Parallel flagged orders | 1,005 |
| Sequential time | 5.03 seconds |
| Parallel time | 0.47 seconds |
| Speed-up | 10.64× |
| Parallel threads used | 14 |
| Processor cores available | 14 |

Both runs returned identical results. The first five flagged order IDs were:

`4, 8, 12, 29, 33`

#### Task 10.4B — Parallel Email Processing

Thirty test emails were processed using `Parallel.ForEachAsync`.

Results:

- Successfully completed: 30/30
- Emails saved: 30/30
- Unique recipients: 30
- Maximum concurrent emails: 4
- Approximate elapsed time: 8.34 seconds

The concurrency limit was supplied by the library configuration rather than a manually implemented semaphore.

## 6. Problems Found and Solutions

### Race Condition in Inventory Reservation

**Problem:** Two orders could read the same remaining stock and both successfully reserve it.

**Solution:** The stock read, availability check, and update were moved into a protected operation using `SemaphoreSlim`.

### Concurrent JSON File Updates

**Problem:** Multiple tasks updating the same JSON collection could overwrite one another.

**Solution:** Per-file synchronization was introduced in `JsonFileStore`, and updates use a protected read-modify-write sequence.

### Blocking Threads

**Problem:** `Thread.Sleep` blocked threads while waiting for simulated I/O.

**Solution:** Asynchronous operations use `Task.Delay` and asynchronous file methods wherever appropriate.

### CPU-Bound Invoice Generation

**Problem:** CPU-intensive work should not be performed directly on the caller's thread.

**Solution:** `Task.Run` was used for the invoice-generation calculation.

### Payment and Database Failure

**Problem:** A payment could be captured successfully while saving the order failed.

**Solution:** The processor catches the database write failure, refunds the payment, releases inventory, and records the order as Failed.

### Email Failures

**Problem:** A failed confirmation email should not invalidate an otherwise completed and paid order.

**Solution:** The email service retries the operation and places emails that exhaust all attempts in `email_retry.json`.

### Cancellation During Checkout

**Problem:** Cancellation could occur after inventory reservation or payment capture.

**Solution:** The order processor performs compensation using a non-cancelled cleanup token: refund captured payments, release reserved inventory, and save the order as Pending with the reason Cancelled.

The live checkout runner also waits for all started tasks to complete their cancellation cleanup before returning.

## 7. Phase 1 Questions

**Q1. Why are synchronous and asynchronous times almost the same when orders are awaited one after another?**

Because the operations are still executed sequentially. The asynchronous version yields the thread during I/O waits, but it does not make the next order start before the current order finishes.

**Q2. Is the thread ID before an await always the same as after it?**

No. After an awaited operation completes, the continuation may run on a different available ThreadPool thread. The thread that started the operation is not guaranteed to resume it.

**Q3. What happens to a thread during Task.Delay and Thread.Sleep?**

`Task.Delay` does not keep a worker thread blocked while waiting. The async method yields, and its continuation runs later.

`Thread.Sleep` blocks the current thread until the delay finishes.

**Q4. Why should a repository return Task<T> instead of T?**

A `Task<T>` allows callers to await the result without blocking their current thread during asynchronous operations. It also allows exceptions and cancellation to flow through the asynchronous call chain.

**Q5. What is the difference between File.ReadAllText and File.ReadAllTextAsync?**

`File.ReadAllText` performs a synchronous read and blocks its caller until the operation finishes.

`File.ReadAllTextAsync` returns a task and allows the caller to await the operation. The caller does not need to keep a worker thread blocked while asynchronous file I/O is pending.

## 8. Phase 2 Questions

**Q1. What is the difference between starting a task and awaiting it?**

Starting a task initiates an operation. Awaiting it allows the current asynchronous method to pause until the task completes, then obtains its result or propagates its exception.

**Q2. Does every Task create a new thread? Does Task.Delay use a thread while waiting?**

No. A Task represents an asynchronous operation and does not necessarily own a thread. `Task.Delay` uses a timer and does not need to block a worker thread during the waiting period.

**Q3. What happens if .Result or .Wait() is called on an unfinished task?**

The current thread blocks until the task completes. This can reduce scalability and may cause deadlocks when a continuation needs the blocked thread or synchronization context. Excessive blocking can also cause ThreadPool starvation.

**Q4. What happens to an exception thrown inside a task that is never awaited?**

The exception remains stored in the faulted task. If nobody observes it, it may later be reported through the unobserved task exception mechanism when the task is collected. It should never be relied upon as a proper error-handling strategy.

**Q5. When should Task.Run be used, and when should it be avoided?**

`Task.Run` is useful for moving CPU-bound work away from the calling thread.

It should generally be avoided for naturally asynchronous I/O operations such as reading files or waiting for database and API responses.

**Q6. What is the difference between Task and Task<T>?**

`Task` represents an asynchronous operation without a result value.

`Task<T>` represents an asynchronous operation that produces a result of type T.

**Q7. Why can a lock block not contain an await?**

The `lock` statement is based on synchronous thread ownership. An asynchronous operation may resume on a different thread after an await, so holding a normal lock across an await is not supported. `SemaphoreSlim.WaitAsync` is suitable for asynchronous critical sections.

**Q8. Why did the unprotected stock reservation oversell, and how was it prevented?**

Both orders could read the same stock before either had updated it. Each order then made its decision based on stale information.

The protected reservation operation serializes the read-check-update sequence so that only one order can reserve the final available item.

## 9. Phase 3 Questions

**Q1. Does async make one order's workflow faster?**

Not necessarily. Asynchronous programming mainly prevents threads from being blocked during I/O waits. It can improve the total time of a concurrent batch because more operations can make progress at the same time.

In the measured comparison, the synchronous batch took 13.99 seconds while the asynchronous batch took 9.81 seconds.

**Q2. Why does async scale better for many orders?**

An asynchronous operation can yield its thread while waiting for I/O. That thread can then be used by other work instead of remaining blocked.

This is especially useful when an application handles many database, file, or API operations.

**Q3. What happens to a synchronous implementation with 1,000 orders?**

Creating a dedicated thread for every order would require a large number of threads. This increases memory consumption, context switching, scheduling overhead, and the risk of resource exhaustion.

An asynchronous implementation can handle many pending I/O operations with fewer worker threads.

**Q4. Why is async important for applications that use databases, files, and APIs?**

These operations spend a significant amount of time waiting. Asynchronous methods allow the application to remain responsive and use available threads more efficiently while the external operation is pending.

**Q5. What is the problem with fire-and-forget?**

When a task is started without awaiting it, the caller may finish before the operation does. The caller also cannot directly observe the task's exception or guarantee that the operation finishes before the application exits.

**Q6. When is await necessary, and when is fire-and-forget acceptable?**

Await should be used when the caller depends on the operation's result, completion, or error handling.

Fire-and-forget should generally be avoided in ordinary application workflows. It may be acceptable for managed background processing when the application has a proper queue, lifetime management, logging, retries, and exception handling.

**Q7. Why is async void dangerous, and why is it acceptable for event handlers?**

An `async void` method cannot be awaited by its caller, and the caller cannot catch its asynchronous exceptions in the normal way.

It is acceptable for event handlers because event delegate signatures often require a `void` return type. For normal application methods, `Task` or `Task<T>` should be used instead.

**Q8. How is a payment compensated if saving an order fails after payment capture?**

The processor refunds the captured payment, releases the reserved inventory, and saves the order with Failed status and an explanatory failure reason.

**Q9. What is the difference between payment timeout and cancellation?**

A payment timeout is a business failure. The order is marked PaymentFailed and inventory is released.

Run cancellation is different. It triggers the cancellation compensation flow, and a cancelled order is saved as Pending with the reason Cancelled. A cancelled order must never be marked PaymentFailed or Failed.

## 10. Phase 4 Questions

**Q1. Which tools were used for Tasks 10.1–10.4, and why is awaiting tasks one by one insufficient?**

- **10.1:** `Task.WhenEach` reports each order as it completes. Awaiting tasks in start order would delay reporting later tasks that already finished.
- **10.2:** `Task.WhenAll` waits for all four dashboard loads together. Awaiting each load separately would make the total time closer to the sum of the individual load times.
- **10.3:** `Task.WhenAny` identifies the first successful shipping quote so the remaining providers can be cancelled.
- **10.4A:** `Parallel.ForEach` distributes CPU-intensive work across available processor cores.
- **10.4B:** `Parallel.ForEachAsync` runs asynchronous email operations with a maximum concurrency of four.

Starting and awaiting tasks one by one would not provide the required completion ordering, simultaneous dashboard loads, first-result selection, or controlled parallel processing.

**Q2. In Task 10.2, how can every failure be retrieved when multiple loads fail? What does a plain await give?**

The application can inspect the tasks after `Task.WhenAll` faults and collect every exception from the faulted tasks using their `Exception` properties and `Flatten().InnerExceptions`.

A normal `await` propagates an exception from the combined operation, but a simple catch should not be assumed to expose every failure. Inspecting the individual faulted tasks allows the application to report them all.

**Q3. What happens to losing carriers if they are not cancelled?**

They continue running even after another carrier has already returned a suitable quote. This wastes time and resources and may perform unnecessary work. Cancelling the losing requests stops their operations when they observe the cancellation token.

**Q4. Why does Task 10.4A benefit from more CPU cores, while Task 10.4B does not depend on the number of cores?**

Fraud scoring is CPU-bound. More available cores allow independent calculations to run in parallel.

Email sending is I/O-bound. Most of the time is spent waiting for simulated SMTP operations, not consuming CPU. Its concurrency is controlled by the configured maximum of four operations rather than the number of processor cores.

**Q5. What is the difference between parallel work and concurrent asynchronous work?**

Parallel work means multiple operations execute at the same time on different CPU cores.

Concurrent asynchronous work means multiple operations are in progress during overlapping periods, even when they are not executing CPU instructions simultaneously.

Fraud scoring uses parallel processing. Live checkout uses concurrent asynchronous operations.

**Q6. Explain the cancelled run and what happened to the orders, stock, and payments.**

The cancellation test was executed with:

```powershell
dotnet run -- phase4 --cancel-after 12000
```

The final order summary was:

| Status | Count |
|---|---:|
| Completed | 9 |
| Rejected | 6 |
| Payment Failed | 2 |
| Failed | 1 |
| Pending / Cancelled | 2 |
| Total orders saved | 20 |

Orders #17 and #3 were cancelled during processing. Both were saved as Pending with the reason Cancelled.

Their captured payments were refunded successfully. The payment records in `payments.json` confirmed that both transactions had Refunded status.

The inventory release logs confirmed that stock reserved by both cancelled orders was released.

Order #13 also had a refunded payment because its database save failed after payment capture. That order remained Failed because the failure was a database failure, not cancellation.

The run completed cancellation cleanup before the application exited.

## 11. Screenshots and Sample Output

The screenshots folder contains console evidence for the required tasks.

Recommended structure:

```text
06-OrderProcessing/
│
├── sample-output/
│   ├── phase1/
│   ├── phase2/
│   ├── phase3/
│   └── phase4/
│
├── screenshots/
│   ├── phase2-race-condition.png
│   ├── phase2-faulted-task.png
│   ├── phase3-payment-timeout.png
│   ├── phase3-refund.png
│   ├── phase3-email-retry.png
│   ├── phase3-fire-and-forget-before.png
│   ├── phase3-fire-and-forget-after.png
│   ├── phase4-10.1-live-checkout.png
│   ├── phase4-10.2-customer-dashboard.png
│   ├── phase4-10.2-dashboard-errors.png
│   ├── phase4-10.3-shipping-race.png
│   ├── phase4-10.3-all-carriers-failed.png
│   ├── phase4-10.4a-fraud-scoring.png
│   ├── phase4-10.4b-parallel-emails.png
│   └── phase4-cancellation-12s.png
│
└── README.md
```

The `sample-output` directory should contain the final JSON snapshots for the phases, not only console logs.

The cancellation evidence includes:

- Final `orders.json` showing all 20 orders.
- `payments.json` showing the refunded transactions.
- `products.json` showing the remaining stock after completed orders and cancellation compensation.

## 12. Final Notes

The project demonstrates the practical differences between synchronous execution, asynchronous I/O, concurrent tasks, parallel CPU-bound work, and thread-safe state updates.

The most important lessons from the implementation were:

- Asynchronous programming improves scalability by avoiding unnecessary thread blocking.
- Concurrency does not automatically make shared data safe.
- Shared file updates need synchronization.
- CPU-bound work and I/O-bound work require different approaches.
- Every started asynchronous operation should have a defined completion and exception-handling strategy.
- Cancellation must be propagated through the application and accompanied by compensation when resources have already been reserved or payments captured.

The application targets .NET 10 and is organized into four phases to demonstrate these concepts progressively.