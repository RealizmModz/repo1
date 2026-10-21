# M2: safe disposable-profile fixture collection

**Purpose:** Determine if an exact custom freight job can be created with version-gated, verifiable save-file editing. The existing desktop program currently performs **no ATS save writes**.

## 1. Disposable test setup

1. Create a **new, expendable ATS profile**. Avoid Steam Cloud sync for this disposable profile and do not open important profiles with experiments.
2. Record the ATS build/version, enabled DLC, installed mods, load order, and save format. Use a test profile with no mods first.
3. Create a save named \`before-offer\` before accepting a new freight contract. Then perform a single in-game change (offer refresh / select freight / accept job); create \`after-offer\` in a **different save slot**.
4. **Quit ATS**. Copy both save directories outside the profile before decoding anything.
5. Make SHA-256 hashes for the original files; never decode, replace or overwrite those originals.
6. Decode only your separate copies with a trusted locally verified utility. This repo does not execute a third-party SII decoder.
7. Inspect the decoded \`game.sii\` copies with the read-only inspector. Compare \`job_offer_data\` unit identifiers, cargo, target, trailer, company truck, count, expiry and route distance.
8. Use the synthetic tests in CI before attempting any parser on a real save.

## 2. Privacy and licensing

Do **not** commit real player saves, Steam credentials, profile identifiers, screenshots showing private account details, or proprietary game archives. For analysis, generate sanitized fixtures that preserve only the minimal necessary unit relationship structure. Store raw copies locally in a test-only folder excluded from Git.

## 3. G1 feasibility pass criteria

- Reliable parsing for a precisely recorded ATS version and save type.
- Reproducible, safe **generation of a different custom job** with correct depot instance, cargo/trailer units and references, not just copied metadata.
- Tests confirm ATS loads the disposable profile, displays the generated offer, allows pickup and completing delivery.
- Tests confirm save protection: game-closed enforcement, no writes on validation failure, backup checksums, atomic staging and successful rollback.
- If exact route waypoints or payout cannot be honored, the app must visibly disable those controls and document support separately.

Until all of this is demonstrated, the product is an **ATS-inspired planner and mod experiment**, not a proven custom job generator.
