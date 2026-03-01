-- Migration script to add archive fields to tasks
-- Run this after updating SCHEMA.sql

ALTER TABLE tasks ADD COLUMN archived INTEGER NOT NULL DEFAULT 0;
ALTER TABLE tasks ADD COLUMN archived_at TEXT;
CREATE INDEX IF NOT EXISTS idx_tasks_archived ON tasks(archived);
