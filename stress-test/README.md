# Task CLI Stress Test

This project contains a stress testing script for the Task CLI application. The stress test simulates high concurrent load to evaluate the application's performance under stress.

## Prerequisites

- Python 3.6+
- The Task CLI executable built and available at `../binaries/task-linux-x64/Task` (relative to this directory)

## Running the Stress Test

1. Ensure the Task CLI is built (run `../build.sh` from the project root)
2. Navigate to this directory: `cd stress-test`
3. Run the stress test: `python3 stress_test.py`

## What the Stress Test Does

The stress test performs three types of operations concurrently:

1. **Add Tasks**: Creates 1000 new tasks using 50 concurrent workers
2. **List Tasks**: Lists all tasks 500 times using 50 concurrent workers
3. **Search Tasks**: Searches for tasks 500 times using 50 concurrent workers

Each operation measures:
- Total execution time
- Success rate (percentage of operations that completed without error)

## Expected Behavior

- **Add operations**: May have lower success rates due to SQLite database locking under high concurrency
- **List/Search operations**: Should have high success rates as they are read-only

## Customizing the Test

You can modify the parameters in `stress_test.py`:

- `num_tasks`: Number of add operations
- `num_workers`: Number of concurrent threads for add operations
- `num_requests`: Number of list/search operations

## Database

The test uses a SQLite database file `tasks.db` in the current directory. The database is cleaned up before and after each test run.

## Troubleshooting

- If the Task executable is not found, ensure you've built it with `../build.sh`
- For very high concurrency, you may need to increase system limits or use a different database backend