using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Promotion manager.
    ///
    /// Fixed logic (per request):
    /// - Runs on daily tick (called from behavior).
    /// - Only promotes soldiers/stacks that have "full EXP standing by":
    ///     ready = (xpCost > 0) ? (availableXp / xpCost) : count
    ///   i.e. we only upgrade whole soldiers who have accumulated the full required XP for their next tier.
    /// - If a troop has multiple upgrade paths, pick one at random.
    /// - Still respects gold reserve + configurable cost multiplier + max per day cap.
    /// - No more frequency modes, no selection mode dropdown, no multi-tier chaining in one pass, no wounded skip (kept simple).
    /// - Uses vanilla PartyTroopUpgradeModel for costs/eligibility.
    /// </summary>
    public class PromotionManager
    {
        /// <summary>
        /// Daily entry point (the only one used now).
        /// </summary>
        public void TryPerformDailyPromotions(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            if (settings == null || !settings.ModEnabled || !settings.AutoPromotionEnabled)
                return;

            try
            {
                int promotedCount = PerformDailyPromotionsInternal(party, settings);

                if (promotedCount > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=TME_PROMO_001}Promoted {PROMOTED} troops.", null);
                    text.SetTextVariable("PROMOTED", promotedCount);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));
                }
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][Promotion] Exception: {ex}");
            }
        }

        /// <summary>
        /// Core daily promotion pass.
        /// Only stacks with full-EXP soldiers (ready count > 0) are considered.
        /// Random branch selection when >1 upgrade target.
        /// </summary>
        private int PerformDailyPromotionsInternal(MobileParty party, TroopManagerSettings settings)
        {
            var roster = party.MemberRoster;
            if (roster == null || roster.TotalManCount <= 0)
                return 0;

            var upgradeModel = Campaign.Current?.Models?.PartyTroopUpgradeModel;
            if (upgradeModel == null)
                return 0;

            int goldReserve = Math.Max(0, settings.MinimumGoldReserve);
            int playerGold = Hero.MainHero?.Gold ?? 0;
            if (playerGold <= goldReserve)
                return 0;

            float costMultiplier = Math.Max(0.1f, Math.Min(5f, settings.PromotionCostMultiplier));
            int maxThisPass = Math.Max(1, settings.MaxPromotionsPerCheck);

            int totalPromoted = 0;
            int goldSpent = 0;

            // Backwards iteration for safe roster mutation via AddToCounts.
            for (int i = roster.Count - 1; i >= 0 && totalPromoted < maxThisPass; i--)
            {
                TroopRosterElement element = roster.GetElementCopyAtIndex(i);
                var fromTroop = element.Character as CharacterObject;

                if (fromTroop == null || element.Number <= 0)
                    continue;

                if (fromTroop.UpgradeTargets == null || fromTroop.UpgradeTargets.Length == 0)
                    continue;

                // Skip troops with multiple upgrade paths if the setting is enabled,
                // leaving the choice to the player.
                if (settings.SkipBranchedPromotions && fromTroop.UpgradeTargets.Length > 1)
                    continue;

                int availableXp = roster.GetElementXp(i);
                if (availableXp < 0) availableXp = 0;

                // Pick target: random if multiple choices (as requested).
                CharacterObject? target = ChooseUpgradeTargetRandom(fromTroop);
                if (target == null)
                    continue;

                // Vanilla costs
                int xpCost = upgradeModel.GetXpCostForUpgrade(party.Party, fromTroop, target);
                int baseGoldCost = upgradeModel.GetGoldCostForUpgrade(party.Party, fromTroop, target).RoundedResultNumber;
                int effectiveGoldCost = (int)(baseGoldCost * costMultiplier);
                if (effectiveGoldCost < 0) effectiveGoldCost = 0;

                // FULL EXP check: how many soldiers in this stack have accumulated the full required XP?
                int readyCount = (xpCost <= 0) ? element.Number : (availableXp / xpCost);
                if (readyCount <= 0)
                    continue;   // Not "full exp stand by" -- skip this stack for today.

                // Gold affordability
                int affordable = (effectiveGoldCost <= 0)
                    ? element.Number
                    : (playerGold - goldReserve - goldSpent) / effectiveGoldCost;

                int num = Math.Min(element.Number, Math.Min(readyCount, affordable));
                num = Math.Min(num, maxThisPass - totalPromoted);

                if (num <= 0)
                    continue;

                // Apply (vanilla roster pattern)
                roster.AddToCounts(fromTroop, -num);
                roster.AddToCounts(target, num);

                int actualCost = effectiveGoldCost * num;
                if (Hero.MainHero != null)
                    Hero.MainHero.ChangeHeroGold(-actualCost);

                goldSpent += actualCost;
                totalPromoted += num;
            }

            return totalPromoted;
        }

        /// <summary>
        /// If multiple upgrade paths, pick one uniformly at random.
        /// Otherwise return the single (or null) target.
        /// </summary>
        private static CharacterObject? ChooseUpgradeTargetRandom(CharacterObject fromTroop)
        {
            var targets = fromTroop.UpgradeTargets?.Where(t => t != null).ToArray() ?? Array.Empty<CharacterObject>();
            if (targets.Length == 0)
                return null;
            if (targets.Length == 1)
                return targets[0];

            // Simple random pick (seeded from current campaign time for some determinism between saves but random per day)
            int seed = (int)(CampaignTime.Now.ToSeconds % 9973) ^ targets.Length;
            var rnd = new Random(seed);
            return targets[rnd.Next(targets.Length)];
        }

        /// <summary>
        /// Static helper for the "Force Auto Promotion Now" MCM button.
        /// Immediately runs a promotion pass (respects gold/cost/max settings).
        /// </summary>
        public static void ForcePromotionPass(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || settings == null || !settings.ModEnabled || !settings.AutoPromotionEnabled)
                return;

            var manager = new PromotionManager();
            manager.TryPerformDailyPromotions(party, settings);  // reuse the same logic for force

            Debug.Print("[TroopManagerEnhanced] Force Promotion requested via MCM button.");
        }
    }
}
