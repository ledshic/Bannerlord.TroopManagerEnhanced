using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Manager responsible for Settlement-based Auto Recruit (filling the party with basic recruits).
    /// Extracted from TroopManagementBehavior for better separation of concerns and consistency
    /// with PromotionManager / RecruitmentManager.
    /// </summary>
    public class AutoRecruitManager
    {
        /// <summary>
        /// Entry point called from the behavior on daily ticks when the feature is enabled.
        /// </summary>
        public void TryPerformAutoRecruit(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            if (settings == null || !settings.ModEnabled || !settings.AutoRecruitEnabled)
                return;

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

            CharacterObject? recruit = FindBasicRecruitForCulture(culture);
            if (recruit == null)
                return;

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
    }
}