using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Optional helper class for troop-related utilities.
    /// Keeping logic here makes the Behavior and future patches cleaner.
    /// </summary>
    public static class TroopManagerHelper
    {
        /// <summary>
        /// Returns the number of troops in the party that are at or below the given tier.
        /// </summary>
        public static int CountTroopsBelowTier(MobileParty party, int maxTierInclusive)
        {
            if (party?.MemberRoster == null)
                return 0;

            int count = 0;
            var roster = party.MemberRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                if (element.Character is CharacterObject co && co.Tier <= maxTierInclusive)
                {
                    count += element.Number;
                }
            }
            return count;
        }

        /// <summary>
        /// Gets a reasonable "basic recruit" CharacterObject for a settlement or culture.
        /// Prefers the culture's declared BasicTroop.
        /// </summary>
        public static CharacterObject? GetBasicRecruit(Settlement? settlement, CultureObject? fallbackCulture = null)
        {
            var culture = settlement?.Culture ?? fallbackCulture ?? Hero.MainHero?.Culture;
            if (culture?.BasicTroop != null)
                return culture.BasicTroop;

            return CharacterObject.All.FirstOrDefault(c =>
                c.Culture == culture &&
                c.Tier <= 2 &&
                c.UpgradeTargets?.Length > 0 &&
                c.Occupation == Occupation.Soldier);
        }

        /// <summary>
        /// Simple check: does this party have room for more troops (respecting size limit)?
        /// </summary>
        public static bool HasRoomForRecruits(MobileParty party, int desiredCount = 1)
        {
            if (party?.Party == null)
                return false;

            int current = party.MemberRoster?.TotalManCount ?? 0;
            int limit = party.Party.PartySizeLimit;
            return current + desiredCount <= limit;
        }
    }
}
