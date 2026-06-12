using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Campaign behavior that performs automated troop management on daily (and optionally hourly / party-tick) events.
    ///
    /// This is now a thin orchestration layer. All heavy feature logic lives in dedicated managers:
    ///   - PromotionManager
    ///   - RecruitmentManager (prisoners + accelerated)
    ///   - AutoRecruitManager (settlement recruits)
    ///   - AutoDismissManager (low-tier / wounded cleanup)
    ///
    /// Best practice: We register common tick events and let the individual managers decide whether to act.
    /// </summary>
    public class TroopManagementBehavior : CampaignBehaviorBase
    {
        // Dedicated manager for Feature 1: Automatic Promotion.
        private readonly PromotionManager _promotionManager = new PromotionManager();

        // Dedicated manager for Feature 2: Automatic Prisoner Recruitment.
        private readonly RecruitmentManager _recruitmentManager = new RecruitmentManager();

        // Dedicated managers for the remaining settlement auto-recruit and dismiss features.
        // Extracted for consistency with the other managers (better separation and testability).
        private readonly AutoRecruitManager _autoRecruitManager = new AutoRecruitManager();
        private readonly AutoDismissManager _autoDismissManager = new AutoDismissManager();

        public override void RegisterEvents()
        {
            // === Promotion (and other features) can run on these events ===
            // Daily is the safest default (good performance + feels natural).
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

            // Hourly gives more responsive promotions when player wants it (via MCM).
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);

            // Very frequent game tick. Only the managers that have "OnPartyTick" frequency selected will actually run.
            // We still throttle inside PromotionManager to avoid doing heavy work every single frame.
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnGameTick);

            // After battles: new prisoners are often added. Good time to attempt recruitment of any that are immediately ready.
            CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No serialized data needed (all configuration is global-only; per-campaign/per-save interface removed).
        }

        /// <summary>
        /// Returns true only for the player's main land party.
        /// Ignores naval/ship parties (e.g. from War Sails or other naval mods).
        /// </summary>
        private static bool IsPlayerLandParty(MobileParty party)
        {
            if (party == null) return false;
            if (party != MobileParty.MainParty) return false;

            // Future-proofing: skip obvious ship parties if the API exposes it
            // (property names may vary; this is defensive and non-breaking if the property doesn't exist).
            // Example (commented because it may not exist in all builds):
            // if (party.HasProperty("IsShip") && (bool)party.GetType().GetProperty("IsShip")?.GetValue(party) == true) return false;

            return true;
        }

        private void OnDailyTick()
        {
            var settings = TroopManagerSettings.Instance;
            if (settings == null || !settings.ModEnabled)
                return;

            // Early exit if none of the daily-managed features are active.
            // Cleaner than the previous long chain of negated IsFeatureEnabled calls.
            bool anyDailyFeature = TroopManagerSettings.IsFeatureEnabled("promotion") ||
                                   TroopManagerSettings.IsFeatureEnabled("prisoner_recruit") ||
                                   TroopManagerSettings.IsFeatureEnabled("settlement_recruit") ||
                                   TroopManagerSettings.IsFeatureEnabled("dismiss");

            if (!anyDailyFeature)
                return;

            var mainParty = MobileParty.MainParty;
            if (mainParty == null || !mainParty.IsActive)
                return;

            if (!IsPlayerLandParty(mainParty))
                return;

            try
            {
                // === FEATURE 1: Automatic Promotion ===
                if (TroopManagerSettings.IsFeatureEnabled("promotion"))
                    _promotionManager.TryPerformPromotions(mainParty, settings);

                // === FEATURE 2: Automatic Prisoner Recruitment ===
                if (TroopManagerSettings.IsFeatureEnabled("prisoner_recruit"))
                    _recruitmentManager.TryRecruitPrisoners(mainParty, settings);

                // Settlement auto-recruit and dismiss are primarily daily.
                if (TroopManagerSettings.IsFeatureEnabled("settlement_recruit"))
                    _autoRecruitManager.TryPerformAutoRecruit(mainParty, settings);

                if (TroopManagerSettings.IsFeatureEnabled("dismiss"))
                    _autoDismissManager.TryPerformAutoDismiss(mainParty, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during daily tick: {ex}");
            }
        }

        private void OnHourlyTick()
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
                _promotionManager.TryPerformPromotions(mainParty, settings);
                _recruitmentManager.TryRecruitPrisoners(mainParty, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during hourly tick: {ex}");
            }
        }

        /// <summary>
        /// High frequency game tick (called very often).
        /// Only managers configured for "OnPartyTick" frequency will actually execute logic here.
        /// </summary>
        private void OnGameTick(float dt)
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
                _promotionManager.TryPerformPromotions(mainParty, settings);
                _recruitmentManager.TryRecruitPrisoners(mainParty, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during game tick: {ex}");
            }
        }

        /// <summary>
        /// Called when the player finishes a battle (victory or retreat).
        /// New prisoners are frequently added here. We attempt recruitment immediately
        /// so the player doesn't have to wait for the next daily/hourly tick.
        /// </summary>
        private void OnPlayerBattleEnd(MapEvent mapEvent)
        {
            var settings = TroopManagerSettings.Instance;
            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitPrisonersEnabled)
                return;

            var mainParty = MobileParty.MainParty;
            if (mainParty == null || !mainParty.IsActive)
                return;

            if (!IsPlayerLandParty(mainParty))
                return;

            try
            {
                _recruitmentManager.TryRecruitPrisoners(mainParty, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced] Exception during OnPlayerBattleEnd recruitment: {ex}");
            }
        }

        // NOTE: The old PerformAutoUpgrades heuristic was replaced by PromotionManager.
        // Auto-recruit and auto-dismiss were also extracted into their own managers
        // (see AutoRecruitManager.cs and AutoDismissManager.cs) for consistency.
    }
}
