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
import json

# Path to the Task executable
TASK_EXECUTABLE = "../binaries/task-linux-x64/Task.Cli"

def generate_random_task():
    """Generate a random task description."""
    words = ['task', 'item', 'work', 'project', 'fix', 'implement', 'test', 'review', 'update', 'create']
    return ' '.join(random.choices(words, k=random.randint(3, 8)))

def generate_random_description():
    """Generate a random task description."""
    descriptions = [
        'This is a detailed description of the task that needs to be completed.',
        'A comprehensive task requiring careful attention to detail.',
        'Important work item that should be prioritized.',
        'Complex implementation that needs thorough testing.',
        'Critical update that affects multiple components.',
        'Routine maintenance and improvement task.',
        'New feature implementation with testing requirements.',
        'Bug fix for existing functionality.',
        'Performance optimization and code review.',
        'Documentation and knowledge sharing task.'
    ]
    return random.choice(descriptions)

def generate_random_tags():
    """Generate random tags for a task."""
    all_tags = ['urgent', 'bug', 'feature', 'enhancement', 'documentation', 'testing', 'review', 'performance', 'ui', 'backend', 'frontend', 'api', 'database', 'security']
    num_tags = random.randint(0, 3)
    return ','.join(random.sample(all_tags, num_tags)) if num_tags > 0 else None

def generate_random_project():
    """Generate a random project name."""
    projects = ['web-app', 'mobile-app', 'api-server', 'database', 'infrastructure', 'testing', 'documentation', 'maintenance', None]
    return random.choice(projects)

def generate_random_assignee():
    """Generate a random assignee."""
    assignees = ['alice', 'bob', 'charlie', 'diana', 'eve', 'frank', 'grace', None]
    return random.choice(assignees)

def generate_random_priority():
    """Generate a random priority."""
    return random.choice(['low', 'medium', 'high'])

def generate_random_due_date():
    """Generate a random due date within the next 30 days."""
    days_ahead = random.randint(1, 30)
    due_date = time.time() + (days_ahead * 24 * 60 * 60)
    return time.strftime('%Y-%m-%d', time.localtime(due_date))

def run_command(command):
    """Run a Task CLI command."""
    full_command = [TASK_EXECUTABLE]
    full_command.extend(command)
    full_command.extend(['--json'])
    try:
        result = subprocess.run(full_command, capture_output=True, text=True, timeout=30, cwd='.')
        success = result.returncode == 0
        if success:
            try:
                data = json.loads(result.stdout)
                return True, data, ""
            except json.JSONDecodeError:
                return True, result.stdout, ""
        else:
            return False, "", result.stderr
    except subprocess.TimeoutExpired:
        return False, "", "Command timed out"

def add_task_stress(num_tasks=100, num_workers=10):
    """Stress test adding tasks concurrently."""
    print(f"Starting add task stress test: {num_tasks} tasks with {num_workers} workers")

    def add_single_task(task_id):
        task_desc = f"Stress test task {task_id}: {generate_random_task()}"
        description = generate_random_description()
        priority = generate_random_priority()
        due_date = generate_random_due_date()
        tags = generate_random_tags()
        project = generate_random_project()
        assignee = generate_random_assignee()
        
        command = ['add', '-t', task_desc, '-d', description, '-p', priority, '--due-date', due_date]
        
        if tags:
            command.extend(['--tags', tags])
        if project:
            command.extend(['--project', project])
        if assignee:
            command.extend(['--assignee', assignee])
        
        success, data, error = run_command(command)
        if success and isinstance(data, dict) and 'uid' in data:
            return data['uid']
        return None

    start_time = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=num_workers) as executor:
        futures = [executor.submit(add_single_task, i) for i in range(num_tasks)]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]

    end_time = time.time()
    duration = end_time - start_time

    added_ids = [rid for rid in results if rid is not None]
    success_count = len(added_ids)
    print(f"Add tasks completed in {duration:.2f} seconds")
    print(f"Success rate: {success_count}/{num_tasks} ({success_count/num_tasks*100:.1f}%)")
    return duration, added_ids

def list_tasks_stress(num_requests=100, num_workers=10):
    """Stress test listing tasks concurrently."""
    print(f"Starting list tasks stress test: {num_requests} requests with {num_workers} workers")

    def list_tasks():
        return run_command(['list'])

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

def delete_task_stress(ids, num_workers=10):
    """Stress test deleting tasks concurrently."""
    print(f"Starting delete task stress test: {len(ids)} tasks with {num_workers} workers")

    def delete_single_task(task_id):
        return run_command(['delete', task_id])

    start_time = time.time()
    with concurrent.futures.ThreadPoolExecutor(max_workers=num_workers) as executor:
        futures = [executor.submit(delete_single_task, tid) for tid in ids]
        results = [future.result() for future in concurrent.futures.as_completed(futures)]

    end_time = time.time()
    duration = end_time - start_time

    success_count = sum(1 for success, _, _ in results if success)
    print(f"Delete tasks completed in {duration:.2f} seconds")
    print(f"Success rate: {success_count}/{len(ids)} ({success_count/len(ids)*100:.1f}%)")
    return duration, success_count

def search_tasks_stress(num_requests=100, num_workers=10):
    """Stress test searching tasks concurrently."""
    print(f"Starting search tasks stress test: {num_requests} requests with {num_workers} workers")

    search_terms = ['task', 'work', 'project', 'test', 'stress']

    def search_tasks():
        term = random.choice(search_terms)
        return run_command(['search', term])

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

def main():
    if not os.path.exists(TASK_EXECUTABLE):
        print(f"Error: Task executable not found at {TASK_EXECUTABLE}")
        sys.exit(1)

    print("=== Task CLI Stress Test ===")
    print(f"Using executable: {TASK_EXECUTABLE}")
    print()

    # Run stress tests
    add_duration, added_ids = add_task_stress(num_tasks=100, num_workers=10)
    print()

    delete_duration, delete_success = delete_task_stress(added_ids, num_workers=10)
    print()

    list_duration, list_success = list_tasks_stress(num_requests=50, num_workers=10)
    print()

    search_duration, search_success = search_tasks_stress(num_requests=50, num_workers=10)
    print()

    # Summary
    print("=== Summary ===")
    print(f"Add tasks: {len(added_ids)}/100 successful ({len(added_ids)/100*100:.1f}%), {add_duration:.2f}s")
    print(f"Delete tasks: {delete_success}/{len(added_ids)} successful ({delete_success/len(added_ids)*100:.1f}%), {delete_duration:.2f}s")
    print(f"List tasks: {list_success}/50 successful ({list_success/50*100:.1f}%), {list_duration:.2f}s")
    print(f"Search tasks: {search_success}/50 successful ({search_success/50*100:.1f}%), {search_duration:.2f}s")

if __name__ == "__main__":
    main()
