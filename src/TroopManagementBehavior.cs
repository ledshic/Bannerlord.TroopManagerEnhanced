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
    /// This is the orchestration layer. Heavy logic for each feature lives in dedicated managers (PromotionManager, etc.)
    /// so the behavior stays clean and easy to extend with more features.
    ///
    /// Best practice: We register the most common tick events (Daily + Hourly) and also a high-frequency TickEvent.
    /// Inside each handler we let the individual managers decide (based on MCM frequency settings) whether they
    /// should actually do work this tick. This gives players fine control without us having to constantly
    /// add/remove listeners when settings change.
    /// </summary>
    public class TroopManagementBehavior : CampaignBehaviorBase
    {
        // Dedicated manager for Feature 1: Automatic Promotion.
        private readonly PromotionManager _promotionManager = new PromotionManager();

        // Dedicated manager for Feature 2: Automatic Prisoner Recruitment.
        // Handles checking PrisonRoster, conformity-available prisoners, free party slots, and vanilla-style recruitment.
        private readonly RecruitmentManager _recruitmentManager = new RecruitmentManager();

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

            // Future expansion ideas:
            // CampaignEvents.OnSettlementEnteredEvent.AddNonSerializedListener(...)
            // CampaignEvents.PartyAttachedToSettlementEvent etc.
        }

        public override void SyncData(IDataStore dataStore)
        {
            // No serialized data needed for Global settings based features.
            // If you later move some promotion state (cooldowns, stats) to PerSave, store it here.
        }

        /// <summary>
        /// 1.4.5 + War Sails compatibility helper.
        /// Returns true only for the player's main land party.
        /// Ignores naval/ship parties that may exist in War Sails content.
        /// </summary>
        private static bool IsPlayerLandParty(MobileParty party)
        {
            if (party == null) return false;
            if (party != MobileParty.MainParty) return false;

            // Additional future-proofing: skip obvious ship parties if the API exposes it in 1.4.5+
            // (property names may vary; this is defensive and non-breaking if the property doesn't exist).
            // Example (commented because it may not exist in all 1.4.5 builds):
            // if (party.HasProperty("IsShip") && (bool)party.GetType().GetProperty("IsShip")?.GetValue(party) == true) return false;

            return true;
        }

        private void OnDailyTick()
        {
            var settings = TroopManagerSettings.Instance;
            if (settings == null || !TroopManagerSettings.IsFeatureEnabled("promotion") &&
                !TroopManagerSettings.IsFeatureEnabled("prisoner_recruit") &&
                !TroopManagerSettings.IsFeatureEnabled("settlement_recruit") &&
                !TroopManagerSettings.IsFeatureEnabled("dismiss"))
                return;

            var mainParty = MobileParty.MainParty;
            if (mainParty == null || !mainParty.IsActive)
                return;

            // 1.4.5 + War Sails compatibility: Only manage the player's primary land party.
            // Naval/ship parties (introduced or expanded in War Sails) should be ignored.
            if (!IsPlayerLandParty(mainParty))
                return;

            try
            {
                // === FEATURE 1: Automatic Promotion ===
                if (TroopManagerSettings.IsFeatureEnabled("promotion"))
                    _promotionManager.TryPerformPromotions(mainParty, settings);

                // === FEATURE 2: Automatic Prisoner Recruitment ===
                // Runs on the same ticks so it respects the "party tick or after battles" requirement.
                // The RecruitmentManager has its own light throttling.
                if (TroopManagerSettings.IsFeatureEnabled("prisoner_recruit"))
                    _recruitmentManager.TryRecruitPrisoners(mainParty, settings);

                // Other features (settlement recruit / dismiss) still use daily as primary.
                if (TroopManagerSettings.IsFeatureEnabled("settlement_recruit") && settings.AutoRecruitEnabled)
                {
                    PerformAutoRecruit(mainParty, settings);
                }

                if (TroopManagerSettings.IsFeatureEnabled("dismiss") && (settings.AutoDismissLowTierEnabled || settings.DismissHeavilyWounded))
                {
                    PerformAutoDismiss(mainParty, settings);
                }
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

        // NOTE: The old PerformAutoUpgrades heuristic has been replaced by the dedicated PromotionManager
        // (see PromotionManager.cs). The behavior now calls _promotionManager.TryPerformPromotions(...)
        // from multiple tick sources. This gives proper vanilla XP/gold checks + rich MCM configuration.

        #region Auto Recruit

        private void PerformAutoRecruit(MobileParty party, TroopManagerSettings settings)
        {
            if (settings.RecruitOnlyInSettlement && party.CurrentSettlement == null)
                return;

            int partyLimit = party.Party.PartySizeLimit;
            if (partyLimit <= 0)
                return;

            int currentSize = party.MemberRoster.TotalManCount;
            int targetSize = (int)(partyLimit * (settings.RecruitTargetPercentage / 100f));

            int needed = Math.Max(0, targetSize - currentSize);
            if (needed <= 0)
                return;

            int toRecruit = Math.Min(needed, settings.MaxRecruitsPerDay);

            // Determine what basic recruit to use.
            // Best effort: use the culture of the current settlement, falling back to player's culture.
            CultureObject? culture = party.CurrentSettlement?.Culture ?? Hero.MainHero?.Culture ?? party.LeaderHero?.Culture;
            if (culture == null)
                return;

            // Find the most basic "recruit" troop for this culture.
            // Vanilla cultures define a basic troop (e.g. "Imperial Recruit", "Vlandian Recruit", etc.).
            // We look for a low-tier troop with no upgrade-from requirement or the first in the tree.
            CharacterObject? recruit = FindBasicRecruitForCulture(culture);
            if (recruit == null)
                return;

            // Add the recruits.
            party.MemberRoster.AddToCounts(recruit, toRecruit);

            if (settings.ShowNotifications)
            {
                var text = new TextObject("{=TME_SETTLE_RECRUIT_001}Recruited {COUNT} {TROOP}.");
                text.SetTextVariable("COUNT", toRecruit);
                text.SetTextVariable("TROOP", recruit.Name);
                InformationManager.DisplayMessage(new InformationMessage(
                    text.ToString(),
                    Colors.Cyan));
            }
        }

        /// <summary>
        /// Tries to locate a suitable basic recruit troop for the given culture.
        /// This is intentionally simple and does not touch the settlement's actual recruit pool.
        /// </summary>
        private static CharacterObject? FindBasicRecruitForCulture(CultureObject culture)
        {
            if (culture?.BasicTroop != null)
                return culture.BasicTroop;

            // Fallback: search all character objects for a low-tier troop belonging to this culture
            // that has upgrade targets (i.e. is recruitable).
            return CharacterObject.All
                .FirstOrDefault(c =>
                    c != null &&
                    c.Culture == culture &&
                    c.Tier <= 2 &&
                    c.UpgradeTargets != null &&
                    c.UpgradeTargets.Length > 0 &&
                    !c.IsHero &&
                    c.Occupation == Occupation.Soldier);
        }

        #endregion

        #region Auto Dismiss

        private void PerformAutoDismiss(MobileParty party, TroopManagerSettings settings)
        {
            var roster = party.MemberRoster;
            if (roster == null)
                return;

            int dismissed = 0;
            bool nearCapacity = party.MemberRoster.TotalManCount >= (party.Party.PartySizeLimit * 0.95f);

            for (int i = roster.Count - 1; i >= 0; i--)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var character = element.Character as CharacterObject;
                if (character == null || element.Number <= 0)
                    continue;

                bool shouldDismiss = false;

                // Rule 1: Low tier excess
                if (settings.AutoDismissLowTierEnabled &&
                    character.Tier <= settings.DismissBelowTier &&
                    nearCapacity)
                {
                    shouldDismiss = true;
                }

                // Rule 2: Heavily wounded stacks
                if (!shouldDismiss &&
                    settings.DismissHeavilyWounded &&
                    element.WoundedNumber > 0)
                {
                    float woundedPercent = (float)element.WoundedNumber / element.Number * 100f;
                    if (woundedPercent >= settings.WoundedDismissThresholdPercent)
                    {
                        shouldDismiss = true;
                    }
                }

                if (!shouldDismiss)
                    continue;

                // Dismiss the whole stack (or you could dismiss only part of it).
                int count = element.Number;
                roster.AddToCounts(character, -count);
                dismissed += count;
            }

            if (dismissed > 0 && settings.ShowNotifications)
            {
                var text = new TextObject("{=TME_DISMISS_001}Dismissed {COUNT} troops.");
                text.SetTextVariable("COUNT", dismissed);
                InformationManager.DisplayMessage(new InformationMessage(
                    text.ToString(),
                    Colors.Red));
            }
        }

        #endregion
    }
}
