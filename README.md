# ATS Custom Freight Dispatcher

Windows desktop project for planning custom freight jobs in American Truck Simulator (ATS).

**Status: early prototype / job draft editor.** This build lets you create and import/export custom job drafts (including optional payouts and ordered city waypoints), detects local ATS save slots, and identifies save signatures. **It does not inject jobs into ATS, override the in-game GPS, or unlock all cargo.** Those require game-data integration and compatibility testing.

## First build

- WPF desktop editor: pickup, delivery, cargo, trailer, payout and intermediate city waypoints.
- Drafts saved as human-readable JSON; import them later.
- Read-only search for saves under Documents/American Truck Simulator/profiles and steam_profiles.
- Signature detection for SiiN, ScsC and BSII formats (detection is not decoding).
- Core validation, smoke tests and Windows CI build artifact.
- No modifications to game files. No third-party SCS tools bundled.

## Run on Windows

Install the .NET 8 SDK and build with:

```powershell
dotnet run --project src/ATS.Dispatcher.App/ATS.Dispatcher.App.csproj
```

Smoke tests:

```powershell
dotnet run --project tests/ATS.Dispatcher.SmokeTests/ATS.Dispatcher.SmokeTests.csproj -c Release
```

GitHub Actions builds and uploads a Windows prototype artifact. The published prototype is framework-dependent: the target PC needs the appropriate .NET 8 Desktop Runtime installed.

## Important ATS constraints

ATS Freight Market jobs depend on valid company depots, cargo/trailer definitions, owned map DLCs, game version, and save structure. Merely typing any location into a draft cannot force ATS to offer a job there. Waypoints in this prototype are a plan only, **not injected into the game's route advisor**. A future integration must verify city and company IDs from game data and test on supported save formats.

**Never edit an active game save in place.** Any future save-editing engine must back up and checksum the original file, reject unsupported versions, and use a temp/atomic replacement after validation.

## Next milestone

1. Capture anonymized ATS version and local test profile data.
2. Add compatible cargo, company and trailer catalog import.
3. Build a version-gated job injector against known-good fixture saves.
4. Add route waypoint conversion where ATS game data supports it.
5. Add real in-game validation, failure-safe rollback, and release packaging.

This is an independent community project, not affiliated with SCS Software.
