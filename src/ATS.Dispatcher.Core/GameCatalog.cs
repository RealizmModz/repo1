namespace ATS.Dispatcher.Core;

/// <summary>Extracted definitions only: not proof of enabled DLC/mods on the active profile.</summary>
public sealed class GameCatalog
{
    public string SourceDirectory { get; init; } = "";
    public int DefinitionFilesRead { get; init; }
    public IReadOnlyList<CatalogCity> Cities { get; init; } = [];
    public IReadOnlyList<CatalogCargo> Cargo { get; init; } = [];
    public IReadOnlyList<CatalogDepot> Depots { get; init; } = [];
    public IReadOnlyList<CatalogTrailer> Trailers { get; init; } = [];
}
public sealed record CatalogCity(string Id, string Name);
public sealed record CatalogCargo(string Id, string Name, IReadOnlyList<string> BodyTypes);
public sealed record CatalogDepot(string CompanyId, string CompanyName, string CityId,
    IReadOnlyList<string> OutgoingCargo, IReadOnlyList<string> IncomingCargo);
public sealed record CatalogTrailer(string CargoId, string Id, string BodyType, string TrailerUnit);
