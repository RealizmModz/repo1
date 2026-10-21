# Roadmap and release gates

## Stage 0: completed in prototype
- [x] Windows WPF custom haul draft editor
- [x] Origin, destination, cargo, trailer, payout, and ordered via-city waypoints
- [x] JSON draft import/export with validation
- [x] Local save discovery and header classification without modification
- [x] Cross-platform core smoke tests and Windows build/publish workflow

## Stage 1: game-aware catalog (not implemented)
- [ ] Collect ATS save fixtures from a disposable local profile
- [ ] Inspect owned DLCs and available cities, depots and trailers
- [ ] Import game/mod cargo definitions rather than guessing IDs
- [ ] Add user-friendly autocomplete and duplicate/invalid depot rejection

## Stage 2: supported save editing (not implemented)
- [ ] Decode tested save formats with version gates
- [ ] Generate a Freight Market contract with valid save unit references
- [ ] Write to a temporary copy; checksum and restore original on failure
- [ ] Verify the job appears in ATS with in-game smoke tests
- [ ] Reject editing while ATS is open

## Stage 3: routing and polish (not implemented)
- [ ] Route waypoints mapping from supported ATS data
- [ ] Saved presets, history and repeat routes
- [ ] Map UI, distance/payout estimation and trailer compatibility
- [ ] One-click backed-up dispatch and a signed Windows release

## Non-goals for the first release
- Any unsafe binary patching of ATS executable
- Inventing cargo/depots not defined by game or enabled mods
- Assuming user-entered waypoints automatically change ATS GPS

## Verification
- Code has build and smoke-test CI checks.
- Save-file editing must not be advertised as available until end-to-end verified in a compatible ATS version.
