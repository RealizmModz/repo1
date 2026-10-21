using System.Text.Json;

namespace ATS.Dispatcher.Core;

/// <summary>
/// Reads and writes independent JSON planning documents only.
/// Deliberately never modifies ATS profiles or game.sii files.
/// </summary>
public static class JobFileService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static void Export(string filePath, DispatchJob job)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var errors = DispatchValidator.Validate(job);
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        var target = Path.GetFullPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(job, Options));
            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static DispatchJob Import(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var job = JsonSerializer.Deserialize<DispatchJob>(File.ReadAllText(filePath), Options)
            ?? throw new InvalidDataException("The draft JSON has no job data.");
        var errors = DispatchValidator.Validate(job);
        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        return job;
    }
}
