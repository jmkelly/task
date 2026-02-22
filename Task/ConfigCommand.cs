using Spectre.Console.Cli;
using System.ComponentModel;
using System.Threading.Tasks;

namespace TaskApp
{
    public class ConfigSetCommand : AsyncCommand<ConfigSetCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<key>")]
            [Description("The configuration key to set (api-url or defaultOutput)")]
            public string Key { get; set; } = null!;

            [CommandArgument(1, "<value>")]
            [Description("The value to set for the key")]
            public string Value { get; set; } = null!;
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var config = Config.Load();
            try
            {
                await config.SetValueAsync(settings.Key, settings.Value);
                config.Save();
                Console.WriteLine($"Set {settings.Key} to {settings.Value}");
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Failed to set config: {ex.Message}");
                return 1;
            }
        }
    }

    public class ConfigGetCommand : AsyncCommand<ConfigGetCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<key>")]
            [Description("The configuration key to get (api-url or defaultOutput)")]
            public string Key { get; set; } = null!;
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var config = Config.Load();
            var value = config.GetValue(settings.Key);
            if (value == null)
            {
                Console.Error.WriteLine($"ERROR: Unknown config key: {settings.Key}");
                return System.Threading.Tasks.Task.FromResult(1);
            }
            Console.WriteLine(value);
            return System.Threading.Tasks.Task.FromResult(0);
        }
    }

    public class ConfigUnsetCommand : AsyncCommand<ConfigUnsetCommand.Settings>
    {
        public class Settings : CommandSettings
        {
            [CommandArgument(0, "<key>")]
            [Description("The configuration key to unset (api-url or defaultOutput)")]
            public string Key { get; set; } = null!;
        }

        public override Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var config = Config.Load();
            try
            {
                config.UnsetValue(settings.Key);
                config.Save();
                Console.WriteLine($"Unset {settings.Key}");
                return System.Threading.Tasks.Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: Failed to unset config: {ex.Message}");
                return System.Threading.Tasks.Task.FromResult(1);
            }
        }
    }

    public class ConfigListCommand : Command<ConfigListCommand.Settings>
    {
        public class Settings : CommandSettings
        {
        }

        public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var config = Config.Load();
            Console.WriteLine("Current configuration:");
            if (!string.IsNullOrEmpty(config.ApiUrl))
            {
                Console.WriteLine($"api-url: {config.ApiUrl}");
            }
            else
            {
                Console.WriteLine("api-url: (not set)");
            }
            Console.WriteLine($"defaultOutput: {config.DefaultOutput}");
            return 0;
        }
    }
}