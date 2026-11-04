# 

# \# Assignment 02 — Table Booking

# 

# \*\*Student name:\*\* Seif Mohamed  

# \*\*Group / ID:\*\* Group #3

# 

# \## Original Results

# 

# The application was executed three times before fixing the problem.

# 

# | Run | Booked | Tables Left |

# |---|---:|---:|

# | Run 1 | 31 | 4 |

# | Run 2 | 28 | -14 |

# | Run 3 | 1 | 4 |

# 

# The results were inconsistent. Some runs allowed more than five bookings, while other runs lost updates to the shared counters.

# 

# \## What Was Wrong

# 

# The Book method accessed shared state from multiple threads without synchronization.

# 

# The availability check and the counter updates were separate operations. Multiple threads could check the available tables at the same time before any of them updated the counters.

# 

# This caused a race condition, resulting in overselling, negative table counts, and lost updates.

# 

# \## How I Fixed It

# 

# I added a private lock object and used the lock statement around the entire booking operation.

# 

# This ensures that only one thread at a time can check table availability and update the shared counters.

# 

# The booking check and both counter updates are now protected as one critical section.

# 

# \## Fixed Results

# 

# The application was executed three more times after applying the fix.

# 

# | Run | Booked | Tables Left |

# |---|---:|---:|

# | Run 1 | 5 | 0 |

# | Run 2 | 5 | 0 |

# | Run 3 | 5 | 0 |

# 

# \## Conclusion

# 

# The lock statement prevents multiple threads from modifying the booking state simultaneously.

# 

# All three fixed runs produced the correct totals, with exactly five successful bookings and zero remaining tables.



