#!/usr/bin/env python3
"""
Stress test script for Task CLI application.
This script simulates high load by running multiple concurrent operations on the Task CLI.
"""

import subprocess
import concurrent.futures
import time
import random
import string
import os
import sys

# Path to the Task executable
TASK_EXECUTABLE = "../binaries/task-linux-x64/Task"

# Database file for stress testing
STRESS_DB = "stress_test.db"

def generate_random_task():
    """Generate a random task description."""
    words = ['task', 'item', 'work', 'project', 'fix', 'implement', 'test', 'review', 'update', 'create']
    return ' '.join(random.choices(words, k=random.randint(3, 8)))

def run_command(command, db_path=None):
    """Run a Task CLI command."""
    full_command = [TASK_EXECUTABLE]
    if db_path:
        full_command.extend(['--db', db_path])
    full_command.extend(command)
    try:
        result = subprocess.run(full_command, capture_output=True, text=True, timeout=30, cwd='.')
        return result.returncode == 0, result.stdout, result.stderr
    except subprocess.TimeoutExpired:
        return False, "", "Command timed out"

def add_task_stress(num_tasks=100, num_workers=10):
    """Stress test adding tasks concurrently."""
    print(f"Starting add task stress test: {num_tasks} tasks with {num_workers} workers")

    def add_single_task(task_id):
        task_desc = f"Stress test task {task_id}: {generate_random_task()}"
        return run_command(['add', '-t', task_desc])

    start_time = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=num_workers) as executor:
        futures = [executor.submit(add_single_task, i) for i in range(num_tasks)]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]

    end_time = time.time()
    duration = end_time - start_time

    success_count = sum(1 for success, _, _ in results if success)
    print(f"Add tasks completed in {duration:.2f} seconds")
    print(f"Success rate: {success_count}/{num_tasks} ({success_count/num_tasks*100:.1f}%)")
    return duration, success_count

def list_tasks_stress(num_requests=100, num_workers=10):
    """Stress test listing tasks concurrently."""
    print(f"Starting list tasks stress test: {num_requests} requests with {num_workers} workers")

    def list_tasks():
        return run_command(['list', '--plain'])

    start_time = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=num_workers) as executor:
        futures = [executor.submit(list_tasks) for _ in range(num_requests)]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]

    end_time = time.time()
    duration = end_time - start_time

    success_count = sum(1 for success, _, _ in results if success)
    print(f"List tasks completed in {duration:.2f} seconds")
    print(f"Success rate: {success_count}/{num_requests} ({success_count/num_requests*100:.1f}%)")
    return duration, success_count

def search_tasks_stress(num_requests=100, num_workers=10):
    """Stress test searching tasks concurrently."""
    print(f"Starting search tasks stress test: {num_requests} requests with {num_workers} workers")

    search_terms = ['task', 'work', 'project', 'test', 'stress']

    def search_tasks():
        term = random.choice(search_terms)
        return run_command(['search', term, '--plain'])

    start_time = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=num_workers) as executor:
        futures = [executor.submit(search_tasks) for _ in range(num_requests)]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]

    end_time = time.time()
    duration = end_time - start_time

    success_count = sum(1 for success, _, _ in results if success)
    print(f"Search tasks completed in {duration:.2f} seconds")
    print(f"Success rate: {success_count}/{num_requests} ({success_count/num_requests*100:.1f}%)")
    return duration, success_count

def cleanup_db():
    """Remove the stress test database."""
    if os.path.exists(STRESS_DB):
        os.remove(STRESS_DB)
        print(f"Cleaned up {STRESS_DB}")

def main():
    if not os.path.exists(TASK_EXECUTABLE):
        print(f"Error: Task executable not found at {TASK_EXECUTABLE}")
        sys.exit(1)

    # Clean up any existing tasks.db
    if os.path.exists('tasks.db'):
        os.remove('tasks.db')
        print("Cleaned up existing tasks.db")

    print("=== Task CLI Stress Test ===")
    print(f"Using executable: {TASK_EXECUTABLE}")
    print(f"Database: tasks.db (in current directory)")
    print()

    # Run stress tests
    add_duration, add_success = add_task_stress(num_tasks=100, num_workers=10)
    print()

    list_duration, list_success = list_tasks_stress(num_requests=50, num_workers=10)
    print()

    search_duration, search_success = search_tasks_stress(num_requests=50, num_workers=10)
    print()

    # Summary
    print("=== Summary ===")
    print(f"Add tasks: {add_success}/100 successful ({add_success:.1f}%), {add_duration:.2f}s")
    print(f"List tasks: {list_success}/50 successful ({list_success:.1f}%), {list_duration:.2f}s")
    print(f"Search tasks: {search_success}/50 successful ({search_success:.1f}%), {search_duration:.2f}s")

    # Cleanup
    if os.path.exists('tasks.db'):
        os.remove('tasks.db')

if __name__ == "__main__":
    main()