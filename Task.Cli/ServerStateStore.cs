using System;
using System.IO;
using System.Text.Json;

namespace Task.Cli
{
    public static class ServerStateStore
    {
        private static readonly string StateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".task");
        private static readonly string StateFile = Path.Combine(StateDir, "server.json");

        public static ServerState? Load()
        {
            if (!File.Exists(StateFile))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(StateFile);
                return JsonSerializer.Deserialize(json, TaskJsonContext.Default.ServerState);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void Save(ServerState state)
        {
            try
            {
                Directory.CreateDirectory(StateDir);
                var json = JsonSerializer.Serialize(state, TaskJsonContext.Default.ServerState);
                File.WriteAllText(StateFile, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save server state: {ex.Message}", ex);
            }
        }

        public static void Clear()
        {
            if (!File.Exists(StateFile))
            {
                return;
            }

            try
            {
                File.Delete(StateFile);
            }

            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to clear server state: {ex.Message}", ex);
            }
        }
    }
}
