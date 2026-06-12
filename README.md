# Bannerlord.TroopManagerEnhanced

A quality-of-life mod for Mount & Blade II: Bannerlord that automates common troop management tasks for the player's party.

**Target**: Bannerlord 1.2.12+ (e1.2 branch) and later.

## Features (all configurable via MCM)

### Automatic Promotion
- Periodically promotes eligible troops in your main party using vanilla `IPartyTroopUpgradeModel` (correct XP + gold costs).
- Multiple upgrade path selection strategies: Vanilla First, Random, Prefer Player Culture, Highest Tier, Lowest Cost.
- Configurable cost multiplier, gold reserve floor, max promotions per check, skip wounded, multi-tier chaining, etc.
- Smart, non-cheaty behavior that follows the game's own troop trees and models.

### Auto Prisoner Recruit + Accelerated Recruitment
- Auto-recruits qualifying prisoners.
- "Accelerated" (pay with gold based on daily wage) recruitment with hotkey support (default Ctrl+R).
- Many tuning options (tier thresholds, conformity bypass, full-stack mode...).

### Settlement Auto Recruit & Auto Dismiss
- Fills your party with basic recruits from the current settlement's culture when below target %.
- Cleans up excess low-tier or heavily wounded troops when near capacity.

All actions are optional, throttled, and produce (optional) notifications.

## Dependencies (load these **before** this mod)

- Bannerlord.Harmony
- Bannerlord.ButterLib (recommended)
- Bannerlord.UIExtenderEx (recommended)
- Bannerlord.MCM (Mod Configuration Menu) v5+

## Installation

1. Install the dependencies above (Workshop or Nexus).
2. Download the latest `Bannerlord.TroopManagerEnhanced-*.zip`.
3. Extract the `Bannerlord.TroopManagerEnhanced` folder into `Modules/`.
4. Enable in Launcher and place it **after** the MCM/Harmony entries in load order.
5. Start a campaign. Open **Mod Options** (ESC → Mod Options) to configure under "Troop Manager Enhanced".

## Load Order (example)

1. Native
2. SandBoxCore
3. Sandbox
4. StoryMode (optional)
5. Bannerlord.Harmony
6. Bannerlord.ButterLib
7. Bannerlord.UIExtenderEx
8. Bannerlord.MBOptionScreen (MCM)
9. **Bannerlord.TroopManagerEnhanced**
10. Everything else

## Localization (l10n)

Full support for both:

- **MCM UI**: All setting names, group headers, hints, dropdown descriptions, and presets use `{=TME_...}` keys and are translated via `ModuleData/Languages/`.
- **In-game messages**: Promotion, recruitment, dismissal, and error notifications are localized.

**Included**:
- English (complete)
- 简体中文 (Simplified Chinese) – good coverage

Additional languages: add a new folder under `ModuleData/Languages/<ISO>/` with `sta_strings.xml` + `language_data.xml` following the existing pattern. PRs welcome!

## MCM Settings

After loading a campaign, go to **Mod Options**. Everything lives under **Troop Manager Enhanced**.

Settings are **global** (JSON) — not per-save.

There are built-in presets (Balanced / Aggressive / Conservative) plus many individual toggles and numeric options. "Force ... Now" toggles act as one-shot buttons.

## Building from Source (Unified Layout)

All three mods in this collection now share the same development structure:

```
dev/
├── build.ps1
├── module/
│   ├── SubModule.xml          (uses __VERSION__)
│   └── ModuleData/Languages/...
└── src/
    └── Bannerlord.TroopManagerEnhanced/
        ├── Bannerlord.TroopManagerEnhanced.csproj
        └── *.cs
```

From the mod root:

```powershell
# Windows
.\dev\build.ps1 -Version v1.2.0

# macOS / Linux (PowerShell Core)
pwsh ./dev/build.ps1 -Version v1.2.0
```

Outputs:
- `out/Bannerlord.TroopManagerEnhanced/` (ready module)
- `out/Bannerlord.TroopManagerEnhanced-v1.2.0.zip`

The csproj uses direct game references (configure `GameFolder` or the GAMEFOLDER env var). See the csproj header for examples.

## Development Notes

- Core logic lives in dedicated managers (`PromotionManager`, `RecruitmentManager`, etc.).
- Most behavior is event-driven + vanilla models (very compatible).
- Harmony is initialized for future patches but currently lightly used.
- See `CHANGELOG.md` and `FOLLOWUPS.md` (if present) for history and ideas.

## Credits

- TaleWorlds — amazing modding support.
- BUTR team (Harmony, ButterLib, UIExtenderEx, MCM) — the essential backbone for modern Bannerlord mods.
- The Bannerlord modding community and docs at https://docs.bannerlordmodding.com/

## License

Free to use, modify, and redistribute.

Happy campaigning!