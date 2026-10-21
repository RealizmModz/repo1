# ATS Custom Freight Dispatcher

> **End goal:** An ATS-style Freight/Cargo Market with interactive map on the left, searchable cargo/job list on the right, and editable pickup, destination, trailer and route before **verified** in-game dispatch.

**Project direction:** [Master start-to-finish roadmap](docs/ROADMAP.md) · [Finished UI/product vision](docs/PRODUCT_VISION.md) · [Architecture and safety plan](docs/TECHNICAL_PLAN.md)


A Windows .NET 8 WPF desktop prototype for planning custom hauls in American Truck Simulator.

**Status: ATS-inspired UI shell + planning + read-only offer inspection/diff + catalog checks + verified save backups + experimental cargo-flow mod export.** This is NOT yet a working in-game job injector. It does not change Freight Market offers, gameplay GPS routes, requested payouts, or ATS saves.

**Default window:** a new ATS-inspired Freight Market with schematic route visualization on the left, search/filterable cargo cards on the right, editable pickup/destination/cargo/trailer fields, reverse route, independent JSON drafts and preview validation. Initial cards are labeled **DEMO • NOT IN GAME**. Importing a separately decoded SII save replaces them with real saved job-offer references (still read-only). The graphic is a **schematic, not an ATS road map**; displayed distance from existing offers belongs to the original job, not the edited route.

## What works

- Draft a haul: origin/destination city and company, cargo, trailer configuration/body type, optional payout, intermediate city waypoints
- Import/export independent JSON job drafts
- Scan local ATS profile saves (read-only)
- Create a separate SHA-256-verified backup of a selected local save
- **Inspect a decoded copy of `game.sii`** to read existing `job_offer_data` entries, including destination company/city tokens, cargo, trailer variant, trailer definition, units and distances
- Choose an existing offer as a template to populate draft cargo and trailer fields; retain reference unit metadata when exporting/importing the draft
- Read-only **Compare two decoded saves** action with added/removed/modified freight-offer unit summary (see [fixture protocol](docs/M2_SAVE_FIXTURES.md))
- Use **Advanced tools** to open the earlier utility window for backup and experimental mod export
- Import **already-extracted**, loose SCS game definition files from a folder containing `def/`, or from `def/` itself
- Suggest cities, companies, cargo, and trailer configurations from imported data
- Run advisory checks on cities, company depots, their inbound/outbound cargo definitions, and cargo/trailer body compatibility
- Export an **experimental cargo-flow .scs** package that associates existing cargo with selected shipping/receiving companies; never automatically installs or enables it
- Windows CI build, publish, and smoke tests, including synthetic fixture definitions

## Run on Windows

1. Open the latest successful [Windows build workflow](https://github.com/RealizmModz/repo1/actions/workflows/ats-dispatcher.yml) and download the `ats-dispatcher-windows-prototype` artifact.
2. Extract the ZIP; install the **.NET 8 Desktop Runtime (x64)** if it is not already installed.
3. Launch `ATS.Dispatcher.App.exe`.
4. Plan a route and export a draft. Or import extracted game definitions to populate suggestions and run compatibility checks.
5. Select a save under ATS Profile Discovery and click **Back up selected save** if you want to make a separate verified copy.
6. For real freight reference information, use **Inspect decoded save** and select a separate `SiiNunit` text copy. Choose an offer and click **Use offer as draft template**. **Do not feed encrypted game.sii directly to this inspector.**

Build from source using the .NET 8 SDK:

```powershell
dotnet run --project src/ATS.Dispatcher.App/ATS.Dispatcher.App.csproj
dotnet run --project tests/ATS.Dispatcher.SmokeTests/ATS.Dispatcher.SmokeTests.csproj -c Release
```

## Experimental cargo-flow mod export

With an extracted definitions catalog loaded, enter an origin and destination company that exist at the selected cities, choose an existing cargo and compatible trailer type, then choose **Export cargo-flow test mod (.scs)**. The exporter writes a standalone ZIP-format SCS archive with:

- `manifest.sii` (visible in Mod Manager)
- `description.txt` (explains the limitations)
- `def/company/<origin>/out/<cargo>.sii`
- `def/company/<destination>/in/<cargo>.sii`

The SII cargo associations use the official `cargo_def` pattern. The exporter validates company/city IDs, existing cargo, a compatible trailer type and safe file names, and refuses to overwrite a previous mod.

**Not a custom-job generator:** This may expand the companies' eligible cargo, but ATS can generate freight between other locations served by those same companies; it does not create an offer between the exact cities or modify route waypoints, payment or trailers. Economy regeneration and compatible DLC/mod order are still necessary. Only test in a disposable ATS profile after making an independent backup and enabling the `.scs` yourself in Mod Manager. The game's in-game behavior has not been verified.

## Working with decoded save copies

ATS saves are often encoded as `ScsC`, `BSII` or `3nK`, which this application detects but does **not** decode. The inspector only accepts separately decoded text beginning with `SiiNunit`. Use a trusted decoder independently, passing an explicit input and a **different output path**—do not decode an original ATS save in place.

For example, the independent [SII-Decrypt-cpp](https://github.com/liam-dong/SII-Decrypt-cpp) documents the form `SII_Decrypt.exe -i input.sii -o decoded.sii`. Make a verified backup first, and check that the decoder you download is trustworthy. The project does not ship or automatically execute third-party binaries.

The read-only diff tool compares offer unit IDs and key properties; unrelated economy refresh/expiry can create changes and a matched ID is not sufficient for job-injection support. The reader recognizes `job_offer_data` blocks and stores a limited set of relevant reference fields. It does not parse any other profile information, never writes the save, and rejects encrypted/binary input or plaintext files over 256 MiB. If you import a template, any displayed destination text is only resolved when the matching company and city IDs exist in the imported definitions. **Copying an offer template does not create a new job in ATS.** It only improves planned cargo/trailer choices.

## Import format and known limits

The importer accepts loose text `.sii` definitions from an **already-extracted** `def/` tree:
- `def/city/*.sii`
- `def/cargo/*.sii` and `def/cargo/<cargo>/*.sii`
- `def/company/<company>.sii`
- `def/company/<company>/editor/*.sii`
- `def/company/<company>/in/*.sii`, `out/*.sii`

It does NOT directly open `.scs` archives. The lightweight text parser does not yet resolve SII inheritance, includes, localization, mod override order or the active game's DLC ownership. A compatible catalog result does not establish that ATS will actually spawn a job, because depot prefab restrictions, cargo skills, geography and more may still prevent it. Waypoints remain a human-readable plan only.

Your game files stay local. Do not upload game save files, license information or Steam Cloud credentials to this repository.

**Important:** Save backups do not alter the original game save, but are not a substitute for testing in-game save changes. Save injection remains disabled until compatible save parsers, safe rollback and end-to-end ATS tests exist. Reference offers can expire or contain modded content; they do not guarantee the same job can be regenerated.

## SCS references

- https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_data
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/cargo_def
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/company_def
- https://modding.scssoft.com/wiki/Documentation/Engine/Units/trailer_def

See [roadmap](docs/ROADMAP.md). Independent community project, not affiliated with SCS Software.
