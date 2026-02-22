using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

namespace Task.Cli
{
    public class Config
    {
        private static readonly string ConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".task");
        private static readonly string ConfigFile = Path.Combine(ConfigDir, "config.json");

        public string? ApiUrl { get; set; }
        public string? DefaultOutput { get; set; }

        public static Config Load()
        {
            try
            {
                if (!File.Exists(ConfigFile))
                {
                    return new Config { DefaultOutput = "plain" };
                }

                var json = File.ReadAllText(ConfigFile);
                var config = JsonSerializer.Deserialize(json, TaskJsonContext.Default.Config) ?? new Config();

                // Set defaults for missing values
                if (string.IsNullOrEmpty(config.DefaultOutput))
                {
                    config.DefaultOutput = "plain";
                }

                // Validate API URL format if set
                if (!string.IsNullOrEmpty(config.ApiUrl) && !IsValidUrlFormat(config.ApiUrl))
                {
                    throw new ArgumentException("Invalid API URL format in config. Must be a valid HTTP or HTTPS URL.");
                }

                return config;
            }
            catch (Exception)
            {
                // If file is corrupted, return defaults
                return new Config { DefaultOutput = "plain" };
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(this, TaskJsonContext.Default.Config);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save config: {ex.Message}", ex);
            }
        }

        public async System.Threading.Tasks.Task SetValueAsync(string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "api-url":
                    await ValidateUrlAsync(value);
                    ApiUrl = value;
                    break;
                case "defaultoutput":
                    if (value != "json" && value != "plain")
                    {
                        throw new ArgumentException("defaultOutput must be 'json' or 'plain'");
                    }
                    DefaultOutput = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown config key: {key}");
            }
        }

        public static async System.Threading.Tasks.Task ValidateUrlAsync(string url)
        {
            if (!IsValidUrlFormat(url))
            {
                throw new ArgumentException("Invalid URL format. Must be a valid HTTP or HTTPS URL.");
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            try
            {
                using var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));
                // We just check if the request succeeds, no need to check status code as HEAD might not be supported
            }
            catch (HttpRequestException ex)
            {
                throw new ArgumentException($"URL is not reachable: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                throw new ArgumentException("URL validation timed out after 5 seconds.");
            }
        }

        private static bool IsValidUrlFormat(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public string? GetValue(string key)
        {
            return key.ToLowerInvariant() switch
            {
                "api-url" => ApiUrl,
                "defaultoutput" => DefaultOutput,
                _ => null
            };
        }

        public void UnsetValue(string key)
        {
            switch (key.ToLowerInvariant())
            {
                case "api-url":
                    ApiUrl = null;
                    break;
                case "defaultoutput":
                    DefaultOutput = "plain"; // Reset to default
                    break;
            }
        }
    }
}