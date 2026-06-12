using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Main entry point for the TroopManagerEnhanced mod.
    /// Follows standard Bannerlord MBSubModuleBase lifecycle.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        // Use a unique Harmony ID. Reverse domain or mod id is conventional.
        private const string HarmonyId = "Bannerlord.TroopManagerEnhanced";

        private Harmony? _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            try
            {
                // Initialize Harmony and apply all patches in this assembly (if any exist).
                // Even if we do not heavily patch, this is the recommended place and pattern.
                //
                // This will pick up:
                //   - Any [HarmonyPatch] classes (PromotionPatches.cs is a placeholder for advanced users only).
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                // Optional UIExtenderEx registration is available if deeper UI integration (e.g. buttons in the party screen) is desired later.
                // See Bannerlord UIExtenderEx documentation for PartyVM / PrisonerListVM mixins.

                // Optional: Log that we loaded (visible in launcher logs / debug).
                Debug.Print($"[Bannerlord.TroopManagerEnhanced] SubModule loaded. Harmony patches applied. v{typeof(SubModule).Assembly.GetName().Version}");
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] ERROR in OnSubModuleLoad: {ex}");
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=TME_INIT_FAIL}TroopManagerEnhanced failed to initialize Harmony. Check logs.").ToString(),
                    Colors.Red));
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();

            try
            {
                _harmony?.UnpatchAll(HarmonyId);
                _harmony = null;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] ERROR in OnSubModuleUnloaded: {ex}");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            // Good place for one-time messages or MCM-related setup if needed.
            // Settings are usually accessed safely after this point.
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);

            // Only add campaign behaviors when we are actually in a campaign (not custom battle, etc.).
            if (game.GameType is Campaign)
            {
                var campaignStarter = (CampaignGameStarter)gameStarter;

                // Add our main behavior that drives the troop management logic on ticks.
                campaignStarter.AddBehavior(new TroopManagementBehavior());

                Debug.Print("[Bannerlord.TroopManagerEnhanced] TroopManagementBehavior registered for campaign.");
            }
        }

        // Additional lifecycle overrides (OnNewGameCreated, OnGameLoaded, etc.) can be added here if needed in the future.
        // (Accelerated recruitment hotkey (Ctrl+R) removed along with the feature.)
    }
}
