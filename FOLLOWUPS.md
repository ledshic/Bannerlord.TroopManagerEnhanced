# TroopManagerEnhanced - Follow-ups & Roadmap

**Generated**: 2026-06-11  
**Context**: After major cleanup pass that removed legacy content and the entire per-campaign / per-save configuration interface (`TroopManagerPerSaveSettings` + related logic, strings, and UI).

This document lists all identified useful follow-up work. Items are prioritized by impact vs. effort. All tasks are actionable via direct code/docs/build changes.

---

## High Priority (Do These First)

These deliver the most immediate value with relatively low risk after the recent simplifications.

| ID | Task | Why Useful | Effort | Affected Files |
|----|------|------------|--------|----------------|
| HP-1 | Remove "(Global)" suffixes from feature toggle display names and hints | Everything is now strictly global-only. The old labels are now misleading and add visual noise in MCM. | Low | `ModuleData/Languages/*/sta_strings.xml` (all 4) |
| HP-2 | Replace placeholder Harmony ID (`com.yourname.troopmanagerenhanced`) | Template leftover. Can cause ID collisions if multiple mods use the same placeholder. Should be a stable reverse-domain identifier. | Low | `src/SubModule.cs` |
| HP-3 | Make `<GameFolder>` in .csproj portable and improve documentation | Hard-coded Windows `D:\Steam\...` path breaks builds for other developers, macOS/Linux users, and CI. Common source of friction for Bannerlord mod projects. | Low-Medium | `TroopManagerEnhanced.csproj`, `README.md` |
| HP-4 | Decide fate of `PromotionPatches.cs` (mostly dead/example code + unused `SuppressVanillaPlayerUpgrades`) | The file is full of commented examples and a public static flag that is never wired to settings or used meaningfully. Either expose it as an Advanced/Debug toggle (and make the patch actually do something) **or** remove it + related comments to reduce confusion and dead weight. | Medium | `src/PromotionPatches.cs`, `src/SubModule.cs`, `README.md`, language strings |
| HP-5 | Update all version + compatibility target references (still heavily references 1.4.5 + War Sails) | The mod advertises outdated targets in code comments, docs, and module definition. Clean this up and settle on a realistic supported range going forward. | Low-Medium | `README.md`, `TroopManagerEnhanced.csproj`, `_Module/SubModule.xml`, multiple `*.cs` comments |
| HP-6 | Make Accelerated Recruitment hotkey (Ctrl + R) configurable via MCM (or at least toggleable) | Hard-coded hotkey hurts discoverability and user control. Natural improvement now that the settings surface is simpler (no more per-save complexity). | Medium | `src/SubModule.cs`, `src/TroopManagerSettings.cs`, new strings in all languages |

---

## Code Quality & Architecture

- Extract `PerformAutoRecruit()` and `PerformAutoDismiss()` from `TroopManagementBehavior` into dedicated manager classes (consistency with `PromotionManager` / `RecruitmentManager`).
- Clean up the awkward chained early-exit condition in `OnDailyTick` that uses multiple `!IsFeatureEnabled(...)` calls.
- Audit and remove (or properly finish) large blocks of commented "future", "optional", "example" code scattered across `SubModule.cs`, `PromotionPatches.cs`, and managers.
- Review the "Force XXX Now" button implementation pattern (the private bool toggle trick used in settings). Consider a small helper or better documentation.
- Reduce duplication in tick handling + throttling logic between the behavior and individual managers.

## Build, Packaging & Developer Experience

- Improve `build.ps1` (or add a cross-platform companion):
  - Auto-sync project version into `_Module/SubModule.xml`
  - Better cleaning of `_Module`
  - Proper Release packaging flow
- Strengthen the `CopyToModule` post-build target in the .csproj (make it more robust, conditional, and cross-platform friendly).
- Expand `.gitignore` coverage if needed (we already added rules for `_Module/bin/` and `_Module/ModuleData/`).
- Add a "release-prep" or "package" target/step that produces a clean, ready-to-upload module folder.
- Consider using MSBuild properties or environment variables for `GameFolder` more elegantly.

## Settings / MCM Polish (Post Per-Save Removal)

- Re-evaluate group structure and ordering now that the old "Legacy" section is gone.
- Consider adding an "Advanced" or "Debug" settings group (ideal home for the vanilla upgrader suppression toggle if kept).
- Review whether any remaining setting names or hints still reference outdated concepts.

## Documentation & Project Maintenance

- Create / maintain `CHANGELOG.md` (highly recommended after structural changes like per-save removal).
- Update `README.md` with:
  - Current list of hotkeys
  - Exact recommended load order
  - Clear statement about "global-only" configuration
  - Build instructions that work on macOS
  - Known limitations
- Keep the four localization files (`EN`, `CN`, `CNs`, `SC`) in sync after string changes. Consider adding a note or small helper.
- Add a lightweight project guidance file (e.g. `AGENTS.md` or `DEVELOPMENT.md`) if AI-assisted development will continue.
- Fix minor inconsistencies between dependency notes in `README.md` vs `_Module/SubModule.xml` (MCM vs MBOptionScreen).

## Features & Robustness (Nice-to-Have)

- Add a setting to also apply auto-management to other clan parties (not just `MainParty`). The defensive `IsPlayerLandParty` logic already exists as a starting point.
- Improve notification behavior (reduce spam, use summary messages, add cooldowns).
- Implement real `OnGameLoaded` / `OnNewGameCreated` handling (currently only placeholder comments) for future-proofing.
- Make Accelerated Recruitment optionally run on a schedule in addition to manual trigger.
- Add a configurable hotkey (or at least a disable switch) — overlaps with HP-6.

## Lower Priority / Out of Scope for Now

- Full UIExtenderEx integration (in-screen buttons, mixins) — significant new surface area and testing.
- Re-introducing any form of per-save/per-campaign settings (explicitly removed).
- Large new gameplay features (troop templates, AI party support, settlement recruit pool draining, etc.) without a clear product direction from the maintainer.
- Comprehensive automated test suite (expensive in the Bannerlord modding environment; manual verification + build checks are more practical).
- Multiplayer support (module is explicitly singleplayer-only).

---

## Notes

- Many of the high-priority items became simpler or more obvious after the legacy + per-campaign removal.
- The project is currently in a much cleaner state: only global settings, minimal `_Module/` (just the manifest), sources under `src/`, and generated build outputs properly ignored.
- When working on string changes, remember to update **all four** language files for consistency (`EN`, `CN`, `CNs`, `SC`).

## Progress (as of latest session)

**Completed High Priority items**:
- **HP-1**: Removed all "(Global)" suffixes from toggle names in EN, CN, CNs, and SC localizations.
- **HP-2**: Replaced placeholder Harmony ID with a clean `TroopManagerEnhanced` identifier.
- **HP-3**: Improved `GameFolder` documentation in .csproj (added override examples for Windows/macOS) and README.
- **HP-4**: Cleaned up `PromotionPatches.cs` — removed non-functional dead code and example patches. Left a minimal, honest placeholder with clear warning.
- **HP-5**: Updated version targets, compatibility notes, and "War Sails / 1.4.5" references in README, .csproj, SubModule.xml, and source comments. Made naval-party guards more general.

**High Priority Progress**:
- **HP-6**: Basic implementation complete — added `AcceleratedRecruitmentHotkeyEnabled` setting (default: on) under the Accelerated Recruitment group. The Ctrl+R hotkey now respects this toggle. Full key remapping would require more advanced input handling.

---

**High Priority section is complete.**

**Next tier**: Code Quality & Architecture (and then Build/DevEx).

---

## Current Progress - Next Tier (Code Quality & Architecture)

**Completed in this session**:
- **CQ-1**: Extracted `PerformAutoRecruit` + `PerformAutoDismiss` (including helper) into new dedicated managers:
  - `src/AutoRecruitManager.cs`
  - `src/AutoDismissManager.cs`
  Behavior is now a thin orchestration layer (consistent with Promotion/Recruitment managers).
- **CQ-2**: Cleaned the awkward chained `!IsFeatureEnabled` early-exit in `OnDailyTick` into a clear `anyDailyFeature` boolean. Removed redundant per-feature flag checks in call sites.
- **CQ-3**: Removed several "future expansion ideas", commented UIExtender examples, and lifecycle override TODOs. Kept only useful high-level comments.

**Code Quality & Architecture tier progress**:
- CQ-1, CQ-2, CQ-3, CQ-4: **Completed** in this session (see details above).
  - Major structural win: Behavior is now pure orchestration.
  - New files: `AutoRecruitManager.cs` and `AutoDismissManager.cs`.
  - Early-exit logic and commented cruft cleaned.
  - Added explanatory comment for the MCM "button via bool property" pattern.

## Build, Packaging & DevEx Tier (Current - just completed)

**Completed**:
- **BT-3**: Created comprehensive `CHANGELOG.md` covering the high-priority cleanup (per-save/legacy removal), code quality refactor (new managers), and other improvements.
- **BT-1**: Major improvements to `build.ps1`:
  - Added automatic version sync step (reads `<Version>` from `.csproj` and updates `SubModule.xml` — single source of truth).
  - Expanded Step 1 cleaning to also purge stale `ModuleData` from `_Module/`.
  - Updated all step numbers, header comments, and process description.
  - Added explicit note about running with `pwsh` on macOS/Linux for cross-platform support.
- **BT-2**: Enhanced `.csproj` post-build target with clearer comments (notes that `build.ps1` is the recommended path for final packaging + version sync).
- **BT-4**: Rewrote the entire "Building" section in `README.md` to document the improved workflow, `build.ps1` usage (including macOS), and link to `CHANGELOG.md`.

**Remaining nice-to-haves**:
- Auto-include version in the output folder name (e.g. `TroopManagerEnhanced-v1.1.0`).
- Add a lightweight `build.sh` wrapper or `dotnet` script for users avoiding pwsh.
- Bump the version in `.csproj` after major changes and produce a test package.

Run `dotnet build` or `./build.ps1` (with pwsh on mac) to test. You will need to set `GameFolder` to a real Bannerlord install.