using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Main entry point for the TroopManagerEnhanced mod.
    /// Follows standard Bannerlord MBSubModuleBase lifecycle.
    /// </summary>
    public class SubModule : MBSubModuleBase
    {
        // Use a unique Harmony ID. Reverse domain or mod id is conventional.
        private const string HarmonyId = "com.yourname.troopmanagerenhanced";

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
                //   - Any [HarmonyPatch] classes (see PromotionPatches.cs for optional
                //     vanilla upgrader suppression and debug patches related to Automatic Promotion).
                _harmony = new Harmony(HarmonyId);
                _harmony.PatchAll(Assembly.GetExecutingAssembly());

                // Optional UIExtenderEx registration (already a dependency of the mod).
                // For a richer experience with Feature 3, you can extend the Party/Prisoner Gauntlet VM here
                // to add an "Accelerate Recruitment" button directly in the prisoner list.
                // Example (requires additional mixin/view model code):
                // var uiExtender = new UIExtender("TroopManagerEnhanced");
                // uiExtender.Register(new PartyScreenAccelerateMixin()); // your custom mixin
                // uiExtender.Enable();
                //
                // See Bannerlord UIExtenderEx documentation for PartyVM / PrisonerListVM mixins.

                // Optional: Log that we loaded (visible in launcher logs / debug).
                Debug.Print($"[TroopManagerEnhanced] SubModule loaded. Harmony patches applied. v{typeof(SubModule).Assembly.GetName().Version}");
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

                Debug.Print("[TroopManagerEnhanced] TroopManagementBehavior registered for campaign.");
            }
        }

        // You can also override:
        // - OnNewGameCreated (for first-time setup)
        // - OnGameLoaded (to handle migration or re-initialization from saves)
        // - BeginGameStart / OnCampaignStart etc. as needed.

        /// <summary>
        /// Simple hotkey support for Feature 3 (Accelerated Recruitment).
        /// Default: Left Ctrl + R (while in a campaign with a main party).
        /// Feel free to change the key combination.
        /// For a more polished experience, consider adding a proper button via UIExtenderEx on the Gauntlet Party/Prisoner screen.
        /// </summary>
        protected override void OnApplicationTick(float dt)
        {
            base.OnApplicationTick(dt);

            try
            {
                // Only allow in campaign mode
                if (Campaign.Current == null || Hero.MainHero == null)
                    return;

                var settings = TroopManagerSettings.Instance;
                if (settings == null || !settings.AcceleratedRecruitmentEnabled)
                    return;

                // Hotkey: Ctrl + R
                bool ctrl = Input.IsKeyDown(InputKey.LeftControl) || Input.IsKeyDown(InputKey.RightControl);
                bool keyPressed = Input.IsKeyPressed(InputKey.R);

                if (ctrl && keyPressed)
                {
                    var party = MobileParty.MainParty;
                    if (party != null && party.IsActive)
                    {
                        RecruitmentManager.AccelerateRecruitment(party, settings);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Hotkey error in OnApplicationTick: {ex}");
            }
        }
    }
}
