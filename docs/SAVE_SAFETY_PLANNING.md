# Save transaction safety planning (dry run)

This is a Core development foundation, with synthetic regressions, not an ATS adapter or a completed M7 transaction. No UI action invokes it. Create ATS Job remains disabled.

## Implemented boundary

SaveSafetyPlanner operates only on caller-supplied byte spans and immutable identity/observation records. It has no filesystem, process, network or Steam API, no commit/restore method, and no retained save payloads. It creates a hash manifest describing a review, not a durable applied receipt or permission to dispatch.

- Preflight fails closed on missing identity, changed profile/save slot/build/content/format, stale source hash, unknown/running game, enabled/unknown Cloud, conflict/unknown conflict, unavailable/unknown access, unvalidated draft, unsupported/unknown adapter, or insufficient/unknown disk space.
- Stage review owns independent source/backup/candidate snapshots. Backup readback must equal the original bytes. Candidate must differ, remain within the research bound and pass the supplied relevant-graph validator. Validation gets an isolated copy; mutation is rejected. Validator exceptions propagate without a manifest.
- Reservation models backup + candidate + retained recovery bytes. It does not determine actual filesystem overhead or multi-file capacity.
- RecheckStage repeats preflight against newly supplied observations/source, and checks backup/stage hashes and lengths.
- Recovery review requires the current target to equal the recorded candidate. Any newer game save is rejected. A completed delivery normally creates newer state: restoring over that state needs a separately reviewed recovery procedure, not a bypass of this guard.
- Public manifests are checked for internal consistency. They are not signed or authenticated and must not be treated as trusted authorization tokens.

The graph validator is an injected contract. The planner does not establish cargo/depot eligibility, reference semantics, output-format acceptance, or real job generation. Tests using a validator that returns true model orchestration only.

## What remains before actual save transactions

A future adapter must independently observe actual game/process, exact selected profile and save, supported version/content, Cloud isolation/conflicts, access and space. Boolean fields are caller reports, not OS-enforced guards. Current production game adapter support remains unproven.

Implement and test exclusive/concurrent access, fresh observations immediately before mutation, durable independent backups/readback, full necessary reference validation, same-filesystem staging, atomic replacement where supported, durable receipts, crash/multi-file recovery and verified restoration. An in-memory check cannot close a check-to-write race.

Persist only reviewed privacy-safe diagnostics. Keep raw player saves and profile identifiers out of GitHub and CI. Do not wire this planner to live writes while G1 and specific disposable-profile authorization remain outstanding.

## Regression evidence

SaveSafetyTests is included in the existing Windows smoke suite. It exercises every unknown/unsafe preflight condition, identity drift, source changes, backup/candidate corruption, altered manifests, isolated validator input, validator rejection/exception, serialized manifest readback, and refusal to restore over newer bytes. Synthetic source/backup/candidate bytes remain unchanged.

CI also builds WPF and launches the packaged application. These checks do not establish disk-fault recovery, live process/Cloud enforcement or ATS pickup/delivery. No game integration was performed.

## Development baseline

PR #1 was merged into main at 618536d2ec09b5a0d9ddbb89815c3f2d0673cf85. Follow-up work uses feat/dispatch-safety-planning and a new draft PR. Workflow push coverage includes main and feat/**; PR filters cover source, tests, docs, README and the workflow.

Next gate: reviewed disposable-profile format control, one genuinely different supported custom job through pickup/delivery and verified recovery. See M2_SAVE_FIXTURES.md and EXPERIMENTAL_DISPATCH.md.
