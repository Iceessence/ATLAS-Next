using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AtlasNext;

internal static class Assets
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static readonly string Root = Path.Combine(AppContext.BaseDirectory, "assets");
    private static readonly Dictionary<string,string> Pins = LoadPins();

    private static Dictionary<string,string> LoadPins()
    {
        using var stream = typeof(Assets).Assembly.GetManifestResourceStream("AtlasNext.pins")
            ?? throw new InvalidDataException("Release asset manifest missing");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
    }

    public static IEnumerable<string> Records => Pins.Keys.Where(k => k.StartsWith("library/", StringComparison.Ordinal));
    public static (T value,string hash) Read<T>(string asset)
    {
        if (!Pins.TryGetValue(asset, out var expected)) throw new InvalidDataException("Unknown release asset");
        using var stream = File.OpenRead(Path.Combine(Root,asset));
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("Asset exceeds 1 MiB budget");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (digest != expected) throw new InvalidDataException($"Release digest mismatch for {asset}");
        return (JsonSerializer.Deserialize<T>(bytes, Json) ?? throw new InvalidDataException(asset),digest);
    }

    public static string[] Tokens(string value) => Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
        .Select(match => match.Value).ToArray();
}
