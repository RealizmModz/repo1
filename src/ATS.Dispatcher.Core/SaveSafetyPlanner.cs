using System.Security.Cryptography;

namespace ATS.Dispatcher.Core;

/// <summary>Exact snapshot identity supplied by a future observation adapter; never a path.</summary>
public sealed record SaveSnapshotIdentity(
    string ProfileId, string SaveSlotId, string AtsBuild,
    string ContentFingerprint, string SaveFormat);

/// <summary>
/// Caller-supplied observations, not OS enforcement. Unknown conditions fail closed.
/// No current UI or game adapter supplies verified live observations.
/// </summary>
public sealed record SavePreflightObservation(SaveSnapshotIdentity Identity)
{
    public bool? GameRunning { get; init; }
    public bool? CloudEnabled { get; init; }
    public bool? CloudConflict { get; init; }
    public bool? WriteAccess { get; init; }
    public bool? DraftValidated { get; init; }
    public bool? AdapterSupported { get; init; }
    public long? AvailableBytes { get; init; }
}

public enum SaveSafetyFailure
{
    InvalidIdentity, IdentityChanged, InvalidHash, SourceChanged,
    GameNotKnownClosed, CloudNotKnownDisabled, CloudConflictNotKnownAbsent,
    AccessNotKnownAvailable, DraftNotValidated, AdapterNotSupported,
    InvalidSpaceRequirement, InsufficientSpace, InvalidPayload,
    BackupMismatch, CandidateUnchanged, CandidateRejected, CandidateValidatorMutatedInput,
    BackupCorrupt, CurrentSaveNotExpected
}

public sealed class SaveSafetyException : InvalidOperationException
{
    public SaveSafetyFailure Failure { get; }
    public SaveSafetyException(SaveSafetyFailure failure)
        : base("Save safety review rejected: " + failure) => Failure = failure;
}

/// <summary>
/// Review-only immutable evidence. Not a durable transaction receipt, commit token,
/// proof of ATS success, or permission to apply a candidate.
/// </summary>
public sealed record SaveSafetyManifest(
    Guid ReviewId, SaveSnapshotIdentity Identity, string SourceSha256,
    string BackupSha256, string CandidateSha256, long SourceBytes,
    long CandidateBytes, long RequiredFreeBytes)
{
    public string Status => "DRY RUN ONLY / NO SAVE WRITES";
}

/// <summary>No file is restored. The expected current hash protects newer saves in the plan.</summary>
public sealed record SaveRecoveryReview(
    Guid ReviewId, string ExpectedCurrentSha256, string RestoreSha256)
{
    public string Status => "RECOVERY REVIEW ONLY / NOT RESTORED";
}

/// <summary>
/// Pure safety planning over supplied bytes. No filesystem/process/cloud access,
/// mutation API, retained payloads, or dispatch capability. A future transaction
/// adapter must obtain fresh trusted observations, lock/recheck/commit atomically,
/// persist receipts, and verify ATS results. This planner cannot enforce those steps.
/// </summary>
public static class SaveSafetyPlanner
{
    private const int PayloadLimit = 64 * 1024 * 1024;

    public static IReadOnlyList<SaveSafetyFailure> CheckPreflight(
        SaveSnapshotIdentity expected, SavePreflightObservation current,
        string expectedSourceSha256, ReadOnlySpan<byte> source, long requiredFreeBytes)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(current);
        var failures = new List<SaveSafetyFailure>();
        if (!ValidIdentity(expected) || !ValidIdentity(current.Identity))
            failures.Add(SaveSafetyFailure.InvalidIdentity);
        if (expected != current.Identity) failures.Add(SaveSafetyFailure.IdentityChanged);
        if (!ValidHash(expectedSourceSha256)) failures.Add(SaveSafetyFailure.InvalidHash);
        else if (!Hash(source).Equals(expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
            failures.Add(SaveSafetyFailure.SourceChanged);
        if (source.IsEmpty || source.Length > PayloadLimit) failures.Add(SaveSafetyFailure.InvalidPayload);
        if (current.GameRunning != false) failures.Add(SaveSafetyFailure.GameNotKnownClosed);
        if (current.CloudEnabled != false) failures.Add(SaveSafetyFailure.CloudNotKnownDisabled);
        if (current.CloudConflict != false) failures.Add(SaveSafetyFailure.CloudConflictNotKnownAbsent);
        if (current.WriteAccess != true) failures.Add(SaveSafetyFailure.AccessNotKnownAvailable);
        if (current.DraftValidated != true) failures.Add(SaveSafetyFailure.DraftNotValidated);
        if (current.AdapterSupported != true) failures.Add(SaveSafetyFailure.AdapterNotSupported);
        if (requiredFreeBytes <= 0) failures.Add(SaveSafetyFailure.InvalidSpaceRequirement);
        else if (!current.AvailableBytes.HasValue || current.AvailableBytes.Value < requiredFreeBytes)
            failures.Add(SaveSafetyFailure.InsufficientSpace);
        return failures.AsReadOnly();
    }

    public static SaveSafetyManifest ReviewStage(
        SaveSnapshotIdentity expected, SavePreflightObservation current,
        string expectedSourceSha256, ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> backupReadback, ReadOnlySpan<byte> candidate,
        Func<ReadOnlyMemory<byte>, bool> validateRelevantGraph)
    {
        ArgumentNullException.ThrowIfNull(validateRelevantGraph);
        // Own the inputs before validation. No callbacks receive these snapshots.
        Require(source.Length is > 0 and <= PayloadLimit &&
            candidate.Length is > 0 and <= PayloadLimit &&
            backupReadback.Length == source.Length, SaveSafetyFailure.InvalidPayload);
        var original = source.ToArray();
        var backup = backupReadback.ToArray();
        var staged = candidate.ToArray();
        // Minimum modeled reservation: backup + stage + retained recovery copy.
        // Real filesystem overhead / multi-file costs remain an adapter responsibility.
        long required = checked(2L * original.Length + staged.Length);
        RejectFirst(CheckPreflight(expected, current, expectedSourceSha256, original, required));
        Require(original.AsSpan().SequenceEqual(backup), SaveSafetyFailure.BackupMismatch);
        Require(!original.AsSpan().SequenceEqual(staged), SaveSafetyFailure.CandidateUnchanged);
        string candidateHash = Hash(staged);
        var validationCopy = staged.ToArray();
        bool accepted = validateRelevantGraph(validationCopy);
        Require(Hash(validationCopy) == candidateHash, SaveSafetyFailure.CandidateValidatorMutatedInput);
        Require(accepted, SaveSafetyFailure.CandidateRejected);
        return new SaveSafetyManifest(Guid.NewGuid(), expected, Hash(original), Hash(backup),
            candidateHash, original.Length, staged.Length, required);
    }

    /// <summary>
    /// Repeats review against fresh observations and bytes. Still never commits.
    /// The caller must repeat actual checks inside a future exclusive transaction.
    /// </summary>
    public static void RecheckStage(SaveSafetyManifest manifest,
        SavePreflightObservation current, ReadOnlySpan<byte> currentSource,
        ReadOnlySpan<byte> backupReadback, ReadOnlySpan<byte> stagedReadback)
    {
        ValidateManifest(manifest);
        RejectFirst(CheckPreflight(manifest.Identity, current, manifest.SourceSha256,
            currentSource, manifest.RequiredFreeBytes));
        Require(backupReadback.Length == manifest.SourceBytes &&
            Hash(backupReadback) == manifest.BackupSha256, SaveSafetyFailure.BackupCorrupt);
        Require(stagedReadback.Length == manifest.CandidateBytes &&
            Hash(stagedReadback) == manifest.CandidateSha256, SaveSafetyFailure.CandidateRejected);
    }

    /// <summary>
    /// Review restoration only when the target is exactly the recorded candidate.
    /// A newer ATS save (even after a successful delivery) requires separate review.
    /// This conservative rule deliberately prevents automatic rollback over progress.
    /// </summary>
    public static SaveRecoveryReview ReviewRecovery(SaveSafetyManifest manifest,
        SavePreflightObservation current, ReadOnlySpan<byte> currentTarget,
        ReadOnlySpan<byte> backupReadback)
    {
        ValidateManifest(manifest);
        // A validated old draft is irrelevant to recovery, but target compatibility
        // and environment observations remain required in this conservative prototype.
        RejectFirst(CheckPreflight(manifest.Identity, current with { DraftValidated = true }, manifest.CandidateSha256,
            currentTarget, manifest.RequiredFreeBytes)
            .Select(x => x == SaveSafetyFailure.SourceChanged
                ? SaveSafetyFailure.CurrentSaveNotExpected : x).ToArray());
        Require(currentTarget.Length == manifest.CandidateBytes, SaveSafetyFailure.CurrentSaveNotExpected);
        Require(backupReadback.Length == manifest.SourceBytes &&
            Hash(backupReadback) == manifest.BackupSha256, SaveSafetyFailure.BackupCorrupt);
        return new SaveRecoveryReview(manifest.ReviewId, manifest.CandidateSha256, manifest.BackupSha256);
    }

    private static void ValidateManifest(SaveSafetyManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        // Public records can be deserialized or copied with altered fields.
        // Consistency checks are not cryptographic authentication of the receipt.
        Require(manifest.ReviewId != Guid.Empty && ValidIdentity(manifest.Identity),
            SaveSafetyFailure.InvalidIdentity);
        Require(ValidHash(manifest.SourceSha256) && ValidHash(manifest.BackupSha256) &&
            ValidHash(manifest.CandidateSha256), SaveSafetyFailure.InvalidHash);
        Require(manifest.SourceSha256 == manifest.BackupSha256, SaveSafetyFailure.BackupMismatch);
        Require(manifest.SourceSha256 != manifest.CandidateSha256, SaveSafetyFailure.CandidateUnchanged);
        Require(manifest.SourceBytes is > 0 and <= PayloadLimit &&
            manifest.CandidateBytes is > 0 and <= PayloadLimit &&
            manifest.RequiredFreeBytes == checked(2L * manifest.SourceBytes + manifest.CandidateBytes),
            SaveSafetyFailure.InvalidSpaceRequirement);
    }

    private static bool ValidIdentity(SaveSnapshotIdentity? identity) => identity is not null &&
        !string.IsNullOrWhiteSpace(identity.ProfileId) && !string.IsNullOrWhiteSpace(identity.SaveSlotId) &&
        !string.IsNullOrWhiteSpace(identity.AtsBuild) && !string.IsNullOrWhiteSpace(identity.ContentFingerprint) &&
        !string.IsNullOrWhiteSpace(identity.SaveFormat);

    private static bool ValidHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void RejectFirst(IReadOnlyList<SaveSafetyFailure> failures)
    {
        if (failures.Count != 0) throw new SaveSafetyException(failures[0]);
    }
    private static void Require(bool condition, SaveSafetyFailure failure)
    {
        if (!condition) throw new SaveSafetyException(failure);
    }
}
