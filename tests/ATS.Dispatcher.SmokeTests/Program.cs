using ATS.Dispatcher.Core;

var validJob = new DispatchJob
{
    Name = "Black Hills to California",
    OriginCity = "Rapid City",
    DestinationCity = "Los Angeles",
    Cargo = "Machinery",
    Trailer = "Lowboy",
    PaymentUsd = 3500,
    Waypoints = ["Cheyenne", "Las Vegas"]
};

Assert(DispatchValidator.Validate(validJob).Count == 0, "Valid job rejected");
Assert(DispatchValidator.Validate(new DispatchJob()).Count >= 4, "Incomplete job accepted");
Assert(DispatchValidator.Validate(new DispatchJob
{
    OriginCity = "A",
    DestinationCity = "a",
    Cargo = "Steel",
    Trailer = "Flatbed",
    PaymentUsd = -2
}).Count >= 2, "Invalid route/payout accepted");

var root = Path.Combine(Path.GetTempPath(), "ats-dispatcher-smoketest-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(root);
    var exported = Path.Combine(root, "my-job.json");
    validJob.OriginCompany = "Shipper";
    validJob.DestinationCompany = "Receiver";
    JobFileService.Export(exported, validJob);
    var imported = JobFileService.Import(exported);
    Assert(imported.DestinationCity == "Los Angeles", "JSON round-trip lost destination");
    Assert(imported.Waypoints.SequenceEqual(validJob.Waypoints), "JSON round-trip lost waypoints");
    Assert(imported.PaymentUsd == 3500, "JSON round-trip lost payout");
    Assert(imported.OriginCompany == "Shipper" && imported.DestinationCompany == "Receiver",
        "JSON round-trip lost pickup/delivery companies");

    var saveDir = Path.Combine(root, "American Truck Simulator", "profiles", "123ABC",
        "save", "autosave");
    Directory.CreateDirectory(saveDir);
    var game = Path.Combine(saveDir, "game.sii");
    File.WriteAllBytes(game, "SiiN"u8.ToArray());
    Assert(GameProfileScanner.DetectFormat(game) == GameSaveFormat.PlainTextSii,
        "SiiN signature not detected");
    Assert(GameProfileScanner.Scan(root).Count == 1, "Save slot discovery failed");
    File.WriteAllBytes(game, "ScsC"u8.ToArray());
    Assert(GameProfileScanner.DetectFormat(game) == GameSaveFormat.CompressedScs,
        "ScsC signature not detected");
    File.WriteAllBytes(game, "BSII"u8.ToArray());
    Assert(GameProfileScanner.DetectFormat(game) == GameSaveFormat.BinarySii,
        "BSII signature not detected");
    File.WriteAllBytes(game, [0x00, 0x01, 0x02, 0x03]);
    Assert(GameProfileScanner.DetectFormat(game) == GameSaveFormat.Unknown,
        "Unknown signature incorrectly accepted");
}
finally
{
    if (Directory.Exists(root))
        Directory.Delete(root, recursive: true);
}


var dataRoot = Path.Combine(Path.GetTempPath(), "ats-catalog-" + Guid.NewGuid().ToString("N"));
try
{
    var defs = Path.Combine(dataRoot, "def");
    void CreateDef(string path, string text)
    {
        var target = Path.Combine(defs, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, text);
    }

    CreateDef("city/rapid_city.sii", "city_name: \"Rapid City\"");
    CreateDef("city/los_angeles.sii", "city_name: \"Los Angeles\"");
    CreateDef("city/cheyenne.sii", "city_name: \"Cheyenne\"");
    CreateDef("cargo/steel.sii", "name: \"Steel\"\nbody_types[]: flatbed");
    CreateDef("cargo/steel/flatbed_a.sii", "body_type: flatbed\ntrailer: \"trailer.flatbed_a\"");
    CreateDef("company/shipper.sii", "name: \"Shipper\"");
    CreateDef("company/shipper/editor/rapid_city.sii", "city: rapid_city");
    CreateDef("company/shipper/out/steel.sii", "cargo: \"cargo.steel\"");
    CreateDef("company/receiver.sii", "name: \"Receiver\"");
    CreateDef("company/receiver/editor/los_angeles.sii", "city: los_angeles");
    CreateDef("company/receiver/in/steel.sii", "cargo: \"cargo.steel\"");

    var catalog = GameCatalogImporter.Import(dataRoot);
    Assert(catalog.Cities.Count == 3, "Catalog city count");
    Assert(catalog.Cargo.Count == 1, "Catalog cargo count");
    Assert(catalog.Depots.Count == 2, "Catalog depot count");
    Assert(catalog.Trailers.Count == 1, "Catalog trailer count");
    Assert(GameCatalogImporter.Import(defs).Depots.Count == 2,
        "Import should allow selecting /def directly");

    var haul = new DispatchJob
    {
        OriginCity = "Rapid City", OriginCompany = "Shipper",
        DestinationCity = "Los Angeles", DestinationCompany = "Receiver",
        Cargo = "Steel", Trailer = "flatbed_a",
        Waypoints = ["Cheyenne"]
    };
    Assert(CatalogCompatibilityValidator.Check(haul, catalog).Count == 0,
        "Valid imported company, cargo and trailer combo rejected");

    haul.DestinationCompany = "Shipper";
    Assert(CatalogCompatibilityValidator.Check(haul, catalog)
        .Any(x => x.Contains("Destination", StringComparison.OrdinalIgnoreCase)),
        "Wrong destination depot was accepted");
    haul.DestinationCompany = "Receiver";

    haul.Trailer = "reefer";
    Assert(CatalogCompatibilityValidator.Check(haul, catalog)
        .Any(x => x.Contains("Trailer", StringComparison.OrdinalIgnoreCase)),
        "Incompatible trailer was accepted");
    haul.Trailer = "flatbed";

    haul.Waypoints = ["Not A City"];
    Assert(CatalogCompatibilityValidator.Check(haul, catalog)
        .Any(x => x.Contains("waypoint", StringComparison.OrdinalIgnoreCase)),
        "Unknown waypoint was accepted");
}
finally
{
    if (Directory.Exists(dataRoot))
        Directory.Delete(dataRoot, recursive: true);
}

Console.WriteLine("All ATS Dispatcher smoke tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
