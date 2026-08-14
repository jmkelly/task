using Spectre.Console.Cli;
using System.ComponentModel;
using Task.Core;

namespace Task.Cli;

/// <summary>
/// `task users create <username> [--admin] [--password <pwd>]` — local, server-host
/// administration. Writes directly to the configured database (no API call), so it
/// works before the first signup, e.g. for Auth:AllowSignup=false setups.
/// </summary>
public sealed class UsersCreateCommand : AsyncCommand<UsersCreateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<username>")]
        [Description("Username for the new account (3-32 chars, letters/digits/._-)")]
        public string Username { get; set; } = null!;

        [CommandOption("--password <PASSWORD>")]
        [Description("Password for the new account. When omitted, you will be prompted.")]
        public string? Password { get; set; }

        [CommandOption("--admin")]
        [Description("Grant the admin role (required for closed-signup servers).")]
        public bool Admin { get; set; }
    }

    public override async System.Threading.Tasks.Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        try
        {
            var config = Config.Load();
            var database = new Database(config.ToDatabaseConnectionSettings());
            await database.InitializeAsync(cancellationToken);

            var authService = new AuthService(database);
            var password = settings.Password;
            if (string.IsNullOrEmpty(password))
            {
                Console.Write("Password: ");
                password = ReadMaskedLine();
                Console.WriteLine();
            }

            if (string.IsNullOrEmpty(password))
            {
                Console.Error.WriteLine("ERROR: Password is required.");
                return 1;
            }

            var user = await authService.CreateUserAsync(settings.Username, password, settings.Admin, cancellationToken);
            Console.WriteLine($"Created user '{user.Username}' (id {user.Id}){(user.IsAdmin ? " with admin role" : "")}.");
            Console.WriteLine("They can now sign in at the board's /login page and create API keys at /keys.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: Failed to create user: {ex.Message}");
            return 1;
        }
    }

    private static string ReadMaskedLine()
    {
        var chars = new System.Collections.Generic.List<char>();
        while (true)
        {
            var key = System.Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                break;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (chars.Count > 0)
                {
                    chars.RemoveAt(chars.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                chars.Add(key.KeyChar);
            }
        }

        return new string(chars.ToArray());
    }
}
