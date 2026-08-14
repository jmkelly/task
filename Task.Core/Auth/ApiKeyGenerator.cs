using System;
using System.Security.Cryptography;
using System.Text;

namespace Task.Core.Auth;

/// <summary>
/// API key generation and hashing. Keys are "tk_" + base64url(32 CSPRNG bytes).
/// Only the SHA-256 hash of a key is ever stored; the plaintext is shown exactly once.
/// </summary>
public static class ApiKeyGenerator
{
    public const string Prefix = "tk_";

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsValidFormat(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || !apiKey.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = apiKey[Prefix.Length..];
        if (payload.Length < 32 || payload.Length > 64)
        {
            return false;
        }

        foreach (var c in payload)
        {
            var valid = (c >= 'a' && c <= 'z')
                || (c >= 'A' && c <= 'Z')
                || (c >= '0' && c <= '9')
                || c == '-'
                || c == '_';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
