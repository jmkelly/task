using System;
using System.Threading;
using Spectre.Console.Cli;

namespace Task.Cli
{
    public sealed class ServerStartCommand : AsyncCommand<ServerStartCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
        }

        public override async global::System.Threading.Tasks.Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var processManager = new ServerProcess();
            var existing = ServerStateStore.Load();

            if (existing != null && processManager.IsRunning(existing))
            {
                Console.WriteLine($"Task API already running at {existing.Url} (pid {existing.ProcessId}).");
                return 0;
            }

            try
            {
                var state = await processManager.StartAsync(cancellationToken);
                ServerStateStore.Save(state);

                var config = Config.Load();
                config.ApiUrl = state.Url;
                config.Save();

                Console.WriteLine($"Task API started at {state.Url} (port {state.Port}).");
                return 0;
            }
            catch (Exception ex)
            {
                ErrorHelper.ShowError($"Failed to start API server: {ex.Message}");
                return 1;
            }
        }
    }

    public sealed class ServerStatusCommand : AsyncCommand<ServerStatusCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
        }

        public override global::System.Threading.Tasks.Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var processManager = new ServerProcess();
            var state = ServerStateStore.Load();

            if (state == null)
            {
                Console.WriteLine("Task API is not running.");
                return global::System.Threading.Tasks.Task.FromResult(0);
            }

            if (!processManager.IsRunning(state))
            {
                Console.WriteLine("Task API is not running.");
                return global::System.Threading.Tasks.Task.FromResult(0);
            }

            Console.WriteLine($"Task API is running at {state.Url} (pid {state.ProcessId}).");
            return global::System.Threading.Tasks.Task.FromResult(0);
        }
    }

    public sealed class ServerStopCommand : AsyncCommand<ServerStopCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
        }

        public override async global::System.Threading.Tasks.Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var processManager = new ServerProcess();
            var state = ServerStateStore.Load();

            if (state == null)
            {
                Console.WriteLine("Task API is not running.");
                return 0;
            }

            if (!processManager.IsRunning(state))
            {
                ServerStateStore.Clear();
                Console.WriteLine("Task API is not running.");
                return 0;
            }

            try
            {
                var stopped = await processManager.StopAsync(state, cancellationToken);
                if (!stopped)
                {
                    ErrorHelper.ShowError("Failed to stop API server.");
                    return 1;
                }

                ServerStateStore.Clear();
                Console.WriteLine("Task API stopped.");
                return 0;
            }
            catch (Exception ex)
            {
                ErrorHelper.ShowError($"Failed to stop API server: {ex.Message}");
                return 1;
            }
        }
    }
}
