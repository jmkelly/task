using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Task.Cli {
    public class TelegramDiscoverChatIdCommand : AsyncCommand<TelegramDiscoverChatIdCommand.Settings> {
        public class Settings : CommandSettings {
        }

        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken) {
            var config = Config.Load();
            var botToken = config.Telegram?.BotToken;
            if (string.IsNullOrEmpty(botToken)) {
                AnsiConsole.MarkupLine("[red]ERROR:[/] Telegram botToken not set in config (set via [yellow]task config set telegram.botToken <TOKEN>[/])");
                return 1;
            }
            using var client = new HttpClient();
            var apiUrl = $"https://api.telegram.org/bot{botToken}/getUpdates";
            try {
                var response = await client.GetAsync(apiUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var resultArr = doc.RootElement.GetProperty("result");
                if (resultArr.GetArrayLength() == 0) {
                    AnsiConsole.MarkupLine("[yellow]No recent Telegram messages found. Send a message to your bot and try again.[/]");
                    return 0;
                }
                AnsiConsole.MarkupLine("[green]Recent Telegram chats:[/]");
                var chats = new List<(string, long, string)>();
                int idx = 1;
                foreach (var update in resultArr.EnumerateArray()) {
                    if (!update.TryGetProperty("message", out var msg)) continue;
                    var chat = msg.GetProperty("chat");
                    var chatId = chat.GetProperty("id").GetInt64();
                    var chatType = chat.GetProperty("type").GetString();
                    var titleOrName = chatType == "private" ? (chat.TryGetProperty("username", out var user) ? user.GetString() : chat.GetProperty("first_name").GetString())
                        : chat.TryGetProperty("title", out var title) ? title.GetString() : chatId.ToString();
                    var textPreview = msg.TryGetProperty("text", out var text) ? text.GetString() : "<no text>";
                    chats.Add((titleOrName, chatId, textPreview));
                }
                // Remove duplicate chatIds
                chats = chats.GroupBy(c => c.Item2).Select(g => g.First()).ToList();
                for (int i = 0; i < chats.Count; i++) {
                    var chat = chats[i];
                    AnsiConsole.MarkupLine($"[yellow]{i+1}[/]: [bold]{chat.Item1}[/] (chatId=[blue]{chat.Item2}[/]) – " +
                        $"Message preview: [italic]{chat.Item3}[/]");
                }
                if (chats.Count == 0) {
                    AnsiConsole.MarkupLine("[yellow]No chatId found in recent updates. Make sure your bot received a message.[/]");
                    return 1;
                }
                var selectedIndex = AnsiConsole.Prompt(new SelectionPrompt<int>()
                    .Title("Select a chat to set as your Telegram chatId:")
                    .PageSize(10)
                    .AddChoices(Enumerable.Range(1, chats.Count).ToArray()));
                var selectedChat = chats[selectedIndex-1];
                // Set config value
                await config.SetValueAsync("telegram.chatId", selectedChat.Item2.ToString());
                config.Save();
                AnsiConsole.MarkupLine($"[green]telegram.chatId set to [bold]{selectedChat.Item2}[/] ([bold]{selectedChat.Item1}[/])[/]");
                return 0;
            } catch (Exception ex) {
                AnsiConsole.MarkupLine($"[red]ERROR:[/] Failed to get Telegram updates: {ex.Message}");
                return 1;
            }
        }
    }
}
