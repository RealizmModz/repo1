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
    var sourceBytes = File.ReadAllBytes(game);
    var verifiedBackup = SaveBackupService.CreateVerifiedBackup(
        game, Path.Combine(root, "independent-backups"));
    Assert(File.Exists(verifiedBackup.BackupFile), "Backup was not created");
    Assert(verifiedBackup.Bytes == sourceBytes.Length, "Backup byte count mismatch");
    Assert(File.ReadAllBytes(verifiedBackup.BackupFile).SequenceEqual(sourceBytes),
        "Backup changed the bytes of the ATS save");
    Assert(File.ReadAllBytes(game).SequenceEqual(sourceBytes),
        "Backup service unexpectedly changed the source save");
    Assert(verifiedBackup.Sha256.Length == 64, "Missing SHA-256 checksum");

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

    var modFile = Path.Combine(dataRoot, "custom-test.scs");
    var mod = CargoFlowModBuilder.Create(catalog, haul, modFile);
    Assert(File.Exists(mod.PackagePath), "Generated cargo mod is missing");
    Assert(mod.OriginCompanyId == "shipper" &&
        mod.DestinationCompanyId == "receiver" && mod.CargoId == "steel",
        "Generated cargo mod uses incorrect company/cargo IDs");
    using (var zip = System.IO.Compression.ZipFile.OpenRead(modFile))
    {
        var expectedPaths = new[]
        {
            "manifest.sii", "description.txt",
            "def/company/shipper/out/steel.sii",
            "def/company/receiver/in/steel.sii"
        };
        Assert(expectedPaths.All(name => zip.GetEntry(name) is not null),
            "Generated cargo mod missing required paths");
        using var reader = new StreamReader(zip.GetEntry(
            "def/company/shipper/out/steel.sii")!.Open());
        var content = reader.ReadToEnd();
        Assert(content.Contains("cargo_def : .ats_dispatch") &&
            content.Contains("cargo: \"cargo.steel\""),
            "Generated SII cargo association invalid");
    }
    AssertThrows<IOException>(() => CargoFlowModBuilder.Create(catalog, haul, modFile),
        "Cargo mod must refuse overwrite");
    haul.OriginCompany = "Unknown Company";
    AssertThrows<InvalidDataException>(() => CargoFlowModBuilder.Create(catalog, haul,
        Path.Combine(dataRoot, "invalid.scs")), "Unknown company must not produce a mod");
    haul.OriginCompany = "Shipper";


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


var offersFixtureRoot = Path.Combine(Path.GetTempPath(),
    "ats-offers-fixture-" + Guid.NewGuid().ToString("N"));
try
{
    Directory.CreateDirectory(offersFixtureRoot);
    var decoded = Path.Combine(offersFixtureRoot, "decoded.sii");
    const string fixture = """
        SiiNunit
        {
        economy : _nameless.demo {
        game_time: 6000
        }
        job_offer_data : _nameless.abc.123 {
        target: "shipper.los_angeles"
        expiration_time: 1156913
        urgency: 2
        shortest_distance_km: 2147
        cargo: cargo.steel
        company_truck: kw_w900
        trailer_variant: trailer.flatbed
        trailer_definition: trailer_def.steel.flatbed
        units_count: 12
        }
        job_offer_data : _nameless.abc.456 {
        target: "receiver.cheyenne"
        cargo: cargo.logs
        trailer_variant: trailer.log
        }
        }
        """;
    File.WriteAllText(decoded, fixture);
    var before = File.ReadAllBytes(decoded);
    var inspection = SaveOfferInspector.Inspect(decoded);
    Assert(inspection.Format == GameSaveFormat.PlainTextSii, "Plain text format misidentified");
    Assert(inspection.OfferCount == 2 && inspection.Offers.Count == 2, "Offers not counted");
    Assert(inspection.Offers[0].CargoUnit == "cargo.steel", "Offer cargo parsing failed");
    Assert(inspection.Offers[0].UnitsCount == 12, "Offer units_count parsing failed");
    Assert(inspection.Offers[0].ShortestDistanceKm == 2147, "Offer distance parsing failed");
    Assert(inspection.Offers[0].TrailerDefinition == "trailer_def.steel.flatbed",
        "Offer trailer definition parsing failed");
    Assert(inspection.Offers[1].Target == "receiver.cheyenne", "Second offer not parsed");

    var board = FreightBoardService.FromOffers(inspection.Offers);
    Assert(board.Count == 2 && board.All(x => !x.IsDemo), "Actual offers marked as demo");
    Assert(board[0].Detail.Contains("2147 km", StringComparison.Ordinal),
        "Original game offer distance lost");
    Assert(FreightBoardService.Search(board, "steel").Count == 1,
        "Cargo search did not filter cards");
    Assert(FreightBoardService.Search(board, "CHEYENNE").Count == 1,
        "Destination search should be case insensitive");
    Assert(FreightBoardService.Demonstration().All(x => x.IsDemo),
        "Illustrative entries must explicitly say demo");
    Assert(FreightBoardService.SchematicStops([]) == "DIRECT (schematic)",
        "Direct schematic route label incorrect");
    Assert(FreightBoardService.SchematicStops(["Cheyenne"]) == "VIA Cheyenne",
        "Waypoint label incorrect");

    var beforeOffers = new SaveOfferInspection(2, inspection.Offers, false,
        GameSaveFormat.PlainTextSii);
    var modified = inspection.Offers[0] with { Target = "receiver.denver" };
    var added = inspection.Offers[1] with { UnitId = "_nameless.new" };
    var afterOffers = new SaveOfferInspection(2, [modified, added], false,
        GameSaveFormat.PlainTextSii);
    var diff = OfferSnapshotDiffService.Compare(beforeOffers, afterOffers);
    Assert(diff.Modified == 1 && diff.Removed == 1 && diff.Added == 1,
        "Before/after offer comparison failed");
    AssertThrows<InvalidDataException>(() => OfferSnapshotDiffService.Compare(
        new SaveOfferInspection(100000, inspection.Offers, true, GameSaveFormat.PlainTextSii),
        afterOffers), "Comparison should reject truncated observations");
    AssertThrows<InvalidDataException>(() => OfferSnapshotDiffService.Compare(
        new SaveOfferInspection(2, [inspection.Offers[0], inspection.Offers[0]], false,
            GameSaveFormat.PlainTextSii),
        afterOffers), "Comparison should reject duplicate unit IDs");

    Assert(File.ReadAllBytes(decoded).SequenceEqual(before), "Inspector modified decoded save");

    var malformed = Path.Combine(offersFixtureRoot, "incomplete.sii");
    File.WriteAllText(malformed, "SiiNunit\njob_offer_data : id {\ntarget: test.city\n");
    AssertThrows<InvalidDataException>(() => SaveOfferInspector.Inspect(malformed),
        "Incomplete offer should be rejected");
    var encrypted = Path.Combine(offersFixtureRoot, "encrypted.sii");
    File.WriteAllBytes(encrypted, "ScsC"u8.ToArray());
    AssertThrows<InvalidDataException>(() => SaveOfferInspector.Inspect(encrypted),
        "Encrypted data should not be inspected as text");
    var encoded = Path.Combine(offersFixtureRoot, "encoded.sii");
    File.WriteAllBytes(encoded, "3nK\x01"u8.ToArray());
    Assert(GameProfileScanner.DetectFormat(encoded) == GameSaveFormat.Encoded3nK,
        "3nK signature not detected");

    var draft = new DispatchJob
    {
        OriginCity = "Rapid City", DestinationCity = "Cheyenne",
        Cargo = "Steel", Trailer = "Flatbed",
        ReferenceOfferUnit = inspection.Offers[0].UnitId,
        ReferenceTarget = inspection.Offers[0].Target,
        ReferenceCargoUnit = inspection.Offers[0].CargoUnit,
        ReferenceTrailerDefinition = inspection.Offers[0].TrailerDefinition,
        ReferenceTrailerVariant = inspection.Offers[0].TrailerVariant,
        ReferenceUnitsCount = inspection.Offers[0].UnitsCount
    };
    var jsonPath = Path.Combine(offersFixtureRoot, "template.json");
    JobFileService.Export(jsonPath, draft);
    var restored = JobFileService.Import(jsonPath);
    Assert(restored.ReferenceTrailerDefinition == "trailer_def.steel.flatbed",
        "JSON round trip lost trailer reference");
    Assert(restored.ReferenceTarget == "shipper.los_angeles",
        "JSON round trip lost offer target");
}
finally
{
    if (Directory.Exists(offersFixtureRoot))
        Directory.Delete(offersFixtureRoot, recursive: true);
}

Console.WriteLine("All ATS Dispatcher smoke tests passed.");

static void AssertThrows<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
