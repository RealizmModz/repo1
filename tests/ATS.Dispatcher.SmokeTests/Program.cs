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
    JobFileService.Export(exported, validJob);
    var imported = JobFileService.Import(exported);
    Assert(imported.DestinationCity == "Los Angeles", "JSON round-trip lost destination");
    Assert(imported.Waypoints.SequenceEqual(validJob.Waypoints), "JSON round-trip lost waypoints");
    Assert(imported.PaymentUsd == 3500, "JSON round-trip lost payout");

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

Console.WriteLine("All ATS Dispatcher smoke tests passed.");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
