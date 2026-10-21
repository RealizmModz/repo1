namespace ATS.Dispatcher.Core;

/// <summary>
/// A requested job, not yet an in-game Freight Market contract.
/// Cities, cargo and trailers must be resolved against the player's game data
/// before a future save editor can apply this draft.
/// </summary>
public sealed class DispatchJob
{
    public string Name { get; set; } = "Custom haul";
    public string OriginCity { get; set; } = "";
    public string DestinationCity { get; set; } = "";
    public string Cargo { get; set; } = "";
    public string Trailer { get; set; } = "";
    public decimal? PaymentUsd { get; set; }
    public List<string> Waypoints { get; set; } = [];
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
