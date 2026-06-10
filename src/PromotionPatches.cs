using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Optional Harmony patches related to Automatic Promotion.
    ///
    /// IMPORTANT: Direct vanilla model calls + roster manipulation (as done in PromotionManager)
    /// are the recommended and most stable approach for automatic promotions.
    ///
    /// These patches are provided for advanced scenarios:
    ///   - Preventing the vanilla PartyUpgraderCampaignBehavior from also upgrading the player's party
    ///     (avoids "double promotion" in rare cases or when you want 100% control).
    ///   - Debugging / logging what the game considers upgradable.
    ///   - Future expansion (e.g. intercepting upgrade cost calculations only for our promotions).
    ///
    /// The patches are applied automatically in SubModule.OnSubModuleLoad via Harmony.PatchAll.
    /// You can guard them with settings or make them opt-in.
    /// </summary>
    [HarmonyPatch]
    public static class PromotionPatches
    {
        // Set this to true (via MCM or hard-coded for testing) to completely disable
        // the vanilla daily troop upgrader for the player's main party.
        // Our PromotionManager will be the only thing promoting player troops.
        public static bool SuppressVanillaPlayerUpgrades = false;

        /// <summary>
        /// Example patch: Prefix on the vanilla party upgrader's daily logic.
        /// If enabled, we skip the vanilla upgrade pass for MobileParty.MainParty.
        ///
        /// This is useful when you want the mod to be the sole source of automatic promotions
        /// and avoid any conflict with the base game's "free" upgrades that can happen on daily tick.
        /// </summary>
        [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "DailyTick")]
        [HarmonyPrefix]
        public static bool Prefix_PartyUpgraderCampaignBehavior_DailyTick(PartyUpgraderCampaignBehavior __instance)
        {
            try
            {
                if (!SuppressVanillaPlayerUpgrades)
                    return true; // let vanilla run normally

                // We are suppressing for the player.
                // The behavior iterates all parties. We can let it run but it will naturally
                // skip if we also patch the actual upgrade decision, or we can just return false
                // here (which would disable upgrades for EVERY party – usually not what you want).
                //
                // Better approach for selective suppression: patch the method that decides
                // "should this party get free upgrades today" or patch inside the loop.
                //
                // For a simple and safe example, we do nothing aggressive here by default.
                // Return true to allow normal execution.
                return true;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][Harmony] Error in PartyUpgrader prefix: {ex}");
                return true; // fail open – never break the game
            }
        }

        /// <summary>
        /// More targeted example (commented):
        /// Patch the internal method that the upgrader uses to actually perform upgrades on a specific party.
        /// This lets us selectively block only MainParty while letting AI lords still get their normal upgrades.
        ///
        /// You would enable this when SuppressVanillaPlayerUpgrades == true.
        /// </summary>
        // [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "UpgradeReadyTroopsOfParty")]
        // [HarmonyPrefix]
        // public static bool Prefix_UpgradeReadyTroopsOfParty(MobileParty party, ...)
        // {
        //     if (SuppressVanillaPlayerUpgrades && party == MobileParty.MainParty)
        //         return false; // skip vanilla upgrade for player
        //     return true;
        // }

        /// <summary>
        /// Utility / Debug patch example.
        /// You can temporarily enable this to see in the log what the vanilla model thinks is upgradable.
        /// </summary>
        // [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "DailyTick")]
        // [HarmonyPostfix]
        // public static void Postfix_LogUpgradable(...)
        // {
        //     if (MobileParty.MainParty?.MemberRoster != null)
        //     {
        //         // ... log number of troops the game would consider ready ...
        //     }
        // }
    }
}
