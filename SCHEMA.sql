-- SQLite Schema for CLI Task Recording App

-- Main tasks table
CREATE TABLE IF NOT EXISTS tasks (
    id INTEGER PRIMARY KEY,
    uid TEXT UNIQUE NOT NULL,
    title TEXT NOT NULL,
    description TEXT,
    priority TEXT CHECK(priority IN ('high', 'medium', 'low')) DEFAULT 'medium',
    due_date TEXT, -- ISO 8601 format (e.g., '2023-12-31')
    tags TEXT, -- Comma-separated tags
    status TEXT CHECK(status IN ('pending', 'completed')) DEFAULT 'pending',
    created_at TEXT DEFAULT (datetime('now')),
    updated_at TEXT DEFAULT (datetime('now'))
);

-- FTS5 virtual table for full-text search on title, description, tags
CREATE VIRTUAL TABLE IF NOT EXISTS tasks_fts USING fts5(
    title, description, tags,
    content=tasks,
    content_rowid=id
);

-- Trigger to keep FTS table in sync
CREATE TRIGGER IF NOT EXISTS tasks_fts_insert AFTER INSERT ON tasks
BEGIN
    INSERT INTO tasks_fts(rowid, title, description, tags) VALUES (new.id, new.title, new.description, new.tags);
END;

CREATE TRIGGER IF NOT EXISTS tasks_fts_delete AFTER DELETE ON tasks
BEGIN
    DELETE FROM tasks_fts WHERE rowid = old.id;
END;

CREATE TRIGGER IF NOT EXISTS tasks_fts_update AFTER UPDATE ON tasks
BEGIN
    UPDATE tasks_fts SET title = new.title, description = new.description, tags = new.tags WHERE rowid = new.id;
END;

-- vss0 virtual table for vector search (requires sqlite-vss extension)
-- This will store vector embeddings for semantic search
CREATE VIRTUAL TABLE IF NOT EXISTS vss_tasks USING vss0(
    embedding(384) -- Assuming 384-dimensional embeddings from SentenceTransformers
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks(status);
CREATE INDEX IF NOT EXISTS idx_tasks_priority ON tasks(priority);
CREATE INDEX IF NOT EXISTS idx_tasks_due_date ON tasks(due_date);