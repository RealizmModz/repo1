using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ATS.Dispatcher.Core;

/// <summary>
/// A job_offer_data record read from a *decoded* SiiNunit save.
/// This is a reference for planning only; it is not a new ATS freight offer.
/// </summary>
public sealed record SaveOfferTemplate(
    string UnitId,
    string Target,
    string CargoUnit,
    string TrailerVariant,
    string TrailerDefinition,
    string CompanyTruck,
    int? UnitsCount,
    int? ShortestDistanceKm,
    long? ExpirationTime)
{
    public string Display =>
        $"{CargoUnit}  →  {Target}  [{TrailerVariant}]";
}

public sealed record SaveOfferInspection(
    int OfferCount,
    IReadOnlyList<SaveOfferTemplate> Offers,
    bool ResultsTruncated,
    GameSaveFormat Format)
{
    // Hash of the exact bytes read from the same locked, read-only stream.
    public string SourceSha256 { get; init; } = "";
    public int MissingRequiredFieldsCount { get; init; }
    public IReadOnlyDictionary<string, string> ObservedUnitTypes { get; init; } =
        new Dictionary<string, string>();
    public bool TopLevelUnitIndexComplete { get; init; }
}

/// <summary>
/// Bounded read-only inspection of separately decoded UTF-8 SiiNunit copies.
/// Validates the outer envelope and offer structure, not the full SII graph.
/// Unknown unit contents and external references are not interpreted.
/// </summary>
public static class SaveOfferInspector
{
    private const long MaxBytes = 256L * 1024 * 1024;
    private const int MaxOffers = 25000;
    private const int MaxIndexedUnits = 250000;
    private static readonly Regex UnitStart = new(
        @"^\s*(?<type>[A-Za-z_][A-Za-z_0-9]*)\s*:\s*(?<id>[^\s{}""]+)\s*\{\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex OfferStart = new(
        @"^\s*job_offer_data\s*:\s*(?<id>[^\s{}]+)\s*\{\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Property = new(
        @"^\s*(?<key>[a-zA-Z_][a-zA-Z_0-9]*)\s*:\s*(?:""(?<q>(?:\\.|[^""\\])*)""|(?<bare>[^\s{}""]+))\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ObservedKeys = new(StringComparer.Ordinal)
    {
        "target", "cargo", "trailer_variant", "trailer_definition",
        "company_truck", "units_count", "shortest_distance_km", "expiration_time"
    };

    public static SaveOfferInspection Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaxBytes)
            throw new InvalidDataException("Decoded save is larger than the 256 MiB inspection limit.");

        // Do not enable BOM autodetection: it could silently switch to UTF-16.
        // The exact header below also rejects encoded/binary saves.
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        input.Position = 0;
        using var reader = new StreamReader(input, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false);
        var offers = new List<SaveOfferTemplate>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var unitTypes = new Dictionary<string, string>(StringComparer.Ordinal);
        bool indexComplete = true;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        int count = 0, incomplete = 0, depth = 0, phase = 0;
        string? id = null;
        bool blockComment = false;

        try
        {
            while (reader.ReadLine() is { } line)
            {
                if (line.Length > 1024 * 1024)
                    throw new InvalidDataException("Save contains an unexpectedly long line.");
                var text = WithoutComments(line, ref blockComment);
                var trimmed = text.Trim();
                if (trimmed.Length == 0) continue;

                if (phase == 0)
                {
                    if (trimmed != "SiiNunit")
                        throw new InvalidDataException("Select a separately decoded UTF-8 SiiNunit copy; encoded saves are not supported.");
                    phase = 1;
                    continue;
                }
                if (phase == 1)
                {
                    if (trimmed != "{")
                        throw new InvalidDataException("Missing SiiNunit opening envelope.");
                    phase = 2;
                    depth = 1;
                    continue;
                }
                if (phase == 3)
                    throw new InvalidDataException("Unexpected data after the SiiNunit envelope.");

                var (opens, closes) = BracesOutsideStrings(text);
                if (depth == 1 && opens != 0)
                {
                    var unit = UnitStart.Match(text);
                    if (!unit.Success) indexComplete = false;
                    else
                    {
                        if (!unitTypes.TryAdd(unit.Groups["id"].Value, unit.Groups["type"].Value))
                            throw new InvalidDataException("Duplicate top-level SII unit identifier.");
                        if (unitTypes.Count > MaxIndexedUnits)
                            throw new InvalidDataException("Decoded save exceeds the 250,000 unit index limit.");
                    }
                }
                if (id is not null)
                {
                    if (trimmed == "}")
                    {
                        count++;
                        string Get(string key) => fields.GetValueOrDefault(key, "");
                        int? ParseInt(string key)
                        {
                            if (!fields.ContainsKey(key)) return null;
                            if (!int.TryParse(Get(key), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out var value))
                                throw new InvalidDataException("Invalid integer in offer field " + key + ".");
                            return value;
                        }
                        long? ParseLong(string key)
                        {
                            if (!fields.ContainsKey(key)) return null;
                            if (!long.TryParse(Get(key), NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out var value))
                                throw new InvalidDataException("Invalid integer in offer field " + key + ".");
                            return value;
                        }
                        // Validate observed numeric fields even on incomplete/capped offers.
                        var units = ParseInt("units_count");
                        var distance = ParseInt("shortest_distance_km");
                        var expiry = ParseLong("expiration_time");
                        if (string.IsNullOrWhiteSpace(Get("cargo")) ||
                            string.IsNullOrWhiteSpace(Get("target")))
                            incomplete++;
                        else if (count <= MaxOffers)
                            offers.Add(new SaveOfferTemplate(id, Get("target"), Get("cargo"),
                                Get("trailer_variant"), Get("trailer_definition"),
                                Get("company_truck"), units, distance, expiry));
                        id = null;
                        fields.Clear();
                    }
                    else
                    {
                        if (opens != 0 || closes != 0)
                            throw new InvalidDataException("Unexpected nested or malformed offer braces.");
                        var property = Property.Match(text);
                        if (!property.Success)
                            throw new InvalidDataException("Unsupported or malformed offer property.");
                        var key = property.Groups["key"].Value;
                        if (ObservedKeys.Contains(key))
                        {
                            var value = property.Groups["q"].Success
                                ? property.Groups["q"].Value : property.Groups["bare"].Value;
                            if (fields.TryGetValue(key, out var previous) && previous != value)
                                throw new InvalidDataException("Conflicting values in offer field " + key + ".");
                            fields[key] = value;
                        }
                    }
                }
                else
                {
                    var match = OfferStart.Match(text);
                    if (match.Success)
                    {
                        if (depth != 1)
                            throw new InvalidDataException("Offer unit is not at the top level.");
                        id = match.Groups["id"].Value;
                        if (!ids.Add(id))
                            throw new InvalidDataException("Duplicate job_offer_data unit identifier.");
                    }
                    else if (trimmed.StartsWith("job_offer_data", StringComparison.Ordinal))
                        throw new InvalidDataException("Malformed or unsupported job_offer_data header.");
                }

                // Reject braces in an impossible order, including in unrelated units.
                foreach (var c in StructuralBraces(text))
                {
                    depth += c == '{' ? 1 : -1;
                    if (depth < 0 || (depth == 0 && trimmed != "}"))
                        throw new InvalidDataException("Malformed SiiNunit braces.");
                }
                if (depth == 0) phase = 3;
            }
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("Decoded save is not valid UTF-8.", ex);
        }

        if (phase != 3 || depth != 0 || id is not null || blockComment)
            throw new InvalidDataException("Decoded save is truncated or has an unterminated envelope, unit or comment.");

        return new SaveOfferInspection(count, offers.AsReadOnly(), count > MaxOffers,
            GameSaveFormat.PlainTextSii)
        {
            SourceSha256 = hash,
            MissingRequiredFieldsCount = incomplete,
            ObservedUnitTypes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(unitTypes),
            TopLevelUnitIndexComplete = indexComplete
        };
    }

    // Preserve quoted strings verbatim and ignore comment markers inside them.
    private static string WithoutComments(string line, ref bool inBlock)
    {
        var result = new StringBuilder(line.Length);
        bool quoted = false, escaped = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            var next = i + 1 < line.Length ? line[i + 1] : '\0';
            if (inBlock)
            {
                if (c == '*' && next == '/') { inBlock = false; i++; result.Append(' '); }
                continue;
            }
            if (quoted)
            {
                result.Append(c);
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
            }
            else if (c == '"') { quoted = true; result.Append(c); }
            else if (c == '#' || (c == '/' && next == '/')) break;
            else if (c == '/' && next == '*') { inBlock = true; i++; result.Append(' '); }
            else result.Append(c);
        }
        if (quoted)
            throw new InvalidDataException("Unterminated quoted string in decoded save.");
        return result.ToString();
    }

    private static IEnumerable<char> StructuralBraces(string text)
    {
        bool quoted = false, escaped = false;
        foreach (var c in text)
        {
            if (quoted)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') quoted = false;
            }
            else if (c == '"') quoted = true;
            else if (c is '{' or '}') yield return c;
        }
    }

    private static (int Opens, int Closes) BracesOutsideStrings(string text)
    {
        int opens = 0, closes = 0;
        foreach (var c in StructuralBraces(text))
            if (c == '{') opens++; else closes++;
        return (opens, closes);
    }
}
