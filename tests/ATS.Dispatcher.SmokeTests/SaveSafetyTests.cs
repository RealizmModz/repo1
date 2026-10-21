using ATS.Dispatcher.Core;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class SaveSafetyTests
{
    public static void Run()
    {
        // Synthetic bytes only: these do not model an ATS job or pass G1.
        byte[] source = [1, 2, 3, 4], backup = [1, 2, 3, 4], candidate = [1, 2, 9, 4, 5];
        var original = source.ToArray();
        var originalCandidate = candidate.ToArray();
        var identity = new SaveSnapshotIdentity("synthetic-profile", "manual-slot", "test-build",
            "test-content", "synthetic-format");
        string hash = Hash(source);
        var observation = new SavePreflightObservation(identity)
        {
            GameRunning = false, CloudEnabled = false, CloudConflict = false,
            WriteAccess = true, DraftValidated = true, AdapterSupported = true, AvailableBytes = 100
        };
        SaveSafetyManifest Stage(SavePreflightObservation? state = null,
            Func<ReadOnlyMemory<byte>, bool>? validator = null) =>
            SaveSafetyPlanner.ReviewStage(identity, state ?? observation, hash, source, backup,
                candidate, validator ?? (_ => true));

        var manifest = Stage();
        Check(manifest.SourceSha256 == hash && manifest.BackupSha256 == hash &&
            manifest.CandidateSha256 == Hash(candidate), "Manifest byte provenance");
        Check(manifest.RequiredFreeBytes == 13, "Backup/stage/recovery reservation");
        Check(manifest.Status.Contains("NO SAVE WRITES"), "Dry-run status");
        SaveSafetyPlanner.RecheckStage(manifest, observation, source, backup, candidate);
        var recoveredManifest = JsonSerializer.Deserialize<SaveSafetyManifest>(
            JsonSerializer.Serialize(manifest))!;
        SaveSafetyPlanner.RecheckStage(recoveredManifest, observation, source, backup, candidate);

        int validatorCalls = 0;
        bool Validator(ReadOnlyMemory<byte> bytes) { validatorCalls++; return true; }
        var blocked = new (SavePreflightObservation State, SaveSafetyFailure Failure)[]
        {
            (observation with { GameRunning = true }, SaveSafetyFailure.GameNotKnownClosed),
            (observation with { GameRunning = null }, SaveSafetyFailure.GameNotKnownClosed),
            (observation with { CloudEnabled = true }, SaveSafetyFailure.CloudNotKnownDisabled),
            (observation with { CloudEnabled = null }, SaveSafetyFailure.CloudNotKnownDisabled),
            (observation with { CloudConflict = true }, SaveSafetyFailure.CloudConflictNotKnownAbsent),
            (observation with { CloudConflict = null }, SaveSafetyFailure.CloudConflictNotKnownAbsent),
            (observation with { WriteAccess = false }, SaveSafetyFailure.AccessNotKnownAvailable),
            (observation with { WriteAccess = null }, SaveSafetyFailure.AccessNotKnownAvailable),
            (observation with { DraftValidated = false }, SaveSafetyFailure.DraftNotValidated),
            (observation with { DraftValidated = null }, SaveSafetyFailure.DraftNotValidated),
            (observation with { AdapterSupported = false }, SaveSafetyFailure.AdapterNotSupported),
            (observation with { AdapterSupported = null }, SaveSafetyFailure.AdapterNotSupported),
            (observation with { AvailableBytes = 12 }, SaveSafetyFailure.InsufficientSpace),
            (observation with { AvailableBytes = null }, SaveSafetyFailure.InsufficientSpace),
            (observation with { AvailableBytes = -1 }, SaveSafetyFailure.InsufficientSpace)
        };
        foreach (var (state, failure) in blocked)
        {
            Reject(() => Stage(state, Validator), failure);
            Reject(() => SaveSafetyPlanner.RecheckStage(manifest, state, source, backup, candidate), failure);
        }
        Check(validatorCalls == 0, "Rejected preflight called candidate validation");
        foreach (var changed in new[]
        {
            identity with { ProfileId = "other" }, identity with { SaveSlotId = "other" },
            identity with { AtsBuild = "other" }, identity with { ContentFingerprint = "other" },
            identity with { SaveFormat = "other" }
        })
        {
            Reject(() => Stage(observation with { Identity = changed }), SaveSafetyFailure.IdentityChanged);
            Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest,
                observation with { Identity = changed }, candidate, backup), SaveSafetyFailure.IdentityChanged);
        }
        Reject(() => Stage(observation with { Identity = identity with { ProfileId = "" } }),
            SaveSafetyFailure.InvalidIdentity);
        Reject(() => Stage(observation with { Identity = null! }), SaveSafetyFailure.InvalidIdentity);
        Reject(() => SaveSafetyPlanner.ReviewStage(identity, observation, "not-a-hash",
            source, backup, candidate, _ => true), SaveSafetyFailure.InvalidHash);
        Reject(() => SaveSafetyPlanner.ReviewStage(identity, observation, new string('0', 64),
            source, backup, candidate, _ => true), SaveSafetyFailure.SourceChanged);
        Reject(() => SaveSafetyPlanner.ReviewStage(identity, observation, hash,
            source, [0, 2, 3, 4], candidate, Validator), SaveSafetyFailure.BackupMismatch);
        Reject(() => SaveSafetyPlanner.ReviewStage(identity, observation, hash,
            source, backup, source, _ => true), SaveSafetyFailure.CandidateUnchanged);
        Reject(() => SaveSafetyPlanner.ReviewStage(identity, observation, hash,
            source, backup, [], _ => true), SaveSafetyFailure.InvalidPayload);
        Reject(() => Stage(validator: _ => false), SaveSafetyFailure.CandidateRejected);
        bool mutated = false;
        Reject(() => Stage(validator: memory =>
        {
            Check(MemoryMarshal.TryGetArray(memory, out var segment), "Validation copy unavailable");
            segment.Array![segment.Offset] ^= 1;
            mutated = true;
            return true;
        }), SaveSafetyFailure.CandidateValidatorMutatedInput);
        Check(mutated && candidate.SequenceEqual(originalCandidate), "Validator reached retained/caller bytes");
        try { Stage(validator: _ => throw new IOException("synthetic validator failure")); }
        catch (IOException) { validatorCalls++; }
        Check(validatorCalls == 1, "Validator failure must propagate without a manifest");

        Reject(() => SaveSafetyPlanner.RecheckStage(manifest, observation, [9, 2, 3, 4],
            backup, candidate), SaveSafetyFailure.SourceChanged);
        Reject(() => SaveSafetyPlanner.RecheckStage(manifest, observation, source,
            [9, 2, 3, 4], candidate), SaveSafetyFailure.BackupCorrupt);
        Reject(() => SaveSafetyPlanner.RecheckStage(manifest, observation, source,
            backup, [1, 2, 9, 4, 6]), SaveSafetyFailure.CandidateRejected);
        Reject(() => SaveSafetyPlanner.RecheckStage(manifest with { RequiredFreeBytes = 1 },
            observation, source, backup, candidate), SaveSafetyFailure.InvalidSpaceRequirement);

        var recovery = SaveSafetyPlanner.ReviewRecovery(manifest,
            observation with { DraftValidated = null }, candidate, backup);
        Check(recovery.ExpectedCurrentSha256 == Hash(candidate) &&
            recovery.RestoreSha256 == hash && recovery.Status.Contains("NOT RESTORED"),
            "Recovery evidence/status");
        Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest, observation, [8, 8, 8],
            backup), SaveSafetyFailure.CurrentSaveNotExpected);
        Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest,
            observation with { GameRunning = true }, candidate, backup), SaveSafetyFailure.GameNotKnownClosed);
        Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest,
            observation with { CloudEnabled = null }, candidate, backup), SaveSafetyFailure.CloudNotKnownDisabled);
        Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest, observation, candidate,
            [9, 2, 3, 4]), SaveSafetyFailure.BackupCorrupt);
        Reject(() => SaveSafetyPlanner.ReviewRecovery(manifest with { BackupSha256 = new string('0', 64) },
            observation, candidate, backup), SaveSafetyFailure.BackupMismatch);
        Check(SaveSafetyPlanner.CheckPreflight(identity, new SavePreflightObservation(identity),
            hash, source, 13).Count == 7, "Unknown environment accepted");
        Check(source.SequenceEqual(original) && backup.SequenceEqual(original) &&
            candidate.SequenceEqual(originalCandidate), "Review changed caller-owned bytes");
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static void Reject(Action action, SaveSafetyFailure failure)
    {
        try { action(); }
        catch (SaveSafetyException ex) when (ex.Failure == failure) { return; }
        throw new InvalidOperationException("Expected rejection: " + failure);
    }
    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
