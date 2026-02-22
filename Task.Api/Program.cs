using Task.Api;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace Task.Api
{
    public class Program
    {
        public static async System.Threading.Tasks.Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddRazorPages();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Configure CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            // Configure JSON serialization
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.Converters.Add(new DateTimeNullableConverter());
            });

            // Configure database service
            builder.Services.AddSingleton<Database>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                var dbPath = configuration.GetValue<string>("DatabasePath") ?? "tasks.db";
                return new Database(dbPath);
            });

            var app = builder.Build();

            // Initialize database
            var database = app.Services.GetRequiredService<Database>();
            await database.InitializeAsync();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseCors("AllowAll");

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseAuthorization();

            app.MapControllers();
            app.MapRazorPages();

            app.Run();
        }
    }
}
