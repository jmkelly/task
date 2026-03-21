using Spectre.Console.Cli;
using System.ComponentModel;
using Task.Api;
using Spectre.Console;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Task.Cli
{
    [Description("Run the Task API server in the foreground.")]
    public sealed class ServerRunCommand : AsyncCommand<ServerRunCommand.Settings>
    {
        private const string ReadySignal = "Task.Ready";

        public sealed class Settings : CommandSettings
        {
            [CommandOption("--urls <URLS>")]
            [Description("Override server URLs (e.g., http://localhost:8080). Explicit configuration disables port auto-selection.")]
            public string? Urls { get; set; }

            [CommandOption("--database-path <PATH>")]
            [Description("Database path for the API server (default: config dir tasks.db, e.g. ~/.config/task/tasks.db).")]
            public string? DatabasePath { get; set; }

            [CommandOption("--ready-file <PATH>")]
            [Description("Write readiness details to this file once the server is ready.")]
            public string? ReadyFile { get; set; }
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var options = new ApiHostOptions
            {
                Urls = settings.Urls,
                DatabasePath = settings.DatabasePath,
                ApplicationName = typeof(ApiHost).Assembly.GetName().Name,
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot")
            };

            var args = context.Remaining.Raw.ToArray();
            await using var handle = await ApiHost.StartAsync(args, options, cancellationToken);

            var readiness = new
            {
                status = "ready",
                port = handle.Result.Port,
                url = handle.Result.Url,
                reason = handle.Result.Reason
            };

            if (!string.IsNullOrWhiteSpace(settings.ReadyFile))
            {
#pragma warning disable IL2026
                var payload = System.Text.Json.JsonSerializer.Serialize(readiness, JsonHelper.Options);
#pragma warning restore IL2026
                await File.WriteAllTextAsync(settings.ReadyFile, payload, cancellationToken);
            }

            if (settings.ReadyFile == null)
            {
                Console.WriteLine($"{ReadySignal} port={handle.Result.Port} url={handle.Result.Url} reason={handle.Result.Reason}");
                AnsiConsole.MarkupLine($"[green]Server ready[/] url={handle.Result.Url} port={handle.Result.Port} reason={handle.Result.Reason}");
            }

            await handle.WaitForShutdownAsync(cancellationToken);
            return 0;
        }
    }
}
