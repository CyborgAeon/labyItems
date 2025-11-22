using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

public static class JsonTokenCompressor
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false, // keep it tight
    };

    // ------------- ENCODE -------------

    // From a .NET object → compressed Base64 string
    public static string ToCompressedBase64<T>(T value, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(value, options ?? DefaultJsonOptions);
        return CompressToBase64(json);
    }

    // From an existing JSON string → compressed Base64 string
    public static string CompressToBase64(string json)
    {
        var inputBytes = Encoding.UTF8.GetBytes(json);

        using var input = new MemoryStream(inputBytes);
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            input.CopyTo(gzip);
        }

        var compressedBytes = output.ToArray();
        return Convert.ToBase64String(compressedBytes);
    }

    // Optional: URL-safe variant (no + / =)
    public static string ToUrlSafe(string base64)
    {
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    // ------------- DECODE -------------

    // Back to a .NET object from compressed Base64
    public static T FromCompressedBase64<T>(string base64, JsonSerializerOptions? options = null)
    {
        var json = DecompressFromBase64(base64);
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultJsonOptions)
            ?? throw new InvalidOperationException("Deserialized object was null.");
    }

    // Back to plain JSON string from compressed Base64
    public static string DecompressFromBase64(string base64)
    {
        var compressedBytes = Convert.FromBase64String(base64);

        using var input = new MemoryStream(compressedBytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);

        return Encoding.UTF8.GetString(output.ToArray());
    }

    // Optional: URL-safe → normal Base64
    public static string FromUrlSafe(string urlSafeBase64)
    {
        var padded = urlSafeBase64.Replace("-", "+").Replace("_", "/");

        // Pad back to length % 4 == 0
        var mod4 = padded.Length % 4;
        if (mod4 != 0)
        {
            padded = padded.PadRight(padded.Length + (4 - mod4), '=');
        }

        return padded;
    }
}
