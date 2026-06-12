# TroopManagerEnhanced - Follow-ups & Roadmap (Historical / Slimmed)

**Note (2026-06)**: The mod underwent a major scope reduction. The following features and their code/config were **completely removed**:
- Auto Dismiss
- Settlement Auto Recruit  
- Accelerated Recruitment (incl. hotkey, pay-to-recruit, conformity bypass)

As a result, many older follow-up ideas (especially anything referencing "Accelerated", the deleted `Auto*Manager.cs` files, frequent ticks, etc.) are now **obsolete**.

The current focused feature set is:
- Daily full-EXP promotion (vanilla costs, random branch on choice)
- Daily conformity-checked prisoner recruitment (vanilla model + slots)

This document has been pruned for the post-reduction state. Only still-relevant or general items remain. Historical details are in CHANGELOG.md.

---

## Still Relevant / Low-Hanging Polish

- Review `PromotionPatches.cs` placeholder — decide whether to remove entirely or keep as a clearly documented "for advanced users only" skeleton.
- Improve `GameFolder` handling / docs further if cross-platform builds become a pain point.
- Keep the 4 language files in sync on any future string changes.
- Consider a lightweight `build.sh` or pure `dotnet` packaging helper for users who don't have pwsh.
- General: notification polish, better error handling, or expanding `HasRoomForRecruits` / other helpers if useful.

## Out of Scope / Obsolete (Due to Scope Reduction)

- Anything related to the three removed features (hotkeys, accelerated schedules, settlement recruits, dismiss rules, etc.).
- Re-introducing per-save/per-campaign settings.
- Large new gameplay features without clear direction.

## General Notes

- The project source is now very lean (only 7 .cs files in src/, no dead managers, clean build artifacts can be ignored via .gitignore).
- When editing strings, update **all four** languages (`EN`, `CN`, `CNs`, `SC`).
- For builds: use `dev/build.ps1 -Version x.y.z` (pwsh on macOS/Linux) after setting a real `GameFolder`.

Run `dotnet build` (with proper GameFolder) or the build script to verify after changes.

(Older detailed roadmap items from before the 2026 scope reduction have been archived/removed from this file for cleanliness.)