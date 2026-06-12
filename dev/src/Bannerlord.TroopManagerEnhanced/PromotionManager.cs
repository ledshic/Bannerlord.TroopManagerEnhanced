using System;
using System.Collections.Generic;
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
    /// Main manager class responsible for Automatic Promotion (troop upgrading) logic.
    ///
    /// Design principles (best practices for Bannerlord modding):
    /// - All cost and eligibility decisions go through the VANILLA IPartyTroopUpgradeModel.
    /// - We only perform the actual roster mutation + gold payment after the model has approved the numbers.
    /// - We respect the exact same data structures the game uses (TroopRosterElement, UpgradeTargets, XP per element).
    /// - No invention of new upgrade rules. We borrow the game's internal upgrade paths.
    /// - Multi-branch selection (when a troop has 2+ possible upgrades) is the only "mod" decision.
    /// - Cost multiplier is applied transparently on top of vanilla costs (player sees effective cost in notifications if wanted).
    /// - Party size is never an issue for promotions (1 troop in → 1 troop out).
    ///
    /// This class is intentionally stateless except for a last-run timestamp (used for frequency throttling).
    /// It can be called from DailyTick, HourlyTick, or a high-frequency Tick with internal throttling.
    /// </summary>
    public class PromotionManager
    {
        private CampaignTime _lastPromotionRun = CampaignTime.Zero;

        /// <summary>
        /// Entry point called by the behavior on various ticks.
        /// Decides whether to run based on MCM frequency setting + elapsed time.
        /// </summary>
        public void TryPerformPromotions(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            // Only the player's main land party (ignore naval/ship parties)
            if (party != MobileParty.MainParty) return;

            if (settings == null || !settings.ModEnabled)
                return;

            if (!settings.AutoPromotionEnabled)
                return;

            if (!ShouldRunNow(settings.PromotionFrequency))
                return;

            try
            {
                int promotedCount = PerformPromotionsInternal(party, settings);

                if (promotedCount > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=TME_PROMO_001}Promoted {PROMOTED} troops.", null);
                    text.SetTextVariable("PROMOTED", promotedCount);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));
                }

                _lastPromotionRun = CampaignTime.Now;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][Promotion] Exception: {ex}");
            }
        }

        /// <summary>
        /// Force version for MCM buttons / hotkeys. Ignores the time throttle.
        /// </summary>
        public void ForcePerformPromotions(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty) return;
            if (settings == null || !settings.ModEnabled) return;

            // War Sails / 1.4.5+ safety
            if (party != MobileParty.MainParty) return;

            if (!settings.AutoPromotionEnabled) return;

            try
            {
                int promotedCount = PerformPromotionsInternal(party, settings);

                if (promotedCount > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=TME_PROMO_001}Promoted {PROMOTED} troops.", null);
                    text.SetTextVariable("PROMOTED", promotedCount);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Green));
                }

                _lastPromotionRun = CampaignTime.Now;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][Promotion] Force error: {ex}");
            }
        }

        private bool ShouldRunNow(PromotionFrequency frequency)
        {
            if (_lastPromotionRun == CampaignTime.Zero)
                return true;

            double hoursSinceLast = (CampaignTime.Now - _lastPromotionRun).ToHours;

            switch (frequency)
            {
                case PromotionFrequency.Daily:
                    return hoursSinceLast >= 23.5; // almost a full day, tolerant of tick ordering
                case PromotionFrequency.Hourly:
                    return hoursSinceLast >= 0.95;
                case PromotionFrequency.OnPartyTick:
                    return hoursSinceLast >= 0.08; // roughly every 5 in-game minutes (very frequent)
                default:
                    return hoursSinceLast >= 23.5;
            }
        }

        /// <summary>
        /// Core promotion pass. Scans the roster from the end (safe for mutation), calculates
        /// exactly how many of each stack can be promoted using vanilla XP + gold rules, chooses
        /// the target according to player preference, applies multiplier, pays gold, mutates roster.
        /// </summary>
        private int PerformPromotionsInternal(MobileParty party, TroopManagerSettings settings)
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
            bool allowMultiTier = settings.AllowMultiTierInOnePass;
            bool skipWounded = settings.SkipWoundedForPromotion;
            var selectionMode = settings.PromotionSelectionMode;

            CultureObject? playerCulture = Hero.MainHero?.Culture ?? party.LeaderHero?.Culture;

            int totalPromoted = 0;
            int goldSpent = 0;

            // Iterate backwards – safe when we call AddToCounts (which can change count but we re-evaluate from end each time conceptually).
            // We also support limited multi-tier chaining within the same stack's "budget" of XP.
            for (int i = roster.Count - 1; i >= 0 && totalPromoted < maxThisPass; i--)
            {
                TroopRosterElement element = roster.GetElementCopyAtIndex(i);
                var fromTroop = element.Character as CharacterObject;

                if (fromTroop == null || element.Number <= 0)
                    continue;

                if (fromTroop.UpgradeTargets == null || fromTroop.UpgradeTargets.Length == 0)
                    continue; // cannot be promoted – dead end or hero, etc.

                if (skipWounded && element.WoundedNumber > 0)
                    continue;

                int availableXp = roster.GetElementXp(i);
                if (availableXp < 0) availableXp = 0;

                // Get candidate targets ordered according to player preference
                var orderedTargets = GetOrderedUpgradeTargets(
                    fromTroop,
                    selectionMode,
                    playerCulture,
                    upgradeModel,
                    party.Party,
                    costMultiplier);

                foreach (var target in orderedTargets)
                {
                    if (totalPromoted >= maxThisPass)
                        break;

                    // Ask the VANILLA model for the true costs
                    int xpCost = upgradeModel.GetXpCostForUpgrade(party.Party, fromTroop, target);
                    int baseGoldCost = upgradeModel.GetGoldCostForUpgrade(party.Party, fromTroop, target).RoundedResultNumber;
                    int effectiveGoldCost = (int)(baseGoldCost * costMultiplier);

                    if (effectiveGoldCost < 0) effectiveGoldCost = 0;

                    // How many can we do limited by XP?
                    int byXp = (xpCost <= 0) ? element.Number : (availableXp / xpCost);

                    // How many can we afford?
                    int affordable = (effectiveGoldCost <= 0)
                        ? element.Number
                        : (playerGold - goldReserve - goldSpent) / effectiveGoldCost;

                    int num = Math.Min(element.Number, Math.Min(byXp, affordable));
                    num = Math.Min(num, maxThisPass - totalPromoted);

                    if (num <= 0)
                        continue;

                    // === APPLY THE PROMOTION (vanilla-style roster change + gold payment) ===
                    // This is the same pattern used internally by PartyUpgraderCampaignBehavior after it decides who is ready.

                    // 1. Remove old troops (XP for the remaining low-tier members stays with the reduced stack)
                    roster.AddToCounts(fromTroop, -num);

                    // 2. Add upgraded troops (they start with 0 XP toward their own next tier – this is vanilla behavior)
                    roster.AddToCounts(target, num);

                    // 3. Pay the (modified) gold cost
                    int actualCostThisUpgrade = effectiveGoldCost * num;
                    if (Hero.MainHero != null)
                    {
                        Hero.MainHero.ChangeHeroGold(-actualCostThisUpgrade);
                    }

                    // 4. Consume the XP that was used for these promotions from the original element's perspective.
                    // Because we already removed some of the stack, the XP left on the (possibly smaller) remaining stack
                    // is still correct for the survivors. We don't need to manually touch XP here for the promoted ones.
                    // If there were leftover XP on the old stack and we didn't promote everyone, it remains for future.
                    availableXp -= (xpCost * num);

                    goldSpent += actualCostThisUpgrade;
                    totalPromoted += num;

                    // Optional: immediately try to promote the *newly created* upgraded troops further this pass.
                    // This only has effect if the newly promoted troops somehow already have XP (rare) or if xpCost was 0.
                    // Still useful for certain edge cases / modded troops with very low requirements.
                    if (allowMultiTier && num > 0 && target.UpgradeTargets != null && target.UpgradeTargets.Length > 0)
                    {
                        // We just added them. Find the new stack index for the target and see if we can chain.
                        // For simplicity and safety we do a limited recursive-style attempt here without full re-scan.
                        int chained = TryChainPromotionOnNewlyPromoted(
                            roster,
                            target,
                            upgradeModel,
                            party,
                            playerGold - goldReserve - goldSpent,
                            costMultiplier,
                            maxThisPass - totalPromoted,
                            playerCulture,
                            selectionMode,
                            out int chainedGoldSpent);

                        totalPromoted += chained;
                        goldSpent += chainedGoldSpent;
                    }

                    // If we don't want to try other branches for this original stack, break.
                    // (We already chose the "best" according to ordering.)
                    break;
                }

                // If we promoted from this stack and multi-tier is on, the loop will naturally look at other stacks.
                // Real multi-tier progress for the same lineage usually requires more XP from battles.
            }

            return totalPromoted;
        }

        /// <summary>
        /// After promoting some troops to 'newTierTroop', immediately see if those new troops can be promoted further
        /// right now (only possible in practice if their upgrade has 0 XP cost or they inherited some XP).
        /// This is a best-effort limited helper.
        /// </summary>
        private int TryChainPromotionOnNewlyPromoted(
            TroopRoster roster,
            CharacterObject newlyPromotedTroop,
            PartyTroopUpgradeModel upgradeModel,
            MobileParty party,
            int remainingGoldBudget,
            float costMultiplier,
            int maxRemaining,
            CultureObject? playerCulture,
            PromotionSelectionMode selectionMode,
            out int goldSpentInChain)
        {
            goldSpentInChain = 0;

            if (maxRemaining <= 0 || newlyPromotedTroop.UpgradeTargets == null || newlyPromotedTroop.UpgradeTargets.Length == 0)
                return 0;

            // Find the current stack for the newly promoted type
            int newStackIndex = -1;
            TroopRosterElement newElement = default;

            for (int j = 0; j < roster.Count; j++)
            {
                var el = roster.GetElementCopyAtIndex(j);
                if (el.Character == newlyPromotedTroop)
                {
                    newStackIndex = j;
                    newElement = el;
                    break;
                }
            }

            if (newStackIndex < 0 || newElement.Number <= 0)
                return 0;

            int xp = roster.GetElementXp(newStackIndex);
            if (xp < 0) xp = 0;

            var ordered = GetOrderedUpgradeTargets(
                newlyPromotedTroop, selectionMode, playerCulture, upgradeModel, party.Party, costMultiplier);

            int chained = 0;

            foreach (var nextTarget in ordered)
            {
                if (chained >= maxRemaining) break;

                int xpCost = upgradeModel.GetXpCostForUpgrade(party.Party, newlyPromotedTroop, nextTarget);
                int baseGold = upgradeModel.GetGoldCostForUpgrade(party.Party, newlyPromotedTroop, nextTarget).RoundedResultNumber;
                int effGold = (int)(baseGold * costMultiplier);

                int byXp = (xpCost <= 0) ? newElement.Number : (xp / xpCost);
                int byGold = (effGold <= 0) ? newElement.Number : (remainingGoldBudget / effGold);

                int num = Math.Min(newElement.Number, Math.Min(byXp, byGold));
                num = Math.Min(num, maxRemaining - chained);

                if (num <= 0) continue;

                roster.AddToCounts(newlyPromotedTroop, -num);
                roster.AddToCounts(nextTarget, num);

                int thisCost = effGold * num;
                if (Hero.MainHero != null)
                    Hero.MainHero.ChangeHeroGold(-thisCost);

                goldSpentInChain += thisCost;
                chained += num;
                remainingGoldBudget -= thisCost;
                // Note: newly promoted from this new tier start with low/zero XP again.
            }

            return chained;
        }

        /// <summary>
        /// Static helper for MCM "Force" buttons and hotkeys.
        /// Forces an immediate promotion pass on the main party, respecting current settings (gold reserve, multipliers, selection mode, etc.).
        /// </summary>
        public static void ForcePromotionPass(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || settings == null || !settings.ModEnabled || !settings.AutoPromotionEnabled)
                return;

            var manager = new PromotionManager();
            manager.ForcePerformPromotions(party, settings);

            Debug.Print("[TroopManagerEnhanced] Force Promotion requested via MCM/button/hotkey.");
        }

        /// <summary>
        /// Returns the list of possible upgrade targets sorted according to the player's chosen selection mode.
        /// This is the "AI" decision part of the feature. Everything else tries to stay as close to vanilla as possible.
        /// </summary>
        private List<CharacterObject> GetOrderedUpgradeTargets(
            CharacterObject fromTroop,
            PromotionSelectionMode mode,
            CultureObject? playerCulture,
            PartyTroopUpgradeModel upgradeModel,
            PartyBase partyBaseForCosts,
            float costMultiplier)
        {
            var targets = fromTroop.UpgradeTargets?.Where(t => t != null).ToList() ?? new List<CharacterObject>();

            if (targets.Count <= 1)
                return targets;

            switch (mode)
            {
                case PromotionSelectionMode.Random:
                    // Fisher-Yates shuffle for true randomness per check
                    var rnd = new Random((int)(CampaignTime.Now.ToSeconds % int.MaxValue));
                    for (int n = targets.Count - 1; n > 0; n--)
                    {
                        int k = rnd.Next(n + 1);
                        var temp = targets[k];
                        targets[k] = targets[n];
                        targets[n] = temp;
                    }
                    return targets;

                case PromotionSelectionMode.PreferPlayerCulture:
                    return targets
                        .OrderByDescending(t => (t.Culture != null && playerCulture != null && t.Culture == playerCulture) ? 100 : 0)
                        .ThenByDescending(t => t.Tier)
                        .ToList();

                case PromotionSelectionMode.HighestTier:
                    return targets.OrderByDescending(t => t.Tier).ToList();

                case PromotionSelectionMode.LowestCost:
                    return targets
                        .OrderBy(t =>
                        {
                            int g = upgradeModel.GetGoldCostForUpgrade(partyBaseForCosts, fromTroop, t).RoundedResultNumber;
                            return (int)(g * costMultiplier);
                        })
                        .ToList();

                case PromotionSelectionMode.VanillaFirst:
                default:
                    // Return in the order the game defined them (first target is usually the "default" branch)
                    return targets;
            }
        }
    }
}
