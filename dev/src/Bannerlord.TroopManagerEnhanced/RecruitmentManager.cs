using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
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
                    ConformityNeededPerOne = neededPerOne,
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
                int conformityCost = Math.Max(1, candidate.ConformityNeededPerOne);

                // Mirror vanilla side effects: consume conformity before moving roster counts.
                prisonRoster.AddXpToTroop(troop, -conformityCost * canRecruit);
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

                    int moraleGain = Campaign.Current?.Models?.PrisonerRecruitmentCalculationModel?
                        .GetPrisonerRecruitmentMoraleEffect(party.Party, troop, canRecruit) ?? 0;
                    if (moraleGain != 0)
                        party.RecentEventsMorale += moraleGain;
                }
                catch { /* non-fatal */ }

                totalRecruited += canRecruit;
                remainingSlots -= canRecruit;
                remainingMax -= canRecruit;
            }

            return totalRecruited;
        }

        public int TryRecruitVolunteersFromSettlement(MobileParty party, Settlement settlement, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return 0;

            if (settlement == null)
                return 0;

            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitFromSettlementEnabled)
                return 0;

            if (!settlement.IsVillage && !settlement.IsTown && !settlement.IsCastle)
                return 0;

            if (party.MapFaction != null && settlement.MapFaction != null && FactionManager.IsAtWarAgainstFaction(party.MapFaction, settlement.MapFaction))
                return 0;

            var memberRoster = party.MemberRoster;
            if (memberRoster == null)
                return 0;

            int partySizeLimit = party.Party.PartySizeLimit;
            if (partySizeLimit <= 0)
                return 0;

            int currentCount = memberRoster.TotalManCount;
            int thresholdPercent = Math.Max(1, Math.Min(100, settings.SettlementRecruitThreshold));
            int threshold = Math.Max(1, (int)Math.Ceiling(partySizeLimit * (thresholdPercent / 100.0)));

            if (currentCount >= threshold)
                return 0;

            int freeSlots = Math.Max(0, partySizeLimit - currentCount);
            if (freeSlots <= 0)
                return 0;

            int needed = threshold - currentCount;
            int capPerEntry = Math.Max(1, settings.MaxSettlementRecruitsPerEntry);
            int remaining = Math.Min(needed, Math.Min(freeSlots, capPerEntry));
            if (remaining <= 0)
                return 0;

            var notables = settlement.Notables;
            if (notables == null || notables.Count == 0)
                return 0;

            int recruited = 0;
            var volunteerModel = Campaign.Current?.Models?.VolunteerModel;
            var wageModel = Campaign.Current?.Models?.PartyWageModel;
            var leader = Hero.MainHero;

            if (leader == null || volunteerModel == null || wageModel == null)
                return 0;

            foreach (var notable in notables)
            {
                if (remaining <= 0)
                    break;

                if (notable == null || !notable.IsAlive || notable.VolunteerTypes == null)
                    continue;

                int maxIndexExclusive = volunteerModel.MaximumIndexHeroCanRecruitFromHero(leader, notable);
                if (maxIndexExclusive <= 0)
                    continue;

                int upper = Math.Min(maxIndexExclusive, notable.VolunteerTypes.Length);
                for (int i = 0; i < upper && remaining > 0; i++)
                {
                    var troop = notable.VolunteerTypes[i];
                    if (troop == null)
                        continue;

                    int recruitCost = wageModel.GetTroopRecruitmentCost(troop, leader).RoundedResultNumber;
                    int troopWage = wageModel.GetCharacterWage(troop);

                    if (leader.Gold < recruitCost)
                        continue;

                    if (party.GetAvailableWageBudget() < troopWage)
                        continue;

                    GiveGoldAction.ApplyBetweenCharacters(leader, null, recruitCost, disableNotification: true);
                    notable.VolunteerTypes[i] = null;
                    memberRoster.AddToCounts(troop, 1);

                    try
                    {
                        CampaignEventDispatcher.Instance.OnTroopRecruited(leader, settlement, notable, troop, 1);
                    }
                    catch { /* non-fatal */ }

                    recruited++;
                    remaining--;
                }
            }

            if (recruited > 0 && settings.ShowNotifications)
            {
                var text = new TextObject("{=TME_SETT_RECRUIT_001}Recruited {COUNT} volunteers in {SETTLEMENT}.", null);
                text.SetTextVariable("COUNT", recruited);
                text.SetTextVariable("SETTLEMENT", settlement.Name?.ToString() ?? "settlement");
                InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Cyan));
            }

            return recruited;
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
            public int ConformityNeededPerOne;
            public int Tier;
            public int OriginalIndex;
        }
    }
}
