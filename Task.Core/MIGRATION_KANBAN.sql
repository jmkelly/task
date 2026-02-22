-- Migration script to update status values from old to new Kanban workflow
-- Run this after updating SCHEMA.sql

-- Update existing 'pending' tasks to 'todo'
UPDATE tasks SET status = 'todo' WHERE status = 'pending';

-- Update existing 'completed' tasks to 'done'
UPDATE tasks SET status = 'done' WHERE status = 'completed';

-- Update the updated_at timestamp for migrated tasks
UPDATE tasks SET updated_at = datetime('now') WHERE status IN ('todo', 'done');