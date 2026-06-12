using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// Manager responsible for Auto Dismiss / Cleanup logic (low tier excess and heavily wounded troops).
    /// Extracted from TroopManagementBehavior for better separation of concerns and consistency
    /// with the other feature managers.
    /// </summary>
    public class AutoDismissManager
    {
        /// <summary>
        /// Entry point called from the behavior on daily ticks when the feature is enabled.
        /// </summary>
        public void TryPerformAutoDismiss(MobileParty party, TroopManagerSettings settings)
        {
            if (party == null || !party.IsActive || party != MobileParty.MainParty)
                return;

            if (settings == null || !settings.ModEnabled)
                return;

            // Feature is enabled if either low-tier dismiss or heavily-wounded dismiss is on
            if (!settings.AutoDismissLowTierEnabled && !settings.DismissHeavilyWounded)
                return;

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
    }
}