using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using Xunit;
using Xunit.Sdk;

namespace Task.Cli.Tests.IntegrationTests
{
    public sealed class ServerStatusIntegrationTests : IDisposable
    {
        private static readonly string CliAssemblyPath = ResolveCliAssemblyPath();
        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"task-server-status-tests-{Guid.NewGuid():N}");

        [Fact]
        public async global::System.Threading.Tasks.Task ServerStatus_ReportsForegroundServerRunInstanceAsRunning()
        {
            var homeDirectory = Path.Combine(_tempRoot, "home");
            var configHome = Path.Combine(_tempRoot, "config-home");
            var readyFile = Path.Combine(_tempRoot, "ready.json");
            var databasePath = Path.Combine(_tempRoot, "data", "tasks.db");

            Directory.CreateDirectory(homeDirectory);
            Directory.CreateDirectory(configHome);
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

            using var serverProcess = StartCliProcess(
                new[] { "server", "run", "--ready-file", readyFile, "--database-path", databasePath },
                homeDirectory,
                configHome);

            var readyUrl = await WaitForReadyUrlAsync(serverProcess, readyFile, TimeSpan.FromSeconds(30));

            try
            {
                var statusResult = await RunCliCommandAsync(
                    new[] { "server", "status" },
                    homeDirectory,
                    configHome,
                    TimeSpan.FromSeconds(30));

                Assert.True(
                    statusResult.ExitCode == 0,
                    $"Expected server status to succeed. ExitCode={statusResult.ExitCode}\nSTDOUT:\n{statusResult.StandardOutput}\nSTDERR:\n{statusResult.StandardError}");
                Assert.Contains("Task API is running at", statusResult.StandardOutput);
                Assert.Contains(readyUrl, statusResult.StandardOutput);
            }
            finally
            {
                await StopServerAsync(serverProcess, homeDirectory, configHome);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        private static string ResolveCliAssemblyPath()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
            var cliAssemblyPath = Path.Combine(repositoryRoot, "Task.Cli", "bin", "Debug", "net10.0", "linux-x64", "Task.Cli.dll");

            if (!File.Exists(cliAssemblyPath))
            {
                throw new XunitException($"Task CLI assembly not found at expected path: {cliAssemblyPath}");
            }

            return cliAssemblyPath;
        }

        private static Process StartCliProcess(string[] arguments, string homeDirectory, string configHome)
        {
            var startInfo = CreateStartInfo(arguments, homeDirectory, configHome, redirectOutput: true);
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new XunitException("Failed to start task CLI process.");
            }

            return process;
        }

        private static async global::System.Threading.Tasks.Task<string> WaitForReadyUrlAsync(Process serverProcess, string readyFile, TimeSpan timeout)
        {
            using var cts = new CancellationTokenSource(timeout);

            while (!cts.IsCancellationRequested)
            {
                if (serverProcess.HasExited)
                {
                    var stdout = await serverProcess.StandardOutput.ReadToEndAsync();
                    var stderr = await serverProcess.StandardError.ReadToEndAsync();
                    throw new XunitException(
                        $"Foreground server exited before writing readiness details. ExitCode={serverProcess.ExitCode}\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
                }

                if (File.Exists(readyFile))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(readyFile, cts.Token);
                        using var document = JsonDocument.Parse(json);
                        if (document.RootElement.TryGetProperty("url", out var urlElement))
                        {
                            var url = urlElement.GetString();
                            if (!string.IsNullOrWhiteSpace(url))
                            {
                                return url;
                            }
                        }
                    }
                    catch (IOException)
                    {
                    }
                    catch (JsonException)
                    {
                    }
                }

                await global::System.Threading.Tasks.Task.Delay(TimeSpan.FromMilliseconds(200), cts.Token);
            }

            throw new XunitException($"Timed out waiting for foreground server readiness file: {readyFile}");
        }

        private static async global::System.Threading.Tasks.Task<CliCommandResult> RunCliCommandAsync(
            string[] arguments,
            string homeDirectory,
            string configHome,
            TimeSpan timeout)
        {
            using var process = new Process
            {
                StartInfo = CreateStartInfo(arguments, homeDirectory, configHome, redirectOutput: true)
            };

            if (!process.Start())
            {
                throw new XunitException("Failed to start task CLI command.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = new CancellationTokenSource(timeout);
            await process.WaitForExitAsync(cts.Token);

            return new CliCommandResult(
                process.ExitCode,
                await stdoutTask,
                await stderrTask);
        }

        private static async global::System.Threading.Tasks.Task StopServerAsync(Process serverProcess, string homeDirectory, string configHome)
        {
            if (serverProcess.HasExited)
            {
                return;
            }

            try
            {
                await RunCliCommandAsync(
                    new[] { "server", "stop" },
                    homeDirectory,
                    configHome,
                    TimeSpan.FromSeconds(30));
            }
            catch
            {
            }

            if (!serverProcess.HasExited)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await serverProcess.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    serverProcess.Kill(entireProcessTree: true);
                    await serverProcess.WaitForExitAsync();
                }
            }
        }

        private static ProcessStartInfo CreateStartInfo(
            string[] arguments,
            string homeDirectory,
            string configHome,
            bool redirectOutput)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = Path.GetDirectoryName(CliAssemblyPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput
            };

            startInfo.ArgumentList.Add(CliAssemblyPath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            startInfo.Environment["HOME"] = homeDirectory;
            startInfo.Environment["XDG_CONFIG_HOME"] = configHome;
            startInfo.Environment["DOTNET_CLI_HOME"] = homeDirectory;
            startInfo.Environment["NO_COLOR"] = "1";

            return startInfo;
        }

        private sealed record CliCommandResult(int ExitCode, string StandardOutput, string StandardError);
    }
}
