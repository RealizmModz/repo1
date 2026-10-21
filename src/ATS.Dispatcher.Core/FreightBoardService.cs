namespace ATS.Dispatcher.Core;

/// <summary>
/// A UI-friendly reference to a saved offer or an explicitly fictional demo card.
/// An offer's original game-provided distance is NOT a route estimate for an
/// edited draft; prices are omitted because job_offer_data does not provide one.
/// </summary>
public sealed record FreightBoardCard(
    string Id,
    string Cargo,
    string Destination,
    string Trailer,
    string Detail,
    bool IsDemo,
    SaveOfferTemplate? Source)
{
    public string SourceLabel => IsDemo ? "DEMO • NOT IN GAME" : "EXISTING OFFER • READ ONLY";
    public string DisplayName => string.IsNullOrWhiteSpace(Cargo) ? "Unknown cargo" : Cargo;
    public string DisplayDestination => string.IsNullOrWhiteSpace(Destination)
        ? "Unknown destination" : Destination;
    public string DisplayTrailer => string.IsNullOrWhiteSpace(Trailer)
        ? "Trailer not specified" : Trailer;
}

public static class FreightBoardService
{
    public static IReadOnlyList<FreightBoardCard> Demonstration() =>
    [
        new("demo-machinery", "Heavy machinery", "Rapid City → Denver",
            "Lowboy", "Illustrative job • No live price or distance", true, null),
        new("demo-grain", "Grain", "Denver → Phoenix",
            "Hopper", "Illustrative job • No live price or distance", true, null),
        new("demo-steel", "Steel", "Las Vegas → Los Angeles",
            "Flatbed", "Illustrative job • No live price or distance", true, null)
    ];

    public static IReadOnlyList<FreightBoardCard> FromOffers(
        IEnumerable<SaveOfferTemplate> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Select((offer, index) => new FreightBoardCard(
            offer.UnitId + "-" + index,
            ShortToken(offer.CargoUnit, "cargo."),
            offer.Target,
            string.IsNullOrWhiteSpace(offer.TrailerVariant)
                ? offer.TrailerDefinition : offer.TrailerVariant,
            offer.ShortestDistanceKm is > 0
                ? "Original offer: " + offer.ShortestDistanceKm.Value + " km • not edited route"
                : "Original offer distance unavailable",
            false,
            offer)).ToArray();
    }

    public static IReadOnlyList<FreightBoardCard> Search(
        IEnumerable<FreightBoardCard> cards, string? filter)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (string.IsNullOrWhiteSpace(filter))
            return cards.ToArray();
        var value = filter.Trim();
        return cards.Where(x =>
            x.Cargo.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            x.Destination.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            x.Trailer.Contains(value, StringComparison.OrdinalIgnoreCase) ||
            x.Id.Contains(value, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static string ShortToken(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..] : value;

    public static string SchematicStops(IReadOnlyList<string> via)
    {
        ArgumentNullException.ThrowIfNull(via);
        if (via.Count == 0) return "DIRECT (schematic)";
        if (via.Count == 1) return "VIA " + via[0];
        return via.Count + " INTERMEDIATE STOPS";
    }
}
