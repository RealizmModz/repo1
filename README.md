# ATS Custom Freight Dispatcher

A Windows .NET 8 WPF desktop prototype for planning custom hauls in American Truck Simulator.

**Status: planning + read-only catalog checks + verified save backups.** This is NOT yet a working in-game job injector. It does not change Freight Market offers, gameplay GPS routes, requested payouts, or ATS saves.

## What works

- Draft a haul: origin/destination city and company, cargo, trailer configuration/body type, optional payout, intermediate city waypoints
- Import/export independent JSON job drafts
- Scan local ATS profile saves (read-only)
- Create a separate SHA-256-verified backup of a selected local save
- Import **already-extracted**, loose SCS game definition files from a folder containing \`def/\`, or from \`def/\` itself
- Suggest cities, companies, cargo, and trailer configurations from imported data
- Run advisory checks on cities, company depots, their inbound/outbound cargo definitions, and cargo/trailer body compatibility
- Windows CI build, publish, and smoke tests, including synthetic fixture definitions

## Run on Windows

1. Open the latest successful [Windows build workflow](https://github.com/RealizmModz/repo1/actions/workflows/ats-dispatcher.yml) and download the \`ats-dispatcher-windows-prototype\` artifact.
2. Extract the ZIP; install the **.NET 8 Desktop Runtime (x64)** if it is not already installed.
3. Launch \`ATS.Dispatcher.App.exe\`.
4. Plan a route and export a draft. Or import extracted game definitions to populate suggestions and run compatibility checks.
5. Select a save under ATS Profile Discovery and click **Back up selected save** if you want to make a separate verified copy.

Build from source using the .NET 8 SDK:

\`\`\`powershell
dotnet run --project src/ATS.Dispatcher.App/ATS.Dispatcher.App.csproj
dotnet run --project tests/ATS.Dispatcher.SmokeTests/ATS.Dispatcher.SmokeTests.csproj -c Release
\`\`\`

## Import format and known limits

The importer accepts loose text \`.sii\` definitions from an **already-extracted** \`def/\` tree:
- \`def/city/*.sii\`
- \`def/cargo/*.sii\` and \`def/cargo/<cargo>/*.sii\`
- \`def/company/<company>.sii\`
- \`def/company/<company>/editor/*.sii\`
- \`def/company/<company>/in/*.sii\`, \`out/*.sii\`

It does NOT directly open \`.scs\` archives. The lightweight text parser does not yet resolve SII inheritance, includes, localization, mod override order or the active game's DLC ownership. A compatible catalog result does not establish that ATS will actually spawn a job, because depot prefab restrictions, cargo skills, geography and more may still prevent it. Waypoints remain a human-readable plan only.

Your game files stay local. Do not upload game save files, license information or Steam Cloud credentials to this repository.

**Important:** Save backups do not alter the original game save, but are not a substitute for testing in-game save changes. Save injection remains disabled until compatible save parsers, safe rollback and end-to-end ATS tests exist.

## SCS references

- https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_data
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_def
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/company_def
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/trailer_def

See [roadmap](docs/ROADMAP.md). Independent community project, not affiliated with SCS Software.
