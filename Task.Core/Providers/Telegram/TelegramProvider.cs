using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Task.Core.Providers.Telegram;

public sealed class TelegramProvider : ITelegramProvider
{
    private readonly HttpClient _httpClient;
    private readonly TelegramProviderOptions _options;
    private readonly ILogger<TelegramProvider> _logger;

    public TelegramProvider(
        HttpClient httpClient,
        IOptions<TelegramProviderOptions> options,
        ILogger<TelegramProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Telegram provider disabled. Skipping send.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BotToken) || string.IsNullOrWhiteSpace(_options.ChatId))
        {
            throw new InvalidOperationException("Telegram provider is enabled but BotToken or ChatId is missing.");
        }

        var request = new TelegramSendMessageRequest(_options.ChatId, message);
        var payload = JsonSerializer.Serialize(request, TelegramJsonContext.Default.TelegramSendMessageRequest);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync("sendMessage", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Telegram send failed with status {StatusCode}. Response: {Response}",
                response.StatusCode,
                body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Telegram message delivered to chat {ChatId}.", _options.ChatId);
    }
}
