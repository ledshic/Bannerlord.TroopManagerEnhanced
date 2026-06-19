using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
    /// - Settlement entered trigger for volunteer auto-recruit when below configured threshold.
    ///
    /// Removed: auto dismiss and accelerated recruitment (and related triggers/hotkeys).
    /// </summary>
    public class TroopManagementBehavior : CampaignBehaviorBase
    {
        private readonly PromotionManager _promotionManager = new PromotionManager();
        private readonly RecruitmentManager _recruitmentManager = new RecruitmentManager();
        private string? _lastSettlementRecruitSettlementId;
        private double _lastSettlementRecruitTimeHours = -9999d;

        public override void RegisterEvents()
        {
            // Daily only, as specified. Natural game rhythm, good performance.
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
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

        private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
        {
            var settings = TroopManagerSettings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitFromSettlementEnabled)
                return;

            if (!IsPlayerLandParty(party))
                return;

            if (settlement == null)
                return;

            try
            {
                if (IsSettlementRecruitOnCooldown(settlement, settings))
                    return;

                _recruitmentManager.TryRecruitVolunteersFromSettlement(party, settlement, settings);

                _lastSettlementRecruitSettlementId = settlement.StringId;
                _lastSettlementRecruitTimeHours = CampaignTime.Now.ToHours;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during settlement auto-recruit: {ex}");
            }
        }

        private bool IsSettlementRecruitOnCooldown(Settlement settlement, TroopManagerSettings settings)
        {
            double cooldownHours = Math.Max(0, settings.SettlementRecruitCooldownHours);
            if (cooldownHours <= 0)
                return false;

            if (!string.Equals(_lastSettlementRecruitSettlementId, settlement.StringId, StringComparison.Ordinal))
                return false;

            double elapsed = CampaignTime.Now.ToHours - _lastSettlementRecruitTimeHours;
            return elapsed < cooldownHours;
        }
    }
}
