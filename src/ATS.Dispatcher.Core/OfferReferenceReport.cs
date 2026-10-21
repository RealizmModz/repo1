namespace ATS.Dispatcher.Core;

/// <summary>
/// Exact-token observations only. A missing header may be an external
/// definition or unsupported syntax; it is never declared an invalid reference.
/// Even an observed header does not prove type/semantic or dispatch compatibility.
/// </summary>
public enum OfferReferenceState { ObservedUnitHeader, Unverified }

public sealed record OfferReferenceObservation(
    string OfferUnitId, string Field, string Reference,
    OfferReferenceState State, string? ObservedUnitType);

public static class OfferReferenceReport
{
    public static IReadOnlyList<OfferReferenceObservation> Build(SaveOfferInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);
        var result = new List<OfferReferenceObservation>();
        foreach (var offer in inspection.Offers)
        {
            Add(offer, "target", offer.Target);
            Add(offer, "cargo", offer.CargoUnit);
            Add(offer, "trailer_variant", offer.TrailerVariant);
            Add(offer, "trailer_definition", offer.TrailerDefinition);
            Add(offer, "company_truck", offer.CompanyTruck);
        }
        return result.AsReadOnly();

        void Add(SaveOfferTemplate offer, string field, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return;
            var found = inspection.ObservedUnitTypes.TryGetValue(token, out var type);
            result.Add(new OfferReferenceObservation(offer.UnitId, field, token,
                found ? OfferReferenceState.ObservedUnitHeader : OfferReferenceState.Unverified,
                found ? type : null));
        }
    }

    public static string Summary(SaveOfferInspection inspection)
    {
        var edges = Build(inspection);
        var observed = edges.Count(x => x.State == OfferReferenceState.ObservedUnitHeader);
        var lines = new List<string>
        {
            "READ-ONLY OFFER REFERENCE OBSERVATIONS",
            "Copied source SHA-256: " + inspection.SourceSha256,
            "Top-level headers indexed: " + inspection.ObservedUnitTypes.Count,
            "Header syntax coverage: " + (inspection.TopLevelUnitIndexComplete
                ? "all observed top-level openings recognized" : "partial / unsupported headers present"),
            "Offer records scanned: " + inspection.OfferCount,
            "Offers missing cargo/target: " + inspection.MissingRequiredFieldsCount,
            "Offer list capped: " + (inspection.ResultsTruncated ? "yes" : "no"),
            "References in retained usable offers: " + edges.Count,
            "Exact headers observed: " + observed + "; unverified: " + (edges.Count - observed),
            "",
            "Unverified can mean external definitions, absent units or unsupported syntax.",
            "Observed headers do not prove valid depots, cargo, trailers or dispatch support.",
            "ATS version and active DLC/mod compatibility: not established.",
            ""
        };
        lines.AddRange(edges.Take(12).Select(x =>
            x.Field + ": " + x.Reference + " — " +
            (x.State == OfferReferenceState.ObservedUnitHeader
                ? "header observed (" + x.ObservedUnitType + ")" : "UNVERIFIED")));
        if (edges.Count > 12) lines.Add("(Additional observations omitted from this preview.)");
        lines.Add("\nNo save writes, job injection or GPS changes were performed.");
        return string.Join(Environment.NewLine, lines);
    }
}
