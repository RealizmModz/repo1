ATS DISPATCHER — START HERE

This is a portable Windows x64 planning prototype. It does not create playable
jobs in ATS yet. You can try the editor without opening ATS or selecting a profile.

OPEN IT
1. Download ats-dispatcher-windows-portable from the linked successful GitHub
   Actions run. GitHub may require you to sign in to download its artifact.
2. In File Explorer, extract the ENTIRE downloaded ZIP to a folder you own.
   Do not run the executable from inside the ZIP or move it away from its files.
3. Double-click ATS.Dispatcher.App.exe. The .NET desktop runtime is included.
   No installer, administrator permission or separate .NET installation is needed.
4. Keep START-HERE.txt and BUILD-INFO.txt. BUILD-INFO identifies the exact commit.
   This prototype is unsigned. If Windows blocks it, stop and report the exact
   message rather than changing your security settings.

FIRST FIVE MINUTES
1. Select a demo freight card (explicitly marked DEMO / NOT IN GAME).
2. Edit pickup city, destination, cargo and trailer. Map labels follow the draft.
3. Enter optional waypoint cities, one per line. The drawing is schematic;
   it does not calculate real road distance or change ATS GPS.
4. Preview / validate checks the draft. A passing preview is not game compatibility.
5. Save draft JSON to a planning folder OUTSIDE your ATS profile. Import draft
   opens it later. Draft files are independent plans, not ATS save files.
6. Use the Getting started button for the in-app reminder.

OPTIONAL READ-ONLY OFFER RESEARCH
Import decoded offers only accepts a separately decoded UTF-8 SiiNunit save COPY.
The app does not decode encoded game.sii files. Do not decode an original in place.
Offer reference report shows exact reference tokens, copied-source hash, observed
unit headers and UNVERIFIED references. Not finding a header does not prove a
bad reference: it may be defined in external game data or unsupported syntax.
Finding a header also does not prove a valid playable job.
Compare two decoded saves refuses incomplete or capped observations.

Advanced tools offers local profile discovery and separate verified backups.
Import game definitions accepts a folder of already-extracted loose def files;
it does not open .scs archives or establish complete active DLC/mod compatibility.
Experimental cargo availability mod export is not exact custom-job creation.

WHAT IS NOT AVAILABLE YET
Create ATS Job stays disabled. Exact depot/cargo job generation, actual ATS road
routing, game GPS application and requested payment enforcement are unproven.
No app action writes to ATS save files. No game archives or decoder are bundled.
Do not use valuable career saves for experiments.

UPDATE / REMOVE
Extract a newer package into a NEW folder. Keep your separately saved draft JSON.
Close the app before removing its extracted folder. This portable package does
not install itself into ATS, add a game mod automatically or change your profile.

WHAT TO REPORT
Include the commit and Actions run from BUILD-INFO, your Windows version and
display scale, the action attempted, and the error text. Do not upload player
saves, Steam credentials or proprietary game archives. Automated WPF startup
checks are not a substitute for testing on your PC or inside ATS.

Project: https://github.com/RealizmModz/repo1/pull/1
