# Roadmap and release gates

## Stage 0 — job planner (completed in prototype)
- [x] WPF desktop route/cargo/trailer/pay draft form
- [x] Optional pickup/delivery company fields
- [x] JSON draft import/export
- [x] Read-only local save discovery and SiiN / ScsC / BSII signature detection
- [x] Windows build, publishing and smoke testing in GitHub Actions

## Stage 1 — extracted game definitions (initial implementation)
- [x] Read loose .sii city, cargo, company and trailer definitions
- [x] Import origin/destination company-city associations and in/out cargo
- [x] Populate choices from the imported catalog
- [x] Advisory validation for matching city, company, cargo and trailer; waypoint lookup
- [x] Synthetic fixture tests for compatible/incompatible jobs
- [ ] Interpret full SII format (includes, inheritance, locale and override semantics)
- [ ] Cross-check true in-game mod load order, owned map DLC, and cargo skills
- [ ] Validate unique depot prefab constraints, allowed length and trailer state restrictions
- [ ] Compare importer output with an up-to-date real ATS installation

## Stage 2 — save safety and injection (partially started)
- [x] Create independent, checksum-verified copies of selected ATS saves
- [x] Include read-only format signature classification for SiiN, ScsC, BSII and 3nK
- [x] Parse existing `job_offer_data` from a separately decoded SiiNunit copy
- [x] Copy real offer cargo/trailer references into JSON draft template metadata
- [x] Test malformed/encoded input rejection and unchanged source saves
- [ ] Gather anonymized disposable test saves with game version and active mods recorded
- [ ] Decode known ScsC/BSII save versions, fail closed on unsupported data
- [ ] Generate a real Freight Market job using proven cargo/trailer/company unit references
- [ ] Detect running ATS, refuse edits, atomically write validated copy, and support rollback
- [ ] Verify in game that job appears, starts, drives, and can be completed

## Stage 3 — routes and release (not started)
- [ ] GPS waypoint integration where supported by ATS save data
- [ ] Road map route UI with valid road graph
- [ ] Saved presets and repeat jobs
- [ ] Packaged release and usability testing

**Release gate:** No claim of working custom freight dispatch until in-game verification succeeds.
