using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Manager for automatic prisoner recruitment.
    ///
    /// Fixed logic (per request):
    /// - Runs daily (via behavior OnDailyTick).
    /// - For each prisoner stack: compute how many have "stand by for recruit" based on accumulated conformity.
    ///   Uses:
    ///     - Campaign.Current.Models.PrisonerRecruitmentModel.GetConformityNeededToRecruitPrisoner(...)
    ///     - prisonRoster.GetElementXp(index)  --> this holds the current conformity points for that prisoner type (vanilla storage).
    ///   ready = currentConformity / neededPerOne
    /// - Only recruit up to ready count, respecting party free slots + max per day + other filters (tier, only-existing, high-tier priority).
    /// - Vanilla roster transfer (prison -> member) + optional OnTroopRecruited event.
    /// - No accelerated / pay-to-recruit logic anymore (completely removed).
    /// </summary>
    public class RecruitmentManager
    {
        /// <summary>
        /// Entry point called daily by the behavior when the feature is enabled.
        /// </summary>
        public void TryRecruitPrisoners(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitPrisonersEnabled)
                return;

            try
            {
                int recruited = PerformRecruitmentInternal(party, settings);

                if (recruited > 0 && settings.ShowNotifications)
                {
                    var text = new TextObject("{=TME_RECRUIT_001}Recruited {COUNT} prisoners into your party.", null);
                    text.SetTextVariable("COUNT", recruited);
                    InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
                }
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

            int freeSlots = party.Party.PartySizeLimit - memberRoster.TotalManCount;
            if (freeSlots <= 0)
                return 0;

            int minTier = Math.Max(0, settings.MinimumPrisonerRecruitTier);
            bool onlyExistingTypes = settings.OnlyRecruitExistingTroopTypes;
            int maxThisCheck = Math.Max(1, settings.MaxPrisonerRecruitsPerTick);
            bool prioritizeHighTier = settings.PrioritizeHighTierPrisoners;

            var recruitmentModel = Campaign.Current?.Models?.PrisonerRecruitmentCalculationModel;

            // Build candidates that have at least some conformity progress
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

                // === CONFORMITY CHECK (the fix) ===
                int currentConformity = prisonRoster.GetElementXp(i);   // For prison rosters, XP field stores conformity points
                int neededPerOne = 100; // safe default

                if (recruitmentModel != null)
                {
                    neededPerOne = recruitmentModel.GetConformityNeededToRecruitPrisoner(troop);
                    if (neededPerOne <= 0) neededPerOne = 1;
                }

                int readyFromConformity = currentConformity / neededPerOne;
                if (readyFromConformity <= 0)
                    continue; // Not "stand by for recruit" yet today.

                candidates.Add(new PrisonerCandidate
                {
                    Troop = troop,
                    Count = element.Number,
                    Ready = readyFromConformity,
                    Tier = troop.Tier,
                    OriginalIndex = i
                });
            }

            if (candidates.Count == 0)
                return 0;

            // Priority: high tier first (if enabled)
            if (prioritizeHighTier)
                candidates = candidates.OrderByDescending(c => c.Tier).ToList();

            int totalRecruited = 0;
            int remainingSlots = freeSlots;
            int remainingMax = maxThisCheck;

            foreach (var candidate in candidates)
            {
                if (remainingSlots <= 0 || remainingMax <= 0)
                    break;

                int canRecruit = Math.Min(candidate.Count, Math.Min(candidate.Ready, remainingSlots));
                canRecruit = Math.Min(canRecruit, remainingMax);

                if (canRecruit <= 0)
                    continue;

                var troop = candidate.Troop;

                // Vanilla-style move (exact same as manual recruit in party screen)
                prisonRoster.AddToCounts(troop, -canRecruit);
                memberRoster.AddToCounts(troop, canRecruit);

                try
                {
                    CampaignEventDispatcher.Instance.OnTroopRecruited(
                        Hero.MainHero,
                        null,
                        null,
                        troop,
                        canRecruit);
                }
                catch { /* non-fatal */ }

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
                if (memberRoster.GetElementCopyAtIndex(i).Character == troop)
                    return true;
            }
            return false;
        }

        private struct PrisonerCandidate
        {
            public CharacterObject Troop;
            public int Count;
            public int Ready;   // how many are conformity-ready right now
            public int Tier;
            public int OriginalIndex;
        }
    }
}
