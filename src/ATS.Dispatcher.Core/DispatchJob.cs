namespace ATS.Dispatcher.Core;

/// <summary>
/// A planned haul only; not an injected ATS contract.
/// Reference fields preserve known working game-generated offer identifiers.
/// </summary>
public sealed class DispatchJob
{
    public string Name { get; set; } = "Custom haul";
    public string OriginCity { get; set; } = "";
    public string OriginCompany { get; set; } = "";
    public string DestinationCity { get; set; } = "";
    public string DestinationCompany { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Trailer { get; set; } = "";
    public decimal? PaymentUsd { get; set; }
    public List<string> Waypoints { get; set; } = [];
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    // Optional planning metadata copied from a decoded game's job_offer_data.
    // These values are not written to game saves by this application.
    public string ReferenceOfferUnit { get; set; } = "";
    public string ReferenceTarget { get; set; } = "";
    public string ReferenceCargoUnit { get; set; } = "";
    public string ReferenceTrailerVariant { get; set; } = "";
    public string ReferenceTrailerDefinition { get; set; } = "";
    public string ReferenceCompanyTruck { get; set; } = "";
    public int? ReferenceUnitsCount { get; set; }
}
