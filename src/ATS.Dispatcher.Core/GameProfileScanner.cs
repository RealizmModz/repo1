namespace ATS.Dispatcher.Core;

public enum GameSaveFormat
{
    Unknown,
    PlainTextSii,
    CompressedScs,
    BinarySii
}

public sealed record AtsSaveSlot(
    string ProfileId,
    string SlotName,
    string FilePath,
    GameSaveFormat Format,
    DateTime LastModifiedUtc);

/// <summary>
/// Read-only discovery of ATS local and Documents-based Steam profiles.
/// Steam Cloud remote storage is intentionally not scanned.
/// </summary>
public static class GameProfileScanner
{
    public static IReadOnlyList<AtsSaveSlot> Scan(string documentsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentsDirectory);
        var gameDirectory = Path.Combine(documentsDirectory, "American Truck Simulator");
        var result = new List<AtsSaveSlot>();

        foreach (var kind in new[] { "profiles", "steam_profiles" })
        {
            var profileDirectory = Path.Combine(gameDirectory, kind);
            if (!Directory.Exists(profileDirectory))
                continue;

            foreach (var profile in Directory.EnumerateDirectories(profileDirectory))
            {
                var saves = Path.Combine(profile, "save");
                if (!Directory.Exists(saves))
                    continue;

                foreach (var slot in Directory.EnumerateDirectories(saves))
                {
                    var file = Path.Combine(slot, "game.sii");
                    if (!File.Exists(file))
                        continue;

                    result.Add(new AtsSaveSlot(
                        Path.GetFileName(profile),
                        Path.GetFileName(slot),
                        file,
                        DetectFormat(file),
                        File.GetLastWriteTimeUtc(file)));
                }
            }
        }

        return result.OrderByDescending(slot => slot.LastModifiedUtc).ToArray();
    }

    public static GameSaveFormat DetectFormat(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        Span<byte> header = stackalloc byte[4];
        if (stream.Read(header) != header.Length)
            return GameSaveFormat.Unknown;

        if (header.SequenceEqual("SiiN"u8))
            return GameSaveFormat.PlainTextSii;
        if (header.SequenceEqual("ScsC"u8))
            return GameSaveFormat.CompressedScs;
        if (header.SequenceEqual("BSII"u8))
            return GameSaveFormat.BinarySii;
        return GameSaveFormat.Unknown;
    }
}
