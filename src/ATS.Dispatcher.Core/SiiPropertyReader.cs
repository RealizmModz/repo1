using System.Text.RegularExpressions;

namespace ATS.Dispatcher.Core;

/// <summary>
/// Small read-only property reader, NOT a complete SII interpreter.
/// Includes, inheritance, overrides and localization aren't resolved.
/// </summary>
internal static class SiiPropertyReader
{
    private static readonly Regex Pattern = new(
        @"^\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)(?:\[[0-9]*\])?\s*:\s*(?:""(?<q>[^""]*)""|(?<b>[^\s#]+))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, List<string>> Read(string file)
    {
        if (new FileInfo(file).Length > 2 * 1024 * 1024)
            throw new InvalidDataException("Definition is too large: " + file);
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(file))
        {
            var match = Pattern.Match(line);
            if (!match.Success) continue;
            var key = match.Groups["key"].Value;
            var value = match.Groups["q"].Success
                ? match.Groups["q"].Value : match.Groups["b"].Value;
            if (!result.TryGetValue(key, out var values))
                result[key] = values = [];
            values.Add(value);
        }
        return result;
    }

    public static string First(IReadOnlyDictionary<string, List<string>> data,
        string key, string fallback = "") =>
        data.TryGetValue(key, out var values) && values.Count > 0 ? values[0] : fallback;

    public static IReadOnlyList<string> All(
        IReadOnlyDictionary<string, List<string>> data, string key) =>
        data.TryGetValue(key, out var values) ? values : [];
}
