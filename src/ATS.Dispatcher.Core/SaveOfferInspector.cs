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
    GameSaveFormat Format);

/// <summary>
/// Parses only job_offer_data records from a separate, already decoded
/// plain-text save. No binary decoding, no save writes, no temp files.
/// Does not parse private player/profile information.
/// </summary>
public static class SaveOfferInspector
{
    private const long MaxBytes = 256L * 1024 * 1024;
    private const int MaxOffers = 25000;
    private static readonly Regex OfferStart = new(
        @"^\s*job_offer_data\s*:\s*(?<id>[^\s{]+)\s*\{",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Property = new(
        @"^\s*(?<key>[a-zA-Z_][a-zA-Z_0-9]*)\s*:\s*(?:""(?<q>[^""]*)""|(?<bare>[^\s#]+))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static SaveOfferInspection Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var format = GameProfileScanner.DetectFormat(fullPath);
        if (format != GameSaveFormat.PlainTextSii)
            throw new InvalidDataException("This is not a plain-text SiiNunit save. " +
                "Decode a COPY using a trusted SII decoder and select that copy. Never decode your live save in place.");

        using var input = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaxBytes)
            throw new InvalidDataException("Decoded save is larger than the 256 MiB inspection limit.");

        using var reader = new StreamReader(input, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: true);
        var offers = new List<SaveOfferTemplate>();
        int count = 0;
        string? id = null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inside = false;

        // Parsing is bounded and only tracks the 9 keys used for preview.
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 1024 * 1024)
                throw new InvalidDataException("Save contains an unexpectedly long line.");
            var trimmed = line.Trim();
            if (!inside)
            {
                var match = OfferStart.Match(line);
                if (!match.Success)
                    continue;
                id = match.Groups["id"].Value;
                fields.Clear();
                inside = true;
                continue;
            }
            if (trimmed.StartsWith("}", StringComparison.Ordinal))
            {
                count++;
                if (offers.Count < MaxOffers)
                {
                    string Get(string key) => fields.GetValueOrDefault(key, "");
                    int? ParseInt(string key) =>
                        int.TryParse(Get(key), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var number) ? number : null;
                    long? ParseLong(string key) =>
                        long.TryParse(Get(key), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out var number) ? number : null;
                    if (!string.IsNullOrWhiteSpace(Get("cargo")) &&
                        !string.IsNullOrWhiteSpace(Get("target")))
                    {
                        offers.Add(new SaveOfferTemplate(
                            id!, Get("target"), Get("cargo"),
                            Get("trailer_variant"), Get("trailer_definition"),
                            Get("company_truck"), ParseInt("units_count"),
                            ParseInt("shortest_distance_km"), ParseLong("expiration_time")));
                    }
                }
                inside = false;
                id = null;
                fields.Clear();
                continue;
            }
            var property = Property.Match(line);
            if (!property.Success)
                continue;
            var key = property.Groups["key"].Value;
            switch (key)
            {
                case "target":
                case "cargo":
                case "trailer_variant":
                case "trailer_definition":
                case "company_truck":
                case "units_count":
                case "shortest_distance_km":
                case "expiration_time":
                    fields[key] = property.Groups["q"].Success
                        ? property.Groups["q"].Value : property.Groups["bare"].Value;
                    break;
            }
        }

        // Explicitly reject truncated unit blocks, rather than treating partial
        // observations as a complete save snapshot.
        if (inside)
            throw new InvalidDataException("Decoded save ends within a job_offer_data block.");

        return new SaveOfferInspection(count, offers, count > MaxOffers, format);
    }
}
