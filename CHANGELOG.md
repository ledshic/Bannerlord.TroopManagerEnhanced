# Changelog

All notable changes to TroopManagerEnhanced will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Major cleanup: Removed legacy content and the entire per-campaign/per-save configuration interface (`TroopManagerPerSaveSettings` class, related `IsFeatureEnabled` logic, `GetEffectiveMaxPromotionsPerCheck`, all associated MCM settings, and strings in all languages).
- All settings are now strictly global-only. Removed "(Global)" suffixes from feature toggle names for clarity.
- Refactored `TroopManagementBehavior` into a thin orchestration layer:
  - Extracted settlement auto-recruit logic into new `AutoRecruitManager.cs`.
  - Extracted auto-dismiss logic into new `AutoDismissManager.cs`.
  - Improved early-exit condition in `OnDailyTick` for readability.
- Cleaned up `PromotionPatches.cs`: Removed non-functional dead code and example patches. Left a minimal, honest placeholder for advanced users only.
- Updated version/compatibility references throughout (moved away from hard 1.4.5 + War Sails focus toward broader e1.2+ compatibility notes). Made naval/ship party guards more general.
- Replaced placeholder Harmony ID with a clean `TroopManagerEnhanced` identifier.
- Improved `GameFolder` handling and documentation in `.csproj` and README for better cross-platform (Windows/macOS) support.
- Added `AcceleratedRecruitmentHotkeyEnabled` setting (with localized strings) so users can disable the Ctrl+R hotkey.
- Added explanatory comment for the common MCM "action button via bool property" pattern used by Force/Trigger buttons.
- Reduced commented "future/optional/example" cruft across source files.
- Updated `.gitignore` to properly exclude generated `_Module/bin/` and `_Module/ModuleData/`.
- Purged legacy build outputs and duplicated ModuleData from the repository.

### Added
- `AutoRecruitManager.cs` and `AutoDismissManager.cs` for better separation of concerns and consistency with other feature managers.
- This `CHANGELOG.md`.
- Basic hotkey toggle for accelerated recruitment.

### Fixed / Improved
- Behavior class documentation and structure now clearly shows it as orchestration only.
- Folder structure documentation in README updated to reflect new managers.
- Build-related comments and examples modernized.

## [1.0.0] - Initial Release (pre-cleanup baseline)

- Core features: Automatic Promotion (with smart branch selection, cost multiplier, etc.), Prisoner Recruitment, Accelerated Recruitment (hotkey + button), Settlement Auto Recruit, Auto Dismiss.
- MCM v5 global settings with presets.
- Harmony integration and optional patches (now cleaned up).
- Multi-language support (EN, CN, CNs, SC).
- Build system with `build.ps1` and `.csproj` post-build for `_Module` packaging.
- Designed around Bannerlord 1.4.5 + War Sails (with compatibility helpers).

[Unreleased]: https://github.com/yourname/TroopManagerEnhanced/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/yourname/TroopManagerEnhanced/releases/tag/v1.0.0
