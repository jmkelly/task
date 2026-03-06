using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Task.Core.Providers.Telegram;
using Xunit;

namespace Task.Api.Tests.UnitTests
{
    public class TelegramProviderOptionsBindingTests
    {
        [Fact]
        public void ConfigureAndPostConfigure_EnablesProvider_WhenBotTokenAndChatIdExist()
        {
            var options = BuildOptions(new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = "token-from-env",
                ["Telegram:ChatId"] = "chat-from-env"
            });

            Assert.Equal("token-from-env", options.BotToken);
            Assert.Equal("chat-from-env", options.ChatId);
            Assert.True(options.Enabled);
        }

        [Fact]
        public void ConfigureAndPostConfigure_DisablesProvider_WhenChatIdMissing()
        {
            var options = BuildOptions(new Dictionary<string, string?>
            {
                ["Telegram:BotToken"] = "token-only"
            });

            Assert.Equal("token-only", options.BotToken);
            Assert.True(string.IsNullOrWhiteSpace(options.ChatId));
            Assert.False(options.Enabled);
        }

        [Fact]
        public void ConfigureAndPostConfigure_DisablesProvider_WhenBotTokenMissing()
        {
            var options = BuildOptions(new Dictionary<string, string?>
            {
                ["Telegram:ChatId"] = "chat-only"
            });

            Assert.True(string.IsNullOrWhiteSpace(options.BotToken));
            Assert.Equal("chat-only", options.ChatId);
            Assert.False(options.Enabled);
        }

        private static TelegramProviderOptions BuildOptions(Dictionary<string, string?> values)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            var services = new ServiceCollection();
            services.Configure<TelegramProviderOptions>(configuration.GetSection("Telegram"));
            services.PostConfigure<TelegramProviderOptions>(configuredOptions =>
            {
                var hasRequiredCredentials =
                    !string.IsNullOrWhiteSpace(configuredOptions.BotToken) &&
                    !string.IsNullOrWhiteSpace(configuredOptions.ChatId);

                configuredOptions.Enabled = hasRequiredCredentials;
            });

            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptions<TelegramProviderOptions>>().Value;
        }
    }
}
