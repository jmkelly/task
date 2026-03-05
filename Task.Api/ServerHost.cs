using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System.Net;
using System.Net.Sockets;
using Task.Core;
using Task.Core.Providers.Telegram;

namespace Task.Api
{
	public static class ServerHost
	{
		public static async System.Threading.Tasks.Task RunAsync(string[] args, CancellationToken cancellationToken = default)
		{
			var builder = WebApplication.CreateBuilder(args);
			ConfigureServices(builder);
			ConfigureServerUrls(builder);

			var app = builder.Build();
			ConfigureApp(app);

			var database = app.Services.GetRequiredService<Database>();
			await database.InitializeAsync();

			await app.StartAsync(cancellationToken);
			LogServerAddress(app);
			await app.WaitForShutdownAsync(cancellationToken);
		}

		private static void ConfigureServices(WebApplicationBuilder builder)
		{
			builder.Services.AddControllers();
			builder.Services.AddRazorPages();
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

			builder.Services.Configure<TelegramProviderOptions>(
				builder.Configuration.GetSection("Telegram"));
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

		private static void ConfigureApp(WebApplication app)
		{
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

		private static void ConfigureServerUrls(WebApplicationBuilder builder)
		{
			if (!string.IsNullOrWhiteSpace(builder.Configuration["urls"]) ||
				!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
			{
				return;
			}

			var preferredUrl = "http://localhost:8080";
			if (IsPortAvailable(IPAddress.Loopback, 8080))
			{
				builder.WebHost.UseUrls(preferredUrl);
				return;
			}

			Console.Error.WriteLine("Server.PortUnavailable port=8080 reason=address-in-use");
			builder.WebHost.ConfigureKestrel(options =>
			{
				options.Listen(IPAddress.Loopback, 0);
			});
		}

		private static void LogServerAddress(WebApplication app)
		{
			var server = app.Services.GetRequiredService<IServer>();
			var addressFeature = server.Features.Get<IServerAddressesFeature>();
			var address = addressFeature?.Addresses.FirstOrDefault();

			if (string.IsNullOrWhiteSpace(address))
			{
				Console.WriteLine("Server.Started port=0 url=unknown reason=unknown");
				return;
			}

			var uri = new Uri(address);
			var reason = uri.Port == 8080 ? "preferred" : "auto-assigned";
			Console.WriteLine($"Server.Started port={uri.Port} url={address} reason={reason}");
		}

		private static bool IsPortAvailable(IPAddress address, int port)
		{
			try
			{
				using var listener = new TcpListener(address, port);
				listener.Start();
				return true;
			}
			catch (SocketException)
			{
				return false;
			}
		}
	}
}
