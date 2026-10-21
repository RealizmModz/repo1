using System.Security.Cryptography;

namespace ATS.Dispatcher.Core;

public sealed record VerifiedSaveBackup(string BackupFile, long Bytes, string Sha256);

/// <summary>
/// Creates a separate, hashed copy of a save. Never writes into the source
/// directory/file and never tries to deserialize or inject game data.
/// </summary>
public static class SaveBackupService
{
    public static VerifiedSaveBackup CreateVerifiedBackup(string sourcePath, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
            throw new FileNotFoundException("Save file was not found.", source);
        var folder = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(folder);
        var fileName = "ats-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" +
            Guid.NewGuid().ToString("N") + "-game.sii.bak";
        var target = Path.Combine(folder, fileName);

        try
        {
            byte[] originalHash;
            long length;
            // Reject writers while taking the snapshot (or fail if a writer
            // already holds an incompatible handle). This is not an ATS lock.
            using (var original = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                originalHash = SHA256.HashData(original);
                length = original.Length;
                original.Position = 0;

                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None);
                original.CopyTo(output);
                output.Flush(flushToDisk: true);
            }

            byte[] copyHash;
            using (var copy = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (copy.Length != length)
                    throw new IOException("Backup length differs from original.");
                copyHash = SHA256.HashData(copy);
            }
            if (!CryptographicOperations.FixedTimeEquals(originalHash, copyHash))
                throw new IOException("Backup checksum does not match the original save.");
            return new VerifiedSaveBackup(target, length, Convert.ToHexString(copyHash));
        }
        catch
        {
            if (File.Exists(target))
                File.Delete(target);
            throw;
        }
    }
}
