using System;

namespace Task.Cli
{
    public sealed class ServerState
    {
        public int ProcessId { get; set; }
        public string Url { get; set; } = string.Empty;
        public int Port { get; set; }
        public string? Reason { get; set; }
        public string? BinaryPath { get; set; }
        public DateTimeOffset StartedAt { get; set; }
    }
}
