using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Placeholder for optional advanced Harmony patches.
    ///
    /// IMPORTANT: The core promotion logic (PromotionManager) already uses vanilla models
    /// and roster operations directly. Adding heavy patches is usually unnecessary and risky.
    ///
    /// Use this file only if you have a very specific need, such as:
    /// - Completely suppressing the vanilla PartyUpgraderCampaignBehavior for the player
    /// - Intercepting upgrade cost calculations
    /// - Advanced debugging
    ///
    /// Patches in this class are automatically applied by Harmony.PatchAll in SubModule.
    /// </summary>
    [HarmonyPatch]
    public static class PromotionPatches
    {
        // Add your [HarmonyPatch] methods here only when truly needed.
        // Example (commented out because the recommended approach is the direct model usage in PromotionManager):

        /*
        [HarmonyPatch(typeof(PartyUpgraderCampaignBehavior), "DailyTick")]
        [HarmonyPrefix]
        public static bool Prefix_SuppressVanillaForPlayer(PartyUpgraderCampaignBehavior __instance)
        {
            // Careful: suppressing here affects ALL parties unless you filter inside.
            // Usually better to patch a more specific method.
            return true; // let vanilla run
        }
        */
    }
}
