# TroopManagerEnhanced

A quality-of-life mod for Mount & Blade II: Bannerlord that automates common troop management tasks for the player's party.

**Target**: Bannerlord 1.4.5 + War Sails 1.2.5 (and compatible later patches)

**Tested / Designed For**:
- Mount & Blade II: Bannerlord v1.4.5
- War Sails DLC v1.2.5

The mod primarily manipulates `MobileParty.MainParty` using high-level vanilla models (`IPartyTroopUpgradeModel`, `PartyWageModel`, roster operations). It has low risk of conflicting with naval content.

**Dependencies** (must be loaded **before** this mod):
- Bannerlord.Harmony (top of load order)
- Bannerlord.ButterLib
- Bannerlord.UIExtenderEx
- Bannerlord.MCM (Mod Configuration Menu v5+)

## Features (all configurable via MCM)

### Feature 1: Automatic Promotion (Fully Implemented)
- Periodically scans the player's main party (Daily / Hourly / OnPartyTick – configurable).
- Uses the **vanilla `IPartyTroopUpgradeModel`** (`GetXpCostForUpgrade`, `GetGoldCostForUpgrade`) to determine exactly how many troops in a stack are eligible.
- Correctly consumes XP from the `TroopRosterElement` and gold via `Hero.ChangeHeroGold`.
- Applies upgrades using the same `TroopRoster.AddToCounts` pattern the base game uses internally (`PartyUpgraderCampaignBehavior`).
- **Smart branch selection** when a troop has multiple upgrade paths:
  - Vanilla First (game-defined order)
  - Random
  - Prefer Player Culture / Kingdom
  - Highest Tier
  - Lowest Cost
- **Cost Multiplier** (0.5x – 2.0x vanilla gold cost) – fully configurable.
- Max promotions per check, gold reserve protection, option to skip wounded stacks, limited multi-tier chaining support.
- Fully respects party size (promotions don't change headcount).

### Other Features
- **Auto Recruit**: Fills your party with basic recruits from the current settlement's culture when below a threshold.
- **Auto Dismiss**: Removes excess low-tier troops or heavily wounded soldiers based on configurable rules.
- **Notifications**: Optional messages when actions are taken.

All logic prefers vanilla models and methods (`PartyTroopUpgradeModel`, `CharacterObject.UpgradeTargets`, roster manipulation following upgrade trees, etc.) to stay compatible with game rules, perks, and other mods.

Optional Harmony patches are included (PromotionPatches.cs) for advanced users who want to suppress the vanilla upgrader for the player party.

## Folder Structure (for development)

```
TroopManagerEnhanced/
├── TroopManagerEnhanced.csproj
├── SubModule.cs
├── TroopManagerSettings.cs
├── TroopManagementBehavior.cs
├── TroopManagerHelper.cs (optional helpers)
├── TroopManagerEnhanced.sln
├── README.md
├── _Module/
│   └── SubModule.xml
└── ModuleData/ (optional custom XMLs / localization)
```

After build, the DLL is copied to `_Module/bin/Win64_Shipping_Client/TroopManagerEnhanced.dll`.

To install for testing:
1. Build in Release (or Debug).
2. Copy the entire `_Module` folder contents (or symlink) into your game's `Modules/TroopManagerEnhanced/`.
3. Or use a post-build script / symbolic link for rapid iteration:
   - `mklink /J "C:\...\Modules\TroopManagerEnhanced" "path\to\this\_Module"`

## Building

### Requirements
- Visual Studio 2022+ (or VS Code + .NET SDK)
- A copy of Bannerlord (for assembly references during dev)
- NuGet packages will pull MCM etc.

### Recommended .csproj setup
Edit the `<GameFolder>` property in the .csproj to point at your Bannerlord installation (the folder containing `bin\Win64_Shipping_Client`).

Example:
```xml
<GameFolder>C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord</GameFolder>
```

Build → the DLL + symbols will be placed correctly for the `_Module`.

You can also add a post-build event that copies the whole module folder to your game's Modules directory.

## Load Order (Launcher / BLSE / Vortex)

1. Native
2. SandBoxCore
3. Sandbox
4. StoryMode (if using)
5. **Bannerlord.Harmony**
6. **Bannerlord.ButterLib**
7. **Bannerlord.UIExtenderEx**
8. **Bannerlord.MCM**
9. **TroopManagerEnhanced**
10. Everything else

## MCM Settings

After loading a campaign, open **Mod Options** (ESC → Mod Options or the MCM button). All settings are under **Troop Manager Enhanced**.

Settings are saved as JSON (Global by default).

## Best Practices & Notes

- This mod manipulates your **MainParty** only by default (easy to extend to clan parties).
- Upgrades respect the troop trees defined in `NPCCharacters.xml` (via `CharacterObject.UpgradeTargets`).
- Gold is deducted for upgrades using the vanilla `PartyTroopUpgradeModel.GetGoldCostForUpgrade`.
- Recruitment is intentionally conservative: it adds low-tier " Recruit" level troops from the settlement's culture. It does **not** drain the settlement's actual recruit pool (to avoid desyncs with AI/villages). For a more authentic version, a patch to `RecruitmentCampaignBehavior` would be needed.
- Harmony is initialized but core features run via `CampaignBehavior` + tick events (no heavy patching required).

## Extending

- Add more events in `TroopManagementBehavior.RegisterEvents()`.
- Add PerSave or PerCampaign settings if you want options that reset per save.
- Use ButterLib's `SubModule.Current.GetServiceContainer()` for advanced DI if desired.

## Versioning & Compatibility

- Mod version tracked in SubModule.xml and Assembly.
- Compatible with 1.2.12+ and 1.3+ branches (test on your patch level).

## Credits

- TaleWorlds for Bannerlord and its excellent moddability.
- BUTR team (Harmony, ButterLib, UIExtenderEx, MCMv5) for the essential framework.
- Community modders for countless examples and the docs at https://docs.bannerlordmodding.com/

Happy campaigning!
