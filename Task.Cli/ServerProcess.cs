using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;

namespace Task.Cli
{
    public sealed class ServerProcess
    {
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan ReadyDelay = TimeSpan.FromMilliseconds(200);
        private const string ReadySignal = "Task.Ready";
        private static readonly string[] ReadyPrefixes =
        {
            ReadySignal,
            "Server.Started"
        };

        public async global::System.Threading.Tasks.Task<ServerState> StartAsync(CancellationToken cancellationToken)
        {
            var binaryPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(binaryPath))
            {
                throw new InvalidOperationException("Unable to resolve current executable path.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = binaryPath,
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start background server process.");
            }

            var readiness = await WaitForServerReadyAsync(process, cancellationToken);
            if (!readiness.IsReady)
            {
                throw new InvalidOperationException($"Server failed to start: {readiness.Message}");
            }

            return new ServerState
            {
                ProcessId = process.Id,
                Url = readiness.Url,
                Port = readiness.Port,
                Reason = readiness.Reason,
                BinaryPath = binaryPath,
                StartedAt = DateTimeOffset.UtcNow
            };
        }

        public bool IsRunning(ServerState state)
        {
            if (state.ProcessId <= 0)
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(state.ProcessId);
                return !process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async global::System.Threading.Tasks.Task<bool> StopAsync(ServerState state, CancellationToken cancellationToken)
        {
            Process? process;
            try
            {
                process = Process.GetProcessById(state.ProcessId);
            }
            catch (Exception)
            {
                return false;
            }

            if (process.HasExited)
            {
                return true;
            }

            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }

            try
            {
                await process.WaitForExitAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static async global::System.Threading.Tasks.Task<ServerReadiness> WaitForServerReadyAsync(Process process, CancellationToken cancellationToken)
        {
            var deadline = DateTimeOffset.UtcNow + ReadyTimeout;
            var outputTask = process.StandardOutput.ReadLineAsync();
            var errorTask = process.StandardError.ReadLineAsync();
            var readiness = new ServerReadiness();

            while (DateTimeOffset.UtcNow < deadline)
            {
                if (process.HasExited)
                {
                    return ServerReadiness.Fail("Server exited before reporting readiness.");
                }

                if (outputTask.IsCompletedSuccessfully)
                {
                    readiness.TryUpdate(outputTask.Result);
                    outputTask = process.StandardOutput.ReadLineAsync();
                }

                if (errorTask.IsCompletedSuccessfully)
                {
                    readiness.TryUpdate(errorTask.Result);
                    errorTask = process.StandardError.ReadLineAsync();
                }

                if (readiness.HasUrl && await IsServerRespondingAsync(readiness.Url, cancellationToken))
                {
                    return readiness.MarkReady();
                }

                await global::System.Threading.Tasks.Task.Delay(ReadyDelay, cancellationToken);
            }

            return ServerReadiness.Fail("Timed out waiting for server readiness output.");
        }

        private static async global::System.Threading.Tasks.Task<bool> IsServerRespondingAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                using var request = new HttpRequestMessage(HttpMethod.Head, new Uri(url));
                using var response = await client.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private sealed class ServerReadiness
        {
            public bool IsReady { get; private set; }
            public string Url { get; private set; } = string.Empty;
            public int Port { get; private set; }
            public string Reason { get; private set; } = string.Empty;
            public string Message { get; private set; } = string.Empty;
            public bool HasUrl => !string.IsNullOrWhiteSpace(Url);

            public void TryUpdate(string? line)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    return;
                }

                var trimmed = line.Trim();
                var marker = FindReadyMarker(trimmed);
                if (marker == null)
                {
                    return;
                }

                var remainder = trimmed.Substring(marker.Value.EndIndex).TrimStart();
                var parts = remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    if (part.StartsWith("url=", StringComparison.OrdinalIgnoreCase))
                    {
                        Url = part.Substring("url=".Length);
                    }
                    else if (part.StartsWith("port=", StringComparison.OrdinalIgnoreCase))
                    {
                        var portValue = part.Substring("port=".Length);
                        if (int.TryParse(portValue, out var parsedPort))
                        {
                            Port = parsedPort;
                        }
                    }
                    else if (part.StartsWith("reason=", StringComparison.OrdinalIgnoreCase))
                    {
                        Reason = part.Substring("reason=".Length);
                    }
                }

                if (!string.IsNullOrWhiteSpace(Url) && Port > 0)
                {
                    Message = trimmed;
                }
            }

            private static ReadyMarker? FindReadyMarker(string line)
            {
                foreach (var prefix in ReadyPrefixes)
                {
                    var index = line.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                    if (index < 0)
                    {
                        continue;
                    }

                    var endIndex = index + prefix.Length;
                    if (endIndex < line.Length && !char.IsWhiteSpace(line[endIndex]))
                    {
                        continue;
                    }

                    return new ReadyMarker(endIndex);
                }

                return null;
            }

            public ServerReadiness MarkReady()
            {
                IsReady = true;
                return this;
            }

            public static ServerReadiness Fail(string message)
            {
                return new ServerReadiness
                {
                    IsReady = false,
                    Message = message
                };
            }

            private readonly record struct ReadyMarker(int EndIndex);
        }
    }
}
