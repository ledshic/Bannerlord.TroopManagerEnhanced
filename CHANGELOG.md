# Changelog

All notable changes to TroopManagerEnhanced will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Major simplification of the mod to its core two features only:
  - **Automatic Promotion**: Now strictly daily. Only promotes soldiers/stacks that have *full required EXP* standing by (`availableXp / xpCost > 0` using the vanilla `PartyTroopUpgradeModel`). If multiple upgrade paths exist, chooses randomly. Removed frequency modes, selection mode dropdown, multi-tier chaining, wounded skip, etc.
  - **Auto Prisoner Recruit**: Now strictly daily. Explicitly checks conformity using `PrisonerRecruitmentModel.GetConformityNeededToRecruitPrisoner(...)` + the prison roster's XP field (which stores accumulated conformity). Only recruits "stand-by" prisoners + respects party slots. Removed all accelerated / pay-to-recruit / hotkey / bypass logic.
- Removed the three non-core features entirely (and **all** their MCM configuration):
  - Auto Dismiss (low-tier + heavily wounded)
  - Settlement Auto Recruit
  - Accelerated Recruitment (including Ctrl+R hotkey, cost multiplier, conformity bypass, full-stack mode, trigger button)
- Deleted the now-dead manager files: `AutoRecruitManager.cs` and `AutoDismissManager.cs`.
- `TroopManagementBehavior` is now an extremely thin daily-tick only layer (removed hourly tick, game tick, battle-end handlers, and all references to the removed managers).
- `TroopManagerSettings.cs`: Removed `PromotionFrequency` and `PromotionSelectionMode` enums, all related advanced properties, the three removed feature toggles, and the entire Accelerated / Settlement / Dismiss sections. `IsFeatureEnabled` now only recognizes the two kept features.
- `SubModule.cs`: Removed the `OnApplicationTick` accelerated hotkey handler and its `InputSystem` dependency.
- Cleaned up stale strings from language files (EN fully updated; CN/SC/CNs had stale entries pruned where practical). Updated ShowNotifications hint and various feature descriptions.
- Updated README.md and this CHANGELOG to accurately describe the current (much simpler) state of the mod.
- (Previous cleanup items retained for history: per-save settings removal, presets removal, global-only, etc.)

### Removed
- `AutoRecruitManager.cs` and `AutoDismissManager.cs` (dead code after feature removal).
- All code paths, MCM properties, hotkeys, and localization for the three removed features.
- References in source comments/behavior/docs to the removed systems.

### Fixed / Improved
- Promotion logic now correctly respects full EXP requirements instead of promoting from pooled/partial XP.
- Prisoner recruitment now correctly checks and waits for vanilla conformity instead of recruiting any qualifying prisoner.
- Behavior and managers are dramatically simpler and only run on the daily tick (better performance, more predictable "in game" behavior as requested).
- Documentation (README + CHANGELOG) fully refreshed to match the current focused feature set.

## [1.0.0] - Initial Release (pre-cleanup baseline)

- Core features: Automatic Promotion (with smart branch selection, cost multiplier, etc.), Prisoner Recruitment, Accelerated Recruitment (hotkey + button), Settlement Auto Recruit, Auto Dismiss.
- MCM v5 global settings with presets.
- Harmony integration and optional patches (now cleaned up).
- Multi-language support (EN, CN, CNs, SC).
- Build system with `build.ps1` and `.csproj` post-build for `_Module` packaging.
- Designed around Bannerlord 1.4.5 + War Sails (with compatibility helpers).

## Later updates (see [Unreleased] above)
Significant scope reduction and logic fixes occurred after the initial baseline. The mod now focuses exclusively on daily full-EXP promotion (random branch) + daily conformity-checked prisoner recruitment, with the other features and their configuration removed.

[Unreleased]: https://github.com/yourname/TroopManagerEnhanced/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/yourname/TroopManagerEnhanced/releases/tag/v1.0.0
