using TaleWorlds.CampaignSystem.Party;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Optional helper class for troop-related utilities.
    /// (Dead methods for removed AutoDismiss / Settlement AutoRecruit features have been pruned.)
    /// </summary>
    public static class TroopManagerHelper
    {
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
