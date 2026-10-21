# Architecture & integration decisions — ATS Custom Dispatcher

This supplements [Product Vision](PRODUCT_VISION.md) and the [Master Roadmap](ROADMAP.md).

## System boundaries

**Desktop application**: Keep the existing C#/.NET WPF foundation. Refactor the growing MainWindow code-behind into MVVM view-models and separately testable services as part of the shell rewrite. Avoid a mandatory server, account, paid map API or always-on cloud dependency for v1.

**Core components** (interfaces to implement, not current shipped APIs):
- `IInstallationDetector`: locate ATS executables/content directories and record version, profiles and user-approved paths.
- `IProfileSnapshotService`: immutable snapshot of selected profile, save formats, disabled/active mods and content fingerprints; avoid secrets.
- `IGameDefinitionProvider`: normalized city, depot, cargo, trailer and DLC compatibility data; preserve original game IDs and source provenance.
- `IOfferRepository`: import existing game offers read-only, show precise confidence and game version.
- `IMapDatasetProvider`: map style + city/depot locations + available roads, with explicit dataset provenance and accuracy.
- `IRoutePlanner`: road graph traversal, ETA/distance, reorderable waypoints and estimated vs verified path classification.
- `IJobDraftService`: validation, templates, presets, edit history and persistence.
- `IDispatchAdapter`: per-game-version capability flags; `PreviewAsync`, `PreflightAsync`, `ApplyAsync`, `VerifyAsync`, `RollbackAsync`.
- `ISaveTransaction`: exclusive access, backup/checksum, temporary output, atomic replacement, audit log and rollback.

An adapter must expose **capabilities** rather than a single false `Supported` flag:
`CanGenerateOffer`, `CanSetExactDepots`, `CanSetCargo`, `CanSetTrailer`, `CanOverridePayment`, `CanApplyWaypoints`. UI disables features independently.

## Proposed canonical data model

```text
ProfileSnapshot
  profileId, atsVersion, gameContentFingerprint, activeMods[]
  installedDlc[], saveFormat, isCloudEnabled, capturedAt

CatalogSnapshot
  cities[], companies[], depotInstances[], cargo[], trailerConfigurations[]
  cargoProducerRules[], cargoReceiverRules[], restrictions[], provenance[]

JobDraft
  id, name, sourceOfferId?, profileFingerprint?
  originDepotId, destinationDepotId, cargoId, trailerConfigurationId?
  weight/units, priceMode, requestedPayout?, waypoints[]
  routingStrategy, lastValidation, createdAt, updatedAt

JobValidation
  errors[], warnings[], versionSupport, depotCompatibility
  cargoCompatibility, trailerCompatibility, routeSupport
  priceEditable, backupEligible

RoutePlan
  pickupNode, deliveryNode, orderedViaNodes[]
  graphSource, accuracy, roadSegments[], totalDistance?, eta?
  canApplyToAtsGps

DispatchReceipt
  draftId, adapterId, atsVersion, originalChecksum, backupPath
  outputChecksum, appliedOfferId?, verification, timestamp, rollbackState
```

**Don't** reduce a depot to company+city alone when ATS distinguishes multiple depots or prefabs; use actual game-specific IDs after validation. Don't label geographical straight-line distance as a drivable route.

## Integration decision — spike this early

**Option A: independent .scs definitions** (already experimental). Pros: supported mod packaging route; can alter some cargo eligibility. Cons: cargo_def operates at company scope, may not select precise city, cannot guarantee a freight offer, GPS routing or payout. Keep a distinct `Cargo Availability Mod` feature, not the real dispatcher.

**Option B: tested save-game dispatch adapter** (candidate for v1). Pros: potential to create exact job with real game IDs. Cons: unofficial format compatibility, expiry/time and reference integrity, Steam Cloud interactions, game version upgrades, unsupported saves. Requires fixture-backed parser, strict version gates, never edit live saves, and in-game validation.

**Option C: supported official runtime APIs (if any suitable extension becomes available)**. Investigate, but do not mistake the official telemetry SDK for a documented freight job creation API; it is mainly telemetry/events. Never memory-patch or inject native code to bypass game integrity.

**Decision Gate G1**: Before committing to the final route-map/rendering investment, demonstrate at least **one** reliable end-to-end *custom* origin/destination/cargo job on a disposable ATS profile, including rollback, or explicitly adopt a plan-only first release and revise the product promise.

## Map engine feasibility

A screenshot-like map is a **visual design goal**. True editable ATS routing needs a legal, maintainable road graph + projection / node map and mod-aware data source.

Three readiness modes:
1. `Schematic`: city pins and straight/curved approximations; **clearly labeled**, no mileage/guidance claims.
2. `Verified Route Preview`: accurate road graph for supported ATS content, routing/search/camera; report dataset and restrictions.
3. `Game-Navigation Applied`: only when evidence proves the selected waypoints actually persist in ATS GPS after loading.

Prefer offline rendering with an interchangeable map provider; WPF custom drawing or WebView2 with a local SVG/vector renderer are candidates to prototype. No automatic scraping or repackaging of SCS maps. Evaluate license and technical limits of any data extraction/renderer.

## Safe dispatch transaction (future, not yet implemented)

A pure `SaveSafetyPlanner` now models preflight, backup/stage hashes, fresh-input rechecks and conservative recovery review. It has no file mutation API, trusted live observations or production dispatch adapter. See [implemented boundary and required remaining work](SAVE_SAFETY_PLANNING.md). This does not pass G1 or complete M7.


```text
DRAFT
  ↓ validate selected depot/cargo/trailer/map/profile/version
PREFLIGHT
  ↓ verify game is closed; reject Steam Cloud collision / unsupported save
BACKUP
  ↓ create independently stored, checksum-verified original snapshot
STAGE
  ↓ write/modify only temporary copy in same filesystem
VALIDATE
  ↓ parse staged data; integrity/reference/version checks
COMMIT
  ↓ atomic replacement + record receipt (if safe to do so)
VERIFY
  ↓ confirm created offer appears in game on same profile
SUCCESS  |  FAILURE → RESTORE VERIFIED BACKUP
```

No project may advertise "one-click dispatch" until every step is exercised under simulated failure and actual ATS on test profiles. Since ATS save data may be concurrently rewritten by Steam Cloud, explicit cloud restrictions and profile checks are critical. Backups must never be automatically deleted after a failure.

## Test matrix and release gates

- Windows desktop: Win 11 64-bit, DPI 100/125/150/200%, keyboard/mouse, common resolutions, dark-theme contrast.
- ATS versions: explicit supported build numbers only, regression checked after each patch. All unknown versions fail closed for dispatch.
- DLC: core game, multiple owned maps, no missing depots; detect the active content rather than assuming all states/cargo available.
- Mods: unmodded profile, cargo mod, trailer mod, map mod; honest incompatibility errors.
- Saves: plain SII, encoded/compressed, malformed, truncated, stale, Steam Cloud, duplicate id and concurrent writes.
- End-to-end: create → select offer → drive → deliver → compensation and records; verify custom pickup/delivery coordinates/depots.
- Route cases: 0, 1 and many waypoints; reordering, graph missing, impossible roads, state boundaries, misestimated ETAs.
- Fault injection: disk full, permission denied, game starts mid-write, stale checksum, process crash, backup corruption, rollback.
- CI: deterministic unit/integration fixtures, WPF compilation, security scanning and downloadable unsigned test artifacts.
- Manual release: SCS asset/license review, documentation, uninstall/restore guide, feedback/issue intake and signed executable where viable.

## Explicit exclusions for v1

- Editing contracts controlled by World of Trucks.
- Modifying game executable/memory.
- Shipping proprietary SCS assets as our own.
- Inventing deliveries outside actual accessible prefabs.
- Cloud service requirements, paid subscriptions, multiplayer server integration.

## Research references

- [SCS cargo_def](https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_def) — assigning produced/accepted cargo to companies
- [SCS cargo_data](https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_data) — cargo metadata, skills, limits and probabilities
- [SCS trailer_def](https://modding.scssoft.com/wiki/Documentation/Engine/Units/trailer_def) — mass, dimensions, country validity and prefab constraints
- [SCS modding documentation](https://modding.scssoft.com/wiki/Documentation) — official tools and formats
