namespace ATS.Dispatcher.Core;

/// <summary>A planned haul only; not an injected ATS contract.</summary>
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
}
