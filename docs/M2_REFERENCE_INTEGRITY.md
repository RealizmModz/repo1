# M2 read-only reference integrity research

This checklist is for **separately decoded, copied, disposable-profile** ATS saves only. It does not authorize game-save writes or claim custom dispatch is possible.

## Observation contract

For each inspected `job_offer_data` unit, retain its exact unit ID, source save hash, ATS build, save format, and any present target, cargo, trailer variant/definition, company truck, unit count, expiry, and original-offer distance. Unknown or missing fields remain unknown.

## Implemented inspection safeguards

The bounded UTF-8 inspector now validates the outer `SiiNunit` envelope and offer-block structure, rejects duplicate offer IDs (including incomplete offers and records beyond the display cap), and rejects conflicting values for the eight tracked offer fields. It ignores braces/comment markers inside quoted strings, handles line/block comments, rejects invalid numeric observations and invalid UTF-8, and hashes the exact bytes read from the same read-only stream.

Missing target/cargo records are counted explicitly and excluded from usable templates. Snapshot comparisons reject missing-field or capped observations instead of presenting a partial list as a complete change set. Both inspection windows show missing-field counts. The first 25,000 observed offers bound display results; parsing/validation continues to EOF.

This is deliberately a limited reader: the envelope must use separate `SiiNunit` and opening-brace lines, offer headers must have their opening brace on the same line, and offer properties must be single-line scalars. Unsupported syntax fails closed. Unknown units are checked for balanced braces, not interpreted. The hash identifies the copied source bytes; it does not establish ATS version, graph completeness or write compatibility. No ATS build is inferred.

Synthetic smoke regressions cover duplicates, conflicting fields, complete/missing/capped observations, comments and quoted braces, malformed/truncated envelopes, invalid integers/UTF-8, oversize rejection, exact source hashing and unchanged source bytes. These are parser checks, not in-game validation.

## Read-only exact-token reference report

The inspector now indexes up to 250,000 top-level unit headers recognized by its bounded same-line header syntax and rejects duplicate indexed unit IDs. It retains only ID/type pairs for unrelated units, not their profile properties. Unrecognized top-level openings explicitly mark header coverage partial.

`OfferReferenceReport` retains separate target, cargo, trailer variant/definition and company-truck edges for usable retained offers. An exact raw token match is labeled `ObservedUnitHeader` with the observed type; all absent matches are `Unverified`. No prefix rewriting, depot inference, semantic type validation or external-definition resolution is attempted. A complete header-syntax pass does NOT imply a complete relevant game graph.

After a successful decoded-copy import, the market's **Offer reference report** button shows the source hash, counts, syntax coverage and a bounded preview of edges. This report stays local and read-only. Synthetic regressions verify present/absent/external references, partial header coverage, duplicate non-offer IDs, provenance and unchanged copied bytes.

## Remaining graph research and observation requirements

1. [Implemented for offer inspection] Reject duplicate `job_offer_data` unit IDs within a snapshot; do not silently keep the first or last.
2. [Implemented for the bounded syntax above] Reject unterminated unit blocks, malformed braces and truncated snapshots. A parser's maximum-record cap must be distinguishable from a complete snapshot.
3. [Implemented for the five tracked reference fields; semantic graph validation pending] Track every reference edge separately from its friendly display text. A target/cargo/trailer reference is not proof that the referenced unit exists in the observed subset.
4. [Implemented as Unverified] Report unresolved references as **unverified** until a complete relevant unit graph is available, not as definitely invalid when the fixture intentionally omits unrelated units.
5. [Implemented for the eight tracked offer fields] Reject conflicting field values within one unit unless the relevant ATS format establishes a deterministic interpretation.
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
