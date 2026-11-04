# 

# \# Assignment 05 — Views and Emails

# 

# \*\*Student name:\*\* Seif Mohamed  

# \*\*Group / ID:\*\* Group #3

# 

# \## Failures Observed

# 

# The original application produced several concurrency failures:

# 

# 1\. Dictionary corruption caused by concurrent modifications.

# 2\. ArgumentException when multiple threads attempted to add the same dictionary key.

# 3\. KeyNotFoundException when a dictionary entry was accessed during concurrent modification.

# 4\. Queue corruption caused by multiple threads calling Enqueue and Dequeue simultaneously.

# 5\. InvalidOperationException during concurrent collection operations.

# 

# \## What I Changed

# 

# 1\. Added a dedicated lock for the Dictionary operations.

# 2\. Protected the complete view registration operation, including checking whether the product exists and incrementing its count.

# 3\. Added a dedicated lock for Queue operations.

# 4\. Protected both Enqueue and Dequeue operations.

# 5\. Added a separate lock for the SentEmails collection.

# 6\. Updated the email workers to check the queue and producer completion state safely.

# 

# \## Collection Safety vs Business-Rule Safety

# 

# Making a collection thread-safe does not automatically make the complete business operation thread-safe.

# 

# For example, checking whether a product exists and then adding it to a Dictionary are two separate operations. Even if individual collection operations were safe, another thread could interfere between those operations.

# 

# The entire business operation must be protected when multiple steps depend on the same shared state.

# 

# \## Fixed Results

# 

# | Run | Views Expected | Views Actual | Emails Placed | Unique Emails | Total Sends |

# |---|---:|---:|---:|---:|---:|

# | Run 1 | 50,000 | 50,000 | 20 | 20 | 20 |

# | Run 2 | 50,000 | 50,000 | 20 | 20 | 20 |

# | Run 3 | 50,000 | 50,000 | 20 | 20 | 20 |

# 

# \## Conclusion

# 

# The updated application consistently records all product views and processes every email exactly once without concurrency exceptions.



