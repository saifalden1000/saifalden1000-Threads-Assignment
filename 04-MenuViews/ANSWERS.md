# 

# \# Assignment 04 — Menu Views

# 

# \*\*Student name:\*\* Seif Mohamed  

# \*\*Group / ID:\*\* Group #3

# 

# \## Original Results

# 

# Expected: 400000  

# Actual: 220513

# 

# The actual count was lower than expected because multiple threads were updating the shared counter simultaneously.

# 

# \## Why Expected and Actual Disagree

# 

# The expression Views++ is not an atomic operation. It consists of reading the current value, incrementing it, and writing the result back.

# 

# When multiple threads execute these operations at the same time, some updates overwrite others. This causes lost increments.

# 

# \## What I Changed

# 

# I replaced Views++ with Interlocked.Increment(ref Views).

# 

# Interlocked.Increment performs the increment atomically, ensuring that concurrent threads do not lose updates.

# 

# \## Fixed Results

# 

# Expected: 400000  

# Actual: 400000

# 

# The fixed application returned the correct count.

# 

# \## When I Would Protect the Update Differently

# 

# For a single integer counter, Interlocked.Increment is a simple and efficient solution.

# 

# I would use lock when an operation involves multiple shared variables or multiple steps that must execute together as one critical section.



