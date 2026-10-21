# ATS Custom Dispatcher — Master Roadmap (Start → Finished Product)

**Product goal:** A complete ATS-inspired Freight/Cargo Market Windows app with a familiar map-left / job-list-right screen, editable real pickup and destination depots, cargo and trailer choices, waypoint route planner, presets and **proven** in-game custom dispatch for supported ATS versions.

Read [Product Vision + UX](PRODUCT_VISION.md) for the actual finished screen, interactions and design language. Read [Technical Plan](TECHNICAL_PLAN.md) for the integration architecture, map readiness modes, testing and safe writes.

**Current reality:** The existing prototype now launches an ATS-inspired schematic-map-left / offer-board-right screen (NOT verified road geometry), and also has a WPF draft editor, JSON import/export, read-only save recognition and job-offer inspection, extracted-definition catalog importer, advisory compatibility checks, independently verified save backups, and experimental `.scs` cargo-flow export. **It is NOT yet an in-game custom job generator and its UI is still a preliminary ATS-inspired shell, not the finished Freight Market.** Current build CI checks do not replace in-game tests.

The plan below defines acceptance criteria, not completed features. Tasks checked `[x]` were implemented in prototype; game tests still required wherever indicated.

## Definition of done — the *finished* v1

A user on a supported ATS installation can:
1. Open an ATS-inspired desktop job market showing a readable, interactive map on the left and searchable freight/cargo cards on the right.
2. Pick a job or start from scratch; change pickup **depot**, destination **depot**, cargo, compatible trailer, eligible weight/units and optional route waypoints without a raw file editor.
3. See an accurate route **only** where supported by a verified compatible map graph; see explicit labels where route display or GPS application is not supported.
4. Save/reload presets, review expected distance, cargo restrictions, warnings and any editable payout fields.
5. Generate an actual job in a disposable profile, then in a real supported profile using safe backups, version gates, atomic staging, in-game verification and undo/restore.
6. Take and finish that generated job in ATS; its pickup, destination and cargo match the chosen plan. Unsupported inputs are rejected, never silently rewritten.
7. Install/update/uninstall from a documented Windows package without corrupting ATS saves or redistributing proprietary SCS resources.

**Stretch goals** (not required for v1): near-perfect in-game map road geometry, all mod ecosystems, native controller support, multi-profile batch dispatch, and GPS road-by-road synchronization. Do not promise these without evidence.

## Critical path and milestones

### M0 — Establish safe foundation [Implemented in prototype]
- [x] .NET/WPF app builds through Windows GitHub Actions.
- [x] Draft validation and JSON import/export.
- [x] Detect ATS local save locations/signatures (read only).
- [x] Import loose extracted `def` catalog; basic advisory city/depot/cargo/trailer matches.
- [x] Inspect real `job_offer_data` in a separately decoded SiiNunit **copy**.
- [x] Compare two decoded snapshots to list added/removed/modified offer units (read-only).
- [x] Store reference-offer IDs in draft; create SHA-256 verified standalone backups.
- [x] Export an **experimental**, independently packaged company cargo-flow .scs mod.
- [ ] Validate experimental mod behavior inside a disposable ATS game profile.
**Exit gate:** all core CI green (previously verified); never claim live dispatch based on M0.

### M1 — Finalize product UX and reference design [Specification prepared]
- [x] Document ATS-inspired dark aesthetic and map-left/jobs-right screen (PRODUCT_VISION.md).
- [x] Define cargo cards, custom editor, map workflows, screen inventory and state/error design.
- [ ] Capture current ATS Freight Market and Cargo Market screenshots from a legitimately installed build for layout calibration.
- [ ] Create original icon/thumbnail/theme asset specification, responsive grid and design tokens.
- [ ] Approve interaction prototype: clicking city, editing destination, selecting cargo, previewing job.
**Exit gate:** signed-off interactive wireflow + measurable UI acceptance checks, and no borrowed proprietary art bundled.

### M2 — Prove actual dispatch & routing feasibility (HIGH PRIORITY / BLOCKER)
- [ ] Establish disposable profile with version, installed DLCs and mod list recorded.
- [ ] Gather anonymized decoded original saves **before and after** a normal job appears/changes.
- [ ] Investigate game job-unit identities, references, expiry, target depot mapping, cargo, trailer and payout.
- [x] Create a read-only before/after offer diff with synthetic tests; see [fixture protocol](M2_SAVE_FIXTURES.md).
- [x] Add bounded offer inspection integrity checks (duplicate IDs, conflicting tracked fields, envelope structure, source hashes, missing-field/cap comparison rejection) with synthetic regressions; see [scope and remaining graph research](M2_REFERENCE_INTEGRITY.md). This does not validate complete unit references or pass G1.
- [x] Add a bounded read-only top-level header index and exact-token offer-reference report with unverified/partial coverage labels and synthetic tests. Complete reference-graph semantics remain pending.
- [ ] Demonstrate creating at least one *different* valid custom cargo/depot job, loading it in ATS and finishing/rolling it back.
- [ ] Investigate whether programmatic GPS waypoints are representable/persistent; document unsupported behavior.
- [ ] Investigate legal road-map geometry sources and map projection/city coordinates.
- [ ] Record a capability matrix with explicit game-version restrictions.
**Exit gate G1:** real in-game supported job creation validated end-to-end or formally switch v1 to planner-only and update promises. **Do not postpone this hard technical risk until after UI polish.**

### M3 — Reliable local game data model
- [ ] Normalize catalog into actual stable city, company, **depot instance**, cargo, trailer and map IDs.
- [ ] Parse enough SII includes, inheritance and game/mod override order for valid choices.
- [ ] Resolve enabled DLCs, map content, cargo skills, state restrictions, trailer length/axle/mass/capacity and prefab compatibility.
- [ ] Cache snapshots by game content fingerprint; rescan when ATS or mod data changes.
- [ ] Expose valid choices (and reasons when unavailable) to app view models.
- [ ] Add synthetic and real fixture tests for unmodded, DLC and modded profiles.
**Exit gate:** selecting impossible combinations is consistently blocked before dispatch.

### M4 — Build the ATS-style screen shell
- [x] Initial WPF market shell: original ATS-inspired dark layout, map-left/cards-right, editable draft and disabled dispatch.
- [x] Read-only cards from decoded real offers, explicitly labeled illustration-only demo cards, search and basic route schematic.
- [x] Legacy advanced tools accessible from new home screen.
- [ ] Refactor WPF code-behind into MVVM app structure and unit-testable actions (current visual shell still uses code-behind).
- [ ] Create top tabs: Freight Market, Cargo Market, My Presets, Custom Dispatch.
- [ ] Finish dark panels, original assets, sorting, multi-tab behavior, advanced filters and full cargo browser (base styles and searchable cards started).
- [ ] Implement detailed cargo/trailer browser and editable right-side job inspector (basic editable inspector started; full constraints pending).
- [ ] Expose loading/empty/unsupported states, keyboard shortcuts, DPI/resizing and focus states.
- [ ] Compare 1080p and 1440p captures to approved in-game reference layout.
**Exit gate:** complete polished navigation and edit interactions with fixtures; buttons cannot falsely imply dispatch succeeds.

### M5 — Interactive route map, city and depot picking
- [ ] Choose renderer/data source (WPF drawing or embedded offline vector view; benchmark both).
- [ ] Render maps with pan/zoom, labels, depot pins, origin/destination, highlighted selection.
- [ ] Click-to-select valid pickup/destination depots and add/remove/reorder waypoint chips.
- [ ] Implement route solver on verified road data, distance/ETA, reroute/undo, unavailable-road handling.
- [ ] Distinguish `schematic`, `verified preview` and `applied to ATS GPS` readiness in UI.
- [ ] Filter map by installed DLC; prevent unavailable territory from being presented as drivable.
**Exit gate:** deterministic routes and labels match source data; no claim of game GPS control without actual proof.

### M6 — Cargo/route/job editor and presets
- [ ] Two-way bind map selections, freight card selection and inspector fields.
- [ ] Select origin/destination specific depots (not only company+city).
- [ ] Cargo graphics, name, category, units/weight, urgency, expiry, realistic preview and price per mile.
- [ ] Trailer configuration selection, owned-trailer mode and compatibility warnings.
- [ ] Editable payout only when supported; otherwise clearly estimate/lock it.
- [ ] Save/duplicate/rename/reverse/delete draft presets, last-used route history and template from existing offers.
- [ ] Review screen summarizes inputs and preflight before applying to any profile.
**Exit gate:** complete planning flow with zero save writes and end-to-end UI tests.

### M7 — Safe *actual* dispatch implementation
- [x] Add pure dry-run preflight/stage/recovery planning contracts and synthetic safety regressions; see [scope and remaining transaction work](SAVE_SAFETY_PLANNING.md). No profile writes, live environment enforcement or ATS support are implemented by this layer.
- [ ] Build version-specific `IDispatchAdapter` on proven M2 fixture data.
- [ ] Check ATS isn't running, profile isn't stale, and Steam Cloud cannot overwrite staged work.
- [ ] Version/capability checks: block unsupported cargo, payout, depot or waypoint operations.
- [ ] Back up original, checksum, stage copy, validate graph/references, commit atomically and keep receipt.
- [ ] On any failure do not leave a partial profile; offer verified rollback/restore.
- [ ] Return a game job ID and verify it appears correctly inside ATS; never report success early.
- [ ] Handle offer expiry, economy refresh, owned trailer, DLC/mod changes and duplicate generated job IDs.
**Exit gate:** on a disposable profile, 10+ distinct supported test jobs are created, picked up and/or completed without unrecoverable loss; fault-injection and rollback pass. Broader device/profile testing precedes release.

### M8 — Integration hardening and game testing
- [ ] Test several supported ATS versions and owned DLC combinations explicitly.
- [ ] Test unmodded, cargo/trailer-modified and map-modified profiles.
- [ ] Verify pickup/drop depots, cargo/trailer geometry, ETA/waypoint promises, payment and completion.
- [ ] Test power loss/process kill, disk-full, locked save, corrupted file and Steam Cloud collision scenarios.
- [ ] Produce compatibility list, diagnostics report and opt-in bug reporting with privacy redaction.
**Exit gate:** supported matrix passes; known limitations are shown visibly before user dispatches.

### M9 — Production polish and Windows release
- [ ] Accessibility, keyboard/controller feasibility, loading feedback, errors, search responsiveness and high-DPI.
- [ ] Final assets and UI comparison to game-inspired design reference.
- [x] Package a Windows x64 portable planning prototype with bundled runtime, getting-started/build identity files and an automated packaged WPF launch/interaction smoke test. This is not a v1 release.
- [ ] Complete release packaging, legal asset review, signing if possible and real-device install/update/uninstall validation.
- [ ] Add first-run setup, version compatibility checks, backup/recovery workflow, uninstall instructions.
- [ ] Publish release candidate for limited testing, resolve blocker bugs, then tag v1.0.0.
**Exit gate:** a nontechnical tester can install, create a supported ATS job and recover safely without manually editing saves.

### M10 — Post-v1 ongoing maintenance (not a prerequisite for finish)
- [ ] ATS patch compatibility monitoring and adapter gating after game updates.
- [ ] Expand map and mod compatibility, controller navigation, localization, presets and route intelligence.
- [ ] Separate in-game mod/UI integration exploration only if suitable official/legitimate hooks become available.

## Milestone dependency graph

```text
M0 Foundation ── M1 Design ───────┐
       └─────── M2 Integration spike [G1] ─── M3 Catalog ──────────┐
                                        M4 ATS UI shell ─────────────┤
                                        M5 Map/road graph ──────────┤
                                        M6 Editor/presets ──────────┘
                                                  ↓
                                         M7 Safe dispatch
                                                  ↓
                                        M8 Real game QA
                                                  ↓
                                         M9 v1.0 Release
                                                  ↓
                                         M10 Updates
```

M1 and M2 can run in parallel. Some M3–M6 work can run concurrently, but M7 depends on proven M2 and coherent model/UI contracts.

## Work rules for every development pass

1. Read this roadmap, [Product Vision](PRODUCT_VISION.md), [Technical Plan](TECHNICAL_PLAN.md), latest CI status and changed files.
2. Pick the **highest-priority unblocked milestone**; prefer real compatibility/safety over cosmetic polish.
3. Update code and deterministic tests together; run Windows compile and smoke suite through GitHub Actions.
4. On red CI, fix the failure before expanding scope. Do not mark any milestone complete without objective evidence.
5. Use separate branches/draft PRs until relevant acceptance gates pass; don't mix untested game editing into stable releases.
6. Update milestone checkboxes/limitations and publish the next tested deliverable with clear instructions.
7. Keep proprietary game assets and personal saves out of the repository, including CI logs or sample fixtures.

## Highest-priority next work (suggested execution order)

**P0:** M2 fixture collection protocol → SII read-only save graph explorer → reference integrity tests → one legitimate test-job feasibility proof. In parallel M1 capture a current ATS Freight Market reference.

**P1:** Normalize depot/trailer catalog (M3), design data-binding contracts and shell layout (M4).

**P2:** Add verified map data and routing (M5) and finish draft editor/presets (M6).

**P3:** Implement gated live dispatch (M7), game tests (M8) and release (M9).

## Important constraints / definition of truthful behavior

- `cargo_def` mod associations do **not** guarantee an offer with exact selected cities.
- Save format recognition/decoded offer inspection do **not** mean writing correct game jobs is supported.
- Unlicensed SCS road geometry, art and assets cannot be repackaged as original content.
- Not every city/company has a valid pickup/delivery depot; not every cargo fits every trailer, skill or owned DLC.
- A route shown in our application does **not** automatically become the in-game GPS route.
- World of Trucks offers are not first-party custom dispatch jobs and are out of scope for modification.

This roadmap is the authoritative project scope until explicitly revised.
