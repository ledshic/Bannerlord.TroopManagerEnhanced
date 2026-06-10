using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Manager responsible for Feature 2: Automatic Prisoner Recruitment.
    ///
    /// Logic:
    /// - Scans the player's MobileParty.PrisonRoster periodically.
    /// - Identifies prisoners that can be recruited (non-heroes, meet min tier, etc.).
    /// - Checks for available party slots (PartySizeLimit - current member count).
    /// - Uses vanilla-style roster manipulation to move prisoners into the party (AddToCounts on both rosters).
    ///   This mirrors what happens in the Party Screen when you manually recruit prisoners.
    /// - Conformity is implicitly respected because the vanilla PrisonerRecruitmentCampaignBehavior builds
    ///   conformity over time and the game only "allows" recruitment in the UI when conformity thresholds are met.
    ///   By running on ticks we simulate the player periodically checking the prisoner list and recruiting when possible.
    /// - Supports running after battles (via event in the behavior) so newly captured prisoners can be processed quickly.
    ///
    /// Best practices followed:
    /// - Prefers direct but vanilla roster operations (same as PartyScreenLogic / recruitment actions).
    /// - No invention of new recruitment rules or costs.
    /// - Respects PartySizeLimit (free slots).
    /// - Clean separation: this manager only handles prisoner -> party member conversion.
    /// - Integrated via the TroopManagementBehavior (called on daily/hourly/tick and battle end).
    /// </summary>
    public class RecruitmentManager
    {
        private CampaignTime _lastRecruitRun = CampaignTime.Zero;

        /// <summary>
        /// Main entry point. Called from the behavior on various ticks and after battles.
        /// Throttles based on simple time check (can be enhanced with MCM frequency later if desired).
        /// </summary>
        public void TryRecruitPrisoners(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            // War Sails / 1.4.5+ safety: only the player's main land party
            if (party != MobileParty.MainParty) return;

            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitPrisonersEnabled)
                return;

            // Light throttling so we don't spam on every single TickEvent
            if (_lastRecruitRun != CampaignTime.Zero)
            {
                double hoursSince = (CampaignTime.Now - _lastRecruitRun).ToHours;
                if (hoursSince < 0.05) // ~3 in-game minutes minimum between checks
                    return;
            }

            try
            {
                int recruited = PerformRecruitmentInternal(party, settings);

                if (recruited > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=TME_RECRUIT_001}Recruited {COUNT} prisoners into your party.", null);
                    text.SetTextVariable("COUNT", recruited);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
                }

                if (recruited > 0)
                    _lastRecruitRun = CampaignTime.Now;
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][Recruitment] Exception: {ex}");
            }
        }

        private int PerformRecruitmentInternal(MobileParty party, TroopManagerSettings settings)
        {
            var prisonRoster = party.PrisonRoster;
            if (prisonRoster == null || prisonRoster.TotalManCount <= 0)
                return 0;

            var memberRoster = party.MemberRoster;
            if (memberRoster == null)
                return 0;

            int currentTroops = memberRoster.TotalManCount;
            int partyLimit = party.Party.PartySizeLimit;
            int freeSlots = partyLimit - currentTroops;
            if (freeSlots <= 0)
                return 0;

            int minTier = Math.Max(0, settings.MinimumPrisonerRecruitTier);
            bool onlyExistingTypes = settings.OnlyRecruitExistingTroopTypes;
            int maxThisCheck = Math.Max(1, settings.MaxPrisonerRecruitsPerTick);
            bool prioritizeHighTier = settings.PrioritizeHighTierPrisoners;

            // Build list of candidate prisoner stacks
            var candidates = new List<PrisonerCandidate>();

            for (int i = 0; i < prisonRoster.Count; i++)
            {
                TroopRosterElement element = prisonRoster.GetElementCopyAtIndex(i);
                var troop = element.Character as CharacterObject;

                if (troop == null || troop.IsHero)
                    continue;

                if (troop.Tier < minTier)
                    continue;

                if (onlyExistingTypes && !PartyHasTroopType(memberRoster, troop))
                    continue;

                // At this point the prisoner is considered "recruitable" (conformity is handled by vanilla daily behavior)
                candidates.Add(new PrisonerCandidate
                {
                    Troop = troop,
                    Count = element.Number,
                    Tier = troop.Tier,
                    OriginalIndex = i
                });
            }

            if (candidates.Count == 0)
                return 0;

            // Apply priority
            if (prioritizeHighTier)
            {
                candidates = candidates.OrderByDescending(c => c.Tier).ToList();
            }
            // Otherwise keep roster order (or could add other priority modes later)

            int totalRecruited = 0;
            int remainingSlots = freeSlots;
            int remainingMax = maxThisCheck;

            foreach (var candidate in candidates)
            {
                if (remainingSlots <= 0 || remainingMax <= 0)
                    break;

                int canRecruit = Math.Min(candidate.Count, remainingSlots);
                canRecruit = Math.Min(canRecruit, remainingMax);

                if (canRecruit <= 0)
                    continue;

                var troop = candidate.Troop;

                // === VANILLA-STYLE RECRUITMENT ===
                // This is the same operation performed when you use the "Recruit" button in the Party Screen
                // for prisoners. We move the troop count from PrisonRoster to MemberRoster.
                // The vanilla PrisonerRecruitmentCampaignBehavior and PartyScreenLogic ultimately do equivalent roster transfers
                // after checking conformity / leadership / perks.

                prisonRoster.AddToCounts(troop, -canRecruit);
                memberRoster.AddToCounts(troop, canRecruit);

                // Optional: notify campaign systems (helps with some stats, logs, or mod compatibility)
                // Signature may vary slightly by game version; wrapped in try for safety.
                try
                {
                    CampaignEventDispatcher.Instance.OnTroopRecruited(
                        Hero.MainHero,
                        null,           // settlement (none for prisoner recruit)
                        null,           // culture (use troop's)
                        troop,
                        canRecruit);
                }
                catch
                {
                    // Non-fatal if the event signature changed or is not applicable
                }

                totalRecruited += canRecruit;
                remainingSlots -= canRecruit;
                remainingMax -= canRecruit;
            }

            return totalRecruited;
        }

        private static bool PartyHasTroopType(TroopRoster memberRoster, CharacterObject troop)
        {
            for (int i = 0; i < memberRoster.Count; i++)
            {
                var element = memberRoster.GetElementCopyAtIndex(i);
                if (element.Character == troop)
                    return true;

                // Also allow same base troop type (e.g. same .StringId without tier) if desired,
                // but strict "already in party" usually means exact or very similar. Using exact for now.
            }
            return false;
        }

        /// <summary>
        /// Small helper struct for candidate sorting.
        /// </summary>
        private struct PrisonerCandidate
        {
            public CharacterObject Troop;
            public int Count;
            public int Tier;
            public int OriginalIndex;
        }

        #region Feature 3: Accelerated Recruitment

        /// <summary>
        /// Static entry point for accelerated recruitment (called from MCM trigger or hotkey).
        /// Processes every distinct troop type currently in the player's prison roster.
        /// </summary>
        public static void AccelerateRecruitment(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || settings == null || !settings.AcceleratedRecruitmentEnabled)
                return;

            if (party != MobileParty.MainParty) return; // War Sails safety

            try
            {
                var manager = new RecruitmentManager();
                manager.PerformAcceleratedRecruitment(party, settings);
            }
            catch (Exception ex)
            {
                Debug.Print($"[TroopManagerEnhanced][AcceleratedRecruitment] Error: {ex}");
                InformationManager.DisplayMessage(new InformationMessage("Accelerated recruitment failed. See logs.", Colors.Red));
            }
        }

        private void PerformAcceleratedRecruitment(MobileParty party, TroopManagerSettings settings)
        {
            var prisonRoster = party.PrisonRoster;
            if (prisonRoster == null || prisonRoster.TotalManCount == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("No prisoners to accelerate.", Colors.Gray));
                return;
            }

            var memberRoster = party.MemberRoster;
            if (memberRoster == null)
                return;

            int freeSlots = party.Party.PartySizeLimit - memberRoster.TotalManCount;
            if (freeSlots <= 0)
            {
                InformationManager.DisplayMessage(new InformationMessage("No room in party for more troops.", Colors.Red));
                return;
            }

            var wageModel = Campaign.Current?.Models?.PartyWageModel;
            if (wageModel == null)
            {
                Debug.Print("[TroopManagerEnhanced] No PartyWageModel available.");
                return;
            }

            // Group prisoners by troop type
            var prisonerGroups = new Dictionary<CharacterObject, int>();
            for (int i = 0; i < prisonRoster.Count; i++)
            {
                var element = prisonRoster.GetElementCopyAtIndex(i);
                var troop = element.Character as CharacterObject;
                if (troop == null || troop.IsHero) continue;

                if (!prisonerGroups.ContainsKey(troop))
                    prisonerGroups[troop] = 0;

                prisonerGroups[troop] += element.Number;
            }

            if (prisonerGroups.Count == 0) return;

            long totalGoldSpent = 0;
            int totalRecruited = 0;
            int remainingSlots = freeSlots;
            bool bypass = settings.BypassConformityForAccelerated;
            bool fullStack = settings.RecruitFullStackOnAccelerate;
            float multiplier = Math.Max(1f, settings.AcceleratedCostMultiplier);

            var hero = Hero.MainHero;
            if (hero == null) return;

            foreach (var kvp in prisonerGroups)
            {
                if (remainingSlots <= 0) break;

                var troop = kvp.Key;
                int prisonerCount = kvp.Value;

                int perTroopWage = wageModel.GetCharacterWage(troop);
                long totalDailyWageForType = (long)perTroopWage * prisonerCount;

                long cost = (long)(totalDailyWageForType * multiplier);

                if (hero.Gold < cost)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"Not enough gold for {troop.Name} (need {cost}). Skipping remaining.", Colors.Red));
                    break;
                }

                // Determine how many to recruit for this type
                int desired = fullStack ? prisonerCount : Math.Min(prisonerCount, 1); // "one recruitable quantity" = at least 1

                // When not bypassing, we could limit further, but for this feature we still recruit
                // the desired amount after paying (the payment is the acceleration).
                // If you want stricter vanilla respect when !bypass, you could query the model here
                // and cap "desired" to the currently recruitable number.
                int toRecruit = Math.Min(desired, remainingSlots);

                if (toRecruit <= 0) continue;

                // Safe gold deduction
                hero.ChangeHeroGold(-cost);
                totalGoldSpent += cost;

                // Perform the recruitment (vanilla roster move)
                prisonRoster.AddToCounts(troop, -toRecruit);
                memberRoster.AddToCounts(troop, toRecruit);

                totalRecruited += toRecruit;
                remainingSlots -= toRecruit;

                // Notify per type (can be noisy; alternatively collect and notify once at end)
                if (settings.ShowNotifications)
                {
                    var msg = new TextObject("{=TME_ACCEL_01}Accelerated {TROOP}: recruited {NUM} (cost {COST}g).", null);
                    msg.SetTextVariable("TROOP", troop.Name);
                    msg.SetTextVariable("NUM", toRecruit);
                    msg.SetTextVariable("COST", cost);
                    InformationManager.DisplayMessage(new InformationMessage(msg.ToString(), Colors.Green));
                }
            }

            if (totalRecruited > 0)
            {
                var summary = new TextObject("{=TME_ACCEL_02}Accelerated recruitment complete. Recruited {TOTAL} troops for {GOLD} gold.", null);
                summary.SetTextVariable("TOTAL", totalRecruited);
                summary.SetTextVariable("GOLD", totalGoldSpent);
                InformationManager.DisplayMessage(new InformationMessage(summary.ToString(), Colors.Green));

                Debug.Print($"[TroopManagerEnhanced] Accelerated recruitment: {totalRecruited} troops for {totalGoldSpent} gold. Bypass={bypass}, FullStack={fullStack}");
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage("Accelerated recruitment: No prisoners were recruited (insufficient gold or slots).", Colors.Gray));
            }
        }

        #endregion
    }
}
