# ATS Custom Dispatcher — Product Vision & UX Specification

> **North star:** A Windows freight dispatcher that feels like an additional American Truck Simulator job-market screen, with the **map on the left, jobs/cargo on the right, and editing of origins, destinations and routes**. Its job-generation features must be real, verified in-game, and fail safely.

Status: **Product target / design brief**, not evidence of features implemented.

## 1. Reference and visual direction

Reference: [American Truck Simulator on Steam — official screenshots](https://store.steampowered.com/app/270880/American_Truck_Simulator/) (Freight Market interface). The recognizable organizing model is **route map at left; sortable cargo offers at right; selection and GPS action below**. A fresh reference capture from the user's installed ATS build will be the fidelity baseline during UI work; this documentation does not assume every version has identical menus.

**ATS-inspired, clean-room implementation**, not a copied game UI asset pack. Build original icons, thumbnails, textures and controls and use licensed/open assets where necessary. Do not bundle SCS map data, logos, fonts, trailers or screenshots without suitable rights. No SCS affiliation implied.

Visual tokens are **initial targets**, subject to screenshot comparisons:
- Window chrome/background: near-black charcoal \`#13191F\`.
- Raised panels: blue-charcoal \`#202B33\`; panel borders \`#3C4851\`.
- Selected item highlight: muted amber/orange \`#E8A23A\`; focused state needs a distinct visible outline.
- Primary text: \`#F1F3F4\`; secondary text: \`#B2BCC5\`.
- Map: subdued roads, state borders, visible city labels, amber selected route. No false accuracy if using a schematic.
- Typography: licensed system font (e.g., Segoe UI); compact, condensed-feeling headings if legitimately licensed.
- Styling: shallow bevels, understated separators, subtle gradients, icon-led tabs and firm rectangular buttons. **Avoid a bright modern SaaS look**.

Target: readable 1920×1080, 2560×1440 and 1366×768; scalable UI/DPI; keyboard/mouse first. Controller navigation and localization are later polish.

## 2. Primary screen layout

At wide desktop sizes: **map ~55% left / cargo board ~45% right**. An editable details panel appears inside or over the right column after selecting a freight card, without losing the map context.

\`\`\`text
┌─────────────────────────────────────────────────────────────────────────────┐
│ ATS CUSTOM DISPATCHER                      Profile • Mod/DLC • ATS Version  │
├─────────────────────────────────────────────────────────────────────────────┤
│ FREIGHT MARKET    CARGO MARKET    MY PRESETS    CUSTOM DISPATCH             │
├────────────────────────────────┬────────────────────────────────────────────┤
│                                │ SEARCH [cargo, company, city...]  SORT ▾  │
│  INTERACTIVE ATS-STYLE MAP     ├────────────────────────────────────────────┤
│                                │ ▸ Excavator   [cargo image]    $13,450    │
│  ○ Pickup                      │   Rapid City → Denver • Lowboy • 400mi     │
│    ╲                           │────────────────────────────────────────────│
│     ═════ Route ════           │ ▸ Grain       [cargo image]     $8,500    │
│                   ╲            │   Denver → Phoenix • Hopper • 900mi       │
│                    ◎ Drop-off  │────────────────────────────────────────────│
│                                │ CUSTOM JOB EDITOR                          │
│  [+ Waypoint] [Clear]          │ From depot ▾   To depot ▾                 │
│  Distance • ETA • Warnings     │ Cargo ▾  Trailer ▾  Weight ▾  Payout      │
│                                │ Via: [ordered stops]  [Edit route]         │
├────────────────────────────────┴────────────────────────────────────────────┤
│ Status: Validated/Incomplete/Unsupported   [Save preset] [Preview] [Dispatch]│
└─────────────────────────────────────────────────────────────────────────────┘
\`\`\`

This is a **layout concept**, not a current screenshot, route calculation, or live ATS data.

At narrow widths, keep the map and job list in switchable tabs and provide a collapsible detail drawer. Do not shrink actionable controls below usability.

## 3. Finished product interactions

### A. Map-first route editing
1. Click a city or depot → \`Set pickup\` / \`Set delivery\` / \`Add waypoint\`.
2. Pickup and destination pins use distinct shapes; stop numbers show waypoint order.
3. Route previews update with accurate road distance **only when a verified ATS-compatible graph is available**; otherwise display \`schematic / estimated\`.
4. Drag or reorder waypoint chips; recalculate and show detours. Reset only the selected route, never the entire draft without confirmation.
5. Toggle \`Fastest\`, \`Shortest\`, \`Avoid tolls\` **only if backed by supported routing data**. Never display options that pretend to change the game GPS if they don't.

### B. ATS-like Freight/Cargo Market
- Scrollable freight cards with cargo image, cargo name, weight, pickup and destination companies/cities, price, distance, price per mile, urgency/deadline and expiry.
- Search by city, cargo, company and compatible trailer; filters for owned trailer, load category, DLCs and distance.
- Select a real existing job → \`Use as template\`; edit a copy so the original game's offer remains untouched.
- \`Freight Market\` vs \`Cargo Market\` distinction: cargo market uses an owned trailer and therefore has different constraints. Do not mislabel a simulated job as a World of Trucks contract.

### C. Right-side Custom Dispatch editor
- **Pickup**: city + real depot/company + pickup point if available.
- **Destination**: city + real depot/company + drop-off prefab/slot if available.
- **Cargo**: original game cargo ID with friendly name, graphic, weight, units and category.
- **Trailer**: compatible body, chassis/axles, cargo capacity, state/length restrictions, and owned-trailer mode as available.
- **Route**: optional ordered city/depot waypoints, saved route presets and reverse journey.
- **Economy**: estimated rate per mile, proposed payout; editable payout only if the supported dispatch backend can honor it.
- **Review**: total distance, travel time, skills/DLC checks, trailer and depot compatibility, payout, status/warnings.
- **Actions**: \`Save preset\`, \`Preview\`, \`Create job\`, \`Undo/restore\`. The \`Create job\` action is disabled unless all validation and tested-integration gates pass.

### D. Expected one-minute workflow

Open app → auto-detect local ATS profile (with confirmation) → see familiar map and freight list → select heavy machinery → switch destination to Phoenix → click a valid receiver depot → pick lowboy → optionally add Albuquerque waypoint → inspect route + limitations → create backed-up custom job → open ATS → verify offer exists → drive.

If the current ATS version or content prevents a requested route/cargo, the app must say **why**, offer supported alternatives, and never silently substitute another destination.

## 4. Screen inventory

| Screen / view | Purpose | Required for v1? |
|---|---|---|
| Profile/setup wizard | Detect ATS, Steam profile, version, DLC/mod limitations | Yes |
| Freight-market map + cargo list | Familiar dual-pane home screen with search and filters | Yes |
| Custom dispatch inspector | Full editable job details; live, honest validation | Yes |
| Cargo/trailer browser | Search, visual preview, valid cargo/trailer combinations | Yes |
| Route editor | Pickup/delivery pins and re-orderable via-stops | Yes (supported routing only) |
| Presets/history | Repeat routes and favorite cargo setups | Yes |
| Compatibility/backup screen | Review version, content, save and restore options | Yes |
| In-game result / diagnostics | Confirm or troubleshoot job appearance, rollback | Yes |
| Advanced telemetry, multiple profiles, controller support | Nice-to-have | Post-v1 |

## 5. UI states that must be designed
- **Loading**: profile, catalog, map and job list load independently.
- **No profile**: guided setup, browse user-selected path, preview-only mode.
- **No matching depot/cargo**: explicit reason and suggestions.
- **Missing DLC/mods**: show what is absent; never imply it is owned.
- **Unsupported game version / save format**: editor usable as drafts; dispatch disabled.
- **Game running / Steam Cloud conflict**: dispatch disabled with recovery guidance.
- **Route graph unavailable**: schematic city map and explicit \`not navigable in ATS\` warning.
- **Ready**: all checks passed and applicable backend test-verified.
- **Dispatching**: progress stages, backup confirmation, no double-submit.
- **Success**: generated job identity, target profile, verification instructions and backup path.
- **Failure**: no partial writes, clear error and restore button.

## 6. Accessibility and visual quality gates
- Contrast at least WCAG AA for body text where feasible, focus outlines, keyboard tab order and accessible labels.
- No dependence on color alone; selected cargo, map pins, and errors include icons/text.
- 125–200% Windows DPI and adjustable UI scale, no clipped cards.
- Fast search/filter feedback; no scrolling map when mouse is over freight list.
- Compare layout and major interaction states against user-owned ATS screenshot references before release.

## 7. The promise and the honest boundary

The v1 promise is a **real editable dispatch** to supported ATS depot/cargo/trailer combinations, presented in an ATS-inspired UI, backed up and verified in-game.

Not promised without feasibility proof: teleporting to arbitrary addresses, fully arbitrary modded cargo without definitions, editing World of Trucks contracts, replicating proprietary ATS road network/visual assets, or guaranteeing every manually chosen road remains the in-game GPS route after loading.
