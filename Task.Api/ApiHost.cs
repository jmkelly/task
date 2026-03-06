using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System.Net.Sockets;
using Task.Core;
using Task.Core.Providers.Telegram;

namespace Task.Api
{
    public sealed record ApiHostResult(int Port, string Url, string Reason);

    public sealed class ApiHostOptions
    {
        public string? Urls { get; set; }
        public string? DatabasePath { get; set; }
        public string? WebRootPath { get; set; }
        public string? ContentRootPath { get; set; }
        public string? ApplicationName { get; set; }
    }

    public sealed class ApiHostHandle : IAsyncDisposable
    {
        public ApiHostHandle(WebApplication app, ApiHostResult result)
        {
            App = app ?? throw new ArgumentNullException(nameof(app));
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public WebApplication App { get; }
        public ApiHostResult Result { get; }

        public global::System.Threading.Tasks.Task WaitForShutdownAsync(CancellationToken cancellationToken)
        {
            return App.WaitForShutdownAsync(cancellationToken);
        }

        public global::System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
        {
            return App.StopAsync(cancellationToken);
        }

        public global::System.Threading.Tasks.ValueTask DisposeAsync()
        {
            return App.DisposeAsync();
        }
    }

    public static class ApiHost
    {
        private const string PreferredUrl = "http://127.0.0.1:8080";
        private const string AutoAssignUrl = "http://127.0.0.1:0";

        public static async global::System.Threading.Tasks.Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
        {
            await using var handle = await StartAsync(args, new ApiHostOptions(), cancellationToken);
            await handle.WaitForShutdownAsync(cancellationToken);
            return 0;
        }

        public static async global::System.Threading.Tasks.Task<ApiHostHandle> StartAsync(string[] args, ApiHostOptions options, CancellationToken cancellationToken)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var explicitUrls = ResolveExplicitUrls(args, options);
            if (!string.IsNullOrWhiteSpace(explicitUrls))
            {
                var app = BuildApp(args, options, explicitUrls);
                await app.StartAsync(cancellationToken);
                var result = BuildResult(app, "explicit");
                app.Logger.LogInformation("Server.Started port={Port} url={Url} reason={Reason}", result.Port, result.Url, result.Reason);
                return new ApiHostHandle(app, result);
            }

            ApiHostHandle? preferredHandle = null;
            try
            {
                preferredHandle = await TryStartAsync(args, options, PreferredUrl, "preferred", cancellationToken);
                return preferredHandle;
            }
            catch (Exception ex) when (IsPortBindingFailure(ex))
            {
                if (preferredHandle != null)
                {
                    await preferredHandle.DisposeAsync();
                }
            }

            try
            {
                var fallbackHandle = await TryStartAsync(args, options, AutoAssignUrl, "auto-assigned", cancellationToken);
                return fallbackHandle;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to bind API server to an available port.", ex);
            }
        }

        private static async global::System.Threading.Tasks.Task<ApiHostHandle> TryStartAsync(string[] args, ApiHostOptions options, string url, string reason, CancellationToken cancellationToken)
        {
            var app = BuildApp(args, options, url);
            try
            {
                await app.StartAsync(cancellationToken);
            }
            catch (Exception ex) when (IsPortBindingFailure(ex))
            {
                app.Logger.LogWarning(ex, "Server.BindFailed url={Url}", url);
                await app.DisposeAsync();
                throw;
            }

            var result = BuildResult(app, reason);
            app.Logger.LogInformation("Server.Started port={Port} url={Url} reason={Reason}", result.Port, result.Url, result.Reason);
            return new ApiHostHandle(app, result);
        }

        private static WebApplication BuildApp(string[] args, ApiHostOptions options, string? urlOverride)
        {
            DisableConfigReloadOnChangeIfUnset();
            var builder = WebApplication.CreateBuilder(args);

            if (!string.IsNullOrWhiteSpace(options.ApplicationName))
            {
                builder.Environment.ApplicationName = options.ApplicationName;
            }

            if (!string.IsNullOrWhiteSpace(options.ContentRootPath))
            {
                builder.Environment.ContentRootPath = options.ContentRootPath;
                builder.Environment.ContentRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(options.ContentRootPath);
            }

            if (!string.IsNullOrWhiteSpace(options.WebRootPath))
            {
                builder.Environment.WebRootPath = options.WebRootPath;
                builder.Environment.WebRootFileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(options.WebRootPath);
            }

            if (!string.IsNullOrWhiteSpace(options.DatabasePath))
            {
                builder.Configuration["DatabasePath"] = options.DatabasePath;
            }

            if (!string.IsNullOrWhiteSpace(options.Urls))
            {
                builder.WebHost.UseUrls(options.Urls);
            }
            else if (!string.IsNullOrWhiteSpace(urlOverride))
            {
                builder.WebHost.UseUrls(urlOverride);
            }

            ConfigureServices(builder);

            var app = builder.Build();

            ConfigureMiddleware(app);

            return app;
        }

        private static void ConfigureServices(WebApplicationBuilder builder)
        {
            var apiAssembly = typeof(ApiHost).Assembly;

            builder.Services.AddControllers()
                .AddApplicationPart(apiAssembly);

            builder.Services.AddRazorPages()
                .AddApplicationPart(apiAssembly);

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new DateTimeNullableConverter());
            });

            builder.Services.AddSingleton<Database>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var dbPath = configuration.GetValue<string>("DatabasePath") ?? "tasks.db";
                return new Database(dbPath);
            });
            builder.Services.AddSingleton<ITaskService>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var dbPath = configuration.GetValue<string>("DatabasePath") ?? "tasks.db";
                return new TaskService(dbPath);
            });
            builder.Services.AddSingleton<IUid, Uid>();

            builder.Services.Configure<TelegramProviderOptions>(
                builder.Configuration.GetSection("Telegram"));
            builder.Services.PostConfigure<TelegramProviderOptions>(options =>
            {
                var hasRequiredCredentials =
                    !string.IsNullOrWhiteSpace(options.BotToken) &&
                    !string.IsNullOrWhiteSpace(options.ChatId);

                options.Enabled = hasRequiredCredentials;
            });
            builder.Services.AddHttpClient<ITelegramProvider, TelegramProvider>((sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramProviderOptions>>().Value;
                if (!string.IsNullOrWhiteSpace(options.BotToken))
                {
                    client.BaseAddress = new Uri($"https://api.telegram.org/bot{options.BotToken}/");
                }
            });
            builder.Services.AddSingleton<TelegramNotificationService>();
        }

        private static void ConfigureMiddleware(WebApplication app)
        {
            var database = app.Services.GetRequiredService<Database>();
            database.InitializeAsync().GetAwaiter().GetResult();

            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseCors("AllowAll");
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseAuthorization();
            app.MapControllers();
            app.MapRazorPages();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }
        }

        private static void DisableConfigReloadOnChangeIfUnset()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange")))
            {
                Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange")))
            {
                Environment.SetEnvironmentVariable("ASPNETCORE_hostBuilder__reloadConfigOnChange", "false");
            }
        }

        private static string? ResolveExplicitUrls(string[] args, ApiHostOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.Urls))
            {
                return options.Urls;
            }

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables(prefix: "ASPNETCORE_")
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var configuredUrls = configuration["urls"];
            if (!string.IsNullOrWhiteSpace(configuredUrls))
            {
                return configuredUrls;
            }

            return null;
        }

        private static ApiHostResult BuildResult(WebApplication app, string reason)
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addressesFeature = server.Features.Get<IServerAddressesFeature>();
            var address = addressesFeature?.Addresses?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(address))
            {
                return new ApiHostResult(0, "unknown", reason);
            }

            if (Uri.TryCreate(address, UriKind.Absolute, out var uri))
            {
                var normalized = uri.GetLeftPart(UriPartial.Authority);
                return new ApiHostResult(uri.Port, normalized, reason);
            }

            return new ApiHostResult(0, address, reason);
        }

        private static bool IsPortBindingFailure(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                if (current is SocketException socketException)
                {
                    return socketException.SocketErrorCode == SocketError.AddressAlreadyInUse
                        || socketException.SocketErrorCode == SocketError.AccessDenied;
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}
