# M2 read-only reference integrity research

This checklist is for **separately decoded, copied, disposable-profile** ATS saves only. It does not authorize game-save writes or claim custom dispatch is possible.

## Observation contract

For each inspected `job_offer_data` unit, retain its exact unit ID, source save hash, ATS build, save format, and any present target, cargo, trailer variant/definition, company truck, unit count, expiry, and original-offer distance. Unknown or missing fields remain unknown.

## Integrity checks to implement

1. Reject duplicate `job_offer_data` unit IDs within a snapshot; do not silently keep the first or last.
2. Reject unterminated unit blocks, malformed braces and truncated snapshots. A parser's maximum-record cap must be distinguishable from a complete snapshot.
3. Track every reference edge separately from its friendly display text. A target/cargo/trailer reference is not proof that the referenced unit exists in the observed subset.
4. Report unresolved references as **unverified** until a complete relevant unit graph is available, not as definitely invalid when the fixture intentionally omits unrelated units.
5. Reject conflicting field values within one unit unless the relevant ATS format establishes a deterministic interpretation.
6. Preserve source offer distance as reference metadata only; never treat it as distance for an edited draft route.
7. Do not infer exact depot instances, payout, or waypoint persistence from target/cargo strings alone.

## Deterministic synthetic test cases

- One complete offer with known fields; all values preserved.
- Two offers with distinct IDs; both retained.
- Duplicate ID with identical payload; rejected.
- Duplicate ID with conflicting payload; rejected.
- Unterminated block and malformed brace; rejected.
- Missing target/cargo; classified explicitly without guessing.
- Duplicate property with conflicting values; rejected.
- Reference to absent unit in a deliberately partial fixture; unresolved rather than fabricated.
- Two snapshots with an unchanged offer, an added offer, a removed offer, and a modified offer; differences remain read-only.
- Oversized, encrypted, binary or invalid-UTF-8 inputs; rejected without writes.

## Feasibility evidence still required

Record the precise ATS version, installed DLC, mod/load order, copied-save SHA-256 hashes and sanitized before/after relationships. Only a separately authorized disposable-profile experiment can establish that a genuinely different origin/destination/cargo job appears, can be picked up, delivered and rolled back. GPS waypoints and payment overrides require independent proof.

**Safety boundary:** no mutation of ATS saves, no decoder execution, no proprietary archives or real player saves in GitHub. Keep `Create ATS Job` disabled.
