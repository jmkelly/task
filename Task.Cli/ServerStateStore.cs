using System;
using System.IO;
using System.Text.Json;

namespace Task.Cli
{
    public static class ServerStateStore
    {
        public static ServerState? Load()
        {
            var stateFile = GetStateFilePath();
            if (!File.Exists(stateFile))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(stateFile);
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
                Directory.CreateDirectory(GetStateDirectory());
                var json = JsonSerializer.Serialize(state, TaskJsonContext.Default.ServerState);
                File.WriteAllText(GetStateFilePath(), json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save server state: {ex.Message}", ex);
            }
        }

        public static void Clear()
        {
            var stateFile = GetStateFilePath();
            if (!File.Exists(stateFile))
            {
                return;
            }

            try
            {
                File.Delete(stateFile);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to clear server state: {ex.Message}", ex);
            }
        }

        public static void Clear(ServerState expectedState)
        {
            if (expectedState == null)
            {
                throw new ArgumentNullException(nameof(expectedState));
            }

            var currentState = Load();
            if (currentState == null)
            {
                return;
            }

            var sameProcess = currentState.ProcessId == expectedState.ProcessId;
            var sameUrl = string.Equals(currentState.Url, expectedState.Url, StringComparison.OrdinalIgnoreCase);
            if (!sameProcess || !sameUrl)
            {
                return;
            }

            Clear();
        }

        private static string GetStateDirectory()
        {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, ".task");
        }

        private static string GetStateFilePath()
        {
            return Path.Combine(GetStateDirectory(), "server.json");
        }
    }
}
