namespace ATS.Dispatcher.Core;

/// <summary>
/// Read-only comparison of offer records from two separately decoded save
/// snapshots. A changed record is not proof a player action caused the change:
/// offers can expire or regenerate as the economy advances.
/// </summary>
public enum OfferChangeKind { Added, Removed, Modified }

public sealed record OfferChange(OfferChangeKind Kind, string UnitId,
    SaveOfferTemplate? Before, SaveOfferTemplate? After);

public sealed record OfferSnapshotDiff(int BeforeCount, int AfterCount,
    IReadOnlyList<OfferChange> Changes)
{
    public int Added => Changes.Count(x => x.Kind == OfferChangeKind.Added);
    public int Removed => Changes.Count(x => x.Kind == OfferChangeKind.Removed);
    public int Modified => Changes.Count(x => x.Kind == OfferChangeKind.Modified);
}

public static class OfferSnapshotDiffService
{
    public static OfferSnapshotDiff Compare(
        SaveOfferInspection before, SaveOfferInspection after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.ResultsTruncated || after.ResultsTruncated)
            throw new InvalidDataException("Cannot compare truncated offer snapshots.");
        if (before.Format != GameSaveFormat.PlainTextSii ||
            after.Format != GameSaveFormat.PlainTextSii)
            throw new InvalidDataException("Both snapshots must be decoded text SII.");

        static Dictionary<string, SaveOfferTemplate> Index(
            IReadOnlyList<SaveOfferTemplate> offers)
        {
            var map = new Dictionary<string, SaveOfferTemplate>(StringComparer.Ordinal);
            foreach (var offer in offers)
            {
                if (string.IsNullOrWhiteSpace(offer.UnitId) ||
                    !map.TryAdd(offer.UnitId, offer))
                    throw new InvalidDataException(
                        "Snapshot contains missing or duplicate offer unit identifiers.");
            }
            return map;
        }

        var oldById = Index(before.Offers);
        var newById = Index(after.Offers);
        var changes = new List<OfferChange>();
        foreach (var (unitId, original) in oldById)
        {
            if (!newById.TryGetValue(unitId, out var current))
                changes.Add(new OfferChange(OfferChangeKind.Removed, unitId, original, null));
            else if (original != current)
                changes.Add(new OfferChange(OfferChangeKind.Modified, unitId, original, current));
        }
        foreach (var (unitId, current) in newById)
        {
            if (!oldById.ContainsKey(unitId))
                changes.Add(new OfferChange(OfferChangeKind.Added, unitId, null, current));
        }

        return new OfferSnapshotDiff(before.Offers.Count, after.Offers.Count,
            changes.OrderBy(x => x.UnitId, StringComparer.Ordinal).ToArray());
    }
}
