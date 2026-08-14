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
			DisableConfigReloadOnChangeIfUnset();
			var builder = WebApplication.CreateBuilder(args);
			ConfigureServices(builder);
			ConfigureServerUrls(builder);

			var app = builder.Build();
			ConfigureApp(app);

			var database = app.Services.GetRequiredService<Database>();
			await database.InitializeAsync(cancellationToken);

			await app.StartAsync(cancellationToken);
			LogServerAddress(app);
			await app.WaitForShutdownAsync(cancellationToken);
		}

		private static void ConfigureServices(WebApplicationBuilder builder)
		{
			ApiHost.ConfigureServices(builder);
		}

		private static void ConfigureApp(WebApplication app)
		{
			ApiHost.ConfigureMiddleware(app);
		}

		private static void ConfigureServerUrls(WebApplicationBuilder builder)
		{
			if (!string.IsNullOrWhiteSpace(builder.Configuration["urls"]) ||
				!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
			{
				return;
			}

			const string preferredUrl = "http://localhost:8080";
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
			if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Telegram__BotToken")))
			{
				Console.WriteLine("Telegram NOT loaded. Please set Telegram__BotToken and Telegram__ChatId environment variable.");
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
