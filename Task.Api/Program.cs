using Task.Api;
using Scalar.AspNetCore;
using Task.Core;
using Task.Core.Providers.Telegram;

namespace Task.Api
{
    public class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

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

            var app = builder.Build();

            var database = app.Services.GetRequiredService<Database>();
            await database.InitializeAsync();

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

            app.Run();
        }
    }
}
