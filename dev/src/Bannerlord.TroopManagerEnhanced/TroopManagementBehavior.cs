using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Campaign behavior for automated troop management.
    ///
    /// Simplified per requirements:
    /// - Daily tick only for:
    ///   1. Promotion: only soldiers with full required EXP (xp / xpCost) get promoted. Random branch if choices.
    ///   2. Prisoner recruit: only prisoners with sufficient conformity (using vanilla PrisonerRecruitmentModel + prison XP-as-conformity) + free slots.
    ///
    /// Removed: auto dismiss, settlement auto-recruit, accelerated recruitment (and all related triggers/hotkeys).
    /// </summary>
    public class TroopManagementBehavior : CampaignBehaviorBase
    {
        private readonly PromotionManager _promotionManager = new PromotionManager();
        private readonly RecruitmentManager _recruitmentManager = new RecruitmentManager();

        public override void RegisterEvents()
        {
            // Daily only, as specified. Natural game rhythm, good performance.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Global-only settings; no per-save data.
        }

        /// <summary>
        /// Returns true only for the player's main land party.
        /// </summary>
        private static bool IsPlayerLandParty(MobileParty party)
        {
            if (party == null) return false;
            if (party != MobileParty.MainParty) return false;
            return true;
        }

        private void OnDailyTick()
        {
            var settings = TroopManagerSettings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            var mainParty = MobileParty.MainParty;
            if (mainParty == null || !mainParty.IsActive)
                return;

            if (!IsPlayerLandParty(mainParty))
                return;

            try
            {
                if (settings.AutoPromotionEnabled)
                    _promotionManager.TryPerformDailyPromotions(mainParty, settings);

                if (settings.AutoRecruitPrisonersEnabled)
                    _recruitmentManager.TryRecruitPrisoners(mainParty, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during daily tick: {ex}");
            }
        }
    }
}
