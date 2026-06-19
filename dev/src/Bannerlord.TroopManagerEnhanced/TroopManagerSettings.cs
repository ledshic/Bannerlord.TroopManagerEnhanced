using System.Collections.Generic;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace Bannerlord.TroopManagerEnhanced
{
    /// <summary>
    /// MCMv5 Global settings for TroopManagerEnhanced.
    /// All options appear in Mod Options under "Troop Manager Enhanced".
    ///
    /// FormatType = "json" ensures settings are persisted to disk.
    ///
    /// Localization: All user-facing strings use {=TME_xxx} keys.
    /// Add translations to ModuleData/Languages/EN/sta_strings.xml (and other languages):
    ///   &lt;string id="TME_EnableMod" text="Enable Mod" /&gt;
    ///   &lt;string id="TME_EnableModHint" text="Master toggle..." /&gt;
    /// etc.
    /// </summary>
    public sealed class TroopManagerSettings : AttributeGlobalSettings<TroopManagerSettings>
    {
        public override string Id => "Bannerlord.TroopManagerEnhanced_v1";
        public override string DisplayName
        {
            get
            {
                var ver = typeof(TroopManagerSettings).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
                return new TextObject("{=TME_MainDisplay}Troop Manager Enhanced {VERSION}", new Dictionary<string, object>
                {
                    { "VERSION", ver }
                }).ToString();
            }
        }
        public override string FolderName => "Bannerlord.TroopManagerEnhanced";
        public override string FormatType => "json";

        // Example of using Fluent Builder as alternative (call from SubModule if you prefer runtime registration over attributes):
        // var builder = BaseSettingsBuilder.Create("TroopManagerEnhanced_Fluent", "{=TME_MainDisplay}Troop Manager Enhanced")!
        //     .SetFormat("json")
        //     .CreateGroup("{=TME_General}General", g => g
        //         .AddBool("mod_enabled", "{=TME_EnableMod}Enable Mod", new ProxyRef<bool>(() => ModEnabled, v => ModEnabled = v), ... ));
        // var global = builder.BuildAsGlobal();
        // global.Register();

        #region General / Master Toggles

        [SettingPropertyBool(
            "{=TME_EnableMod}Enable Mod",
            RequireRestart = false,
            HintText = "{=TME_EnableModHint}Master toggle. When off, no automatic troop actions will occur.")]
        [SettingPropertyGroup("{=TME_General}General")]
        public bool ModEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_ShowNotifs}Show Notifications",
            RequireRestart = false,
            HintText = "{=TME_ShowNotifsHint}Display information messages when the mod upgrades, recruits, or recruits troops.")]
        [SettingPropertyGroup("{=TME_General}General")]
        public bool ShowNotifications { get; set; } = true;

        // Global feature toggles (for quick enable/disable of major systems)
        [SettingPropertyBool(
            "{=TME_TogglePromo}Auto Promotion",
            RequireRestart = false,
            HintText = "{=TME_TogglePromoHint}Master toggle for Automatic Promotion feature.")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoPromotionEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_TogglePrisoner}Auto Prisoner Recruit",
            RequireRestart = false,
            HintText = "{=TME_TogglePrisonerHint}Master toggle for automatic prisoner recruitment (daily conformity check + slots).")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoRecruitPrisonersEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_ToggleSettlement}Auto Settlement Recruit",
            RequireRestart = false,
            HintText = "{=TME_ToggleSettlementHint}When entering a non-hostile village/castle/town, auto recruit volunteers if party size is below threshold.")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoRecruitFromSettlementEnabled { get; set; } = true;

        #endregion

        #region Automatic Promotion (daily, full EXP + random branch)

        [SettingPropertyFloatingInteger(
            "{=TME_PromoCostMult}Cost Multiplier",
            0.1f, 5.0f,
            "0.00",
            RequireRestart = false,
            HintText = "{=TME_PromoCostMultHint}Multiply the vanilla gold cost by this value (0.1x = very cheap, 5.0x = expensive).")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public float PromotionCostMultiplier { get; set; } = 1.0f;

        [SettingPropertyInteger(
            "{=TME_MinGold}Minimum Gold Reserve",
            0, 100000, "0",
            RequireRestart = false,
            HintText = "{=TME_MinGoldHint}Never spend gold below this reserve on promotions.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public int MinimumGoldReserve { get; set; } = 500;

        [SettingPropertyInteger(
            "{=TME_MaxPromoPer}Max Promotions Per Day",
            1, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_MaxPromoPerHint}Hard cap on how many individual troop promotions can occur in the daily pass.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public int MaxPromotionsPerCheck { get; set; } = 20;

        [SettingPropertyBool(
            "{=TME_SkipBranched}Skip Branched Promotions",
            RequireRestart = false,
            HintText = "{=TME_SkipBranchedHint}When enabled, troops with multiple upgrade paths will NOT be auto-promoted. They are left for the player to choose manually.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public bool SkipBranchedPromotions { get; set; } = false;

        // Button for manual trigger
        private bool _forcePromo;
        private bool _isForcingPromo;

        [SettingPropertyBool(
            "{=TME_ForcePromo}Force Auto Promotion Now",
            RequireRestart = false,
            HintText = "{=TME_ForcePromoHint}Toggle ON to immediately run a promotion pass on your main party. Resets automatically.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public bool ForcePromotionNow
        {
            get => _forcePromo;
            set
            {
                if (value && !_isForcingPromo)
                {
                    _isForcingPromo = true;
                    _forcePromo = false;

                    try
                    {
                        var party = MobileParty.MainParty;
                        if (party != null && ModEnabled && AutoPromotionEnabled)
                        {
                            PromotionManager.ForcePromotionPass(party, this);
                        }
                    }
                    finally
                    {
                        _isForcingPromo = false;
                    }
                }
                else if (!value)
                {
                    _forcePromo = false;
                }
            }
        }

        #endregion

        #region Automatic Prisoner Recruitment (Feature 2)

        [SettingPropertyInteger(
            "{=TME_PrisTier}Minimum Prisoner Tier",
            0, 6, "0",
            RequireRestart = false,
            HintText = "{=TME_PrisTierHint}Only recruit prisoners of this tier or higher.")]
        [SettingPropertyGroup("{=TME_Pris}Automatic Prisoner Recruitment", GroupOrder = 2)]
        public int MinimumPrisonerRecruitTier { get; set; } = 1;

        [SettingPropertyBool(
            "{=TME_OnlyExisting}Only Recruit Existing Troop Types",
            RequireRestart = false,
            HintText = "{=TME_OnlyExistingHint}Only recruit prisoner types already present in your party.")]
        [SettingPropertyGroup("{=TME_Pris}Automatic Prisoner Recruitment")]
        public bool OnlyRecruitExistingTroopTypes { get; set; } = false;

        [SettingPropertyInteger(
            "{=TME_MaxPrisPer}Max Recruits Per Tick",
            1, 50, "0",
            RequireRestart = false,
            HintText = "{=TME_MaxPrisPerHint}Hard cap on prisoners recruited per check.")]
        [SettingPropertyGroup("{=TME_Pris}Automatic Prisoner Recruitment")]
        public int MaxPrisonerRecruitsPerTick { get; set; } = 5;

        [SettingPropertyBool(
            "{=TME_PrisHighTier}Prioritize High Tier Prisoners",
            RequireRestart = false,
            HintText = "{=TME_PrisHighTierHint}Recruit higher tier first.")]
        [SettingPropertyGroup("{=TME_Pris}Automatic Prisoner Recruitment")]
        public bool PrioritizeHighTierPrisoners { get; set; } = true;

        // Manual trigger button for prisoner recruitment
        private bool _forcePrisRecruit;
        private bool _isForcingPris;

        [SettingPropertyBool(
            "{=TME_ForcePris}Force Prisoner Recruitment Now",
            RequireRestart = false,
            HintText = "{=TME_ForcePrisHint}Immediately attempt to recruit available prisoners (respects free slots and settings).")]
        [SettingPropertyGroup("{=TME_Pris}Automatic Prisoner Recruitment")]
        public bool ForcePrisonerRecruitNow
        {
            get => _forcePrisRecruit;
            set
            {
                if (value && !_isForcingPris)
                {
                    _isForcingPris = true;
                    _forcePrisRecruit = false;
                    try
                    {
                        var party = MobileParty.MainParty;
                        if (party != null && ModEnabled && AutoRecruitPrisonersEnabled)
                        {
                            // Reuse the manager logic
                            new RecruitmentManager().TryRecruitPrisoners(party, this);
                        }
                    }
                    finally { _isForcingPris = false; }
                }
                else if (!value) _forcePrisRecruit = false;
            }
        }

        #endregion

        #region Settlement Entry Auto Recruitment

        [SettingPropertyInteger(
            "{=TME_SettThreshold}Recruit Threshold (%)",
            1, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_SettThresholdHint}If your party size ratio is below this percentage when entering a recruitable settlement, the mod will recruit volunteers up to this percentage of party size limit.")]
        [SettingPropertyGroup("{=TME_Sett}Settlement Auto Recruitment", GroupOrder = 3)]
        public int SettlementRecruitThreshold { get; set; } = 80;

        [SettingPropertyInteger(
            "{=TME_SettMaxPerEntry}Max Recruits Per Entry",
            1, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_SettMaxPerEntryHint}Hard cap of volunteers recruited on each settlement entry trigger.")]
        [SettingPropertyGroup("{=TME_Sett}Settlement Auto Recruitment")]
        public int MaxSettlementRecruitsPerEntry { get; set; } = 20;

        [SettingPropertyInteger(
            "{=TME_SettCooldown}Same Settlement Cooldown (Hours)",
            0, 24, "0",
            RequireRestart = false,
            HintText = "{=TME_SettCooldownHint}Minimum hours before the same settlement can trigger auto recruitment again. 0 = no cooldown.")]
        [SettingPropertyGroup("{=TME_Sett}Settlement Auto Recruitment")]
        public int SettlementRecruitCooldownHours { get; set; } = 8;

        #endregion

        // Auto Dismiss and Accelerated Recruitment were removed in prior steps.

        /// <summary>
        /// Feature enablement for the (now very small) set of active systems.
        /// Removed keys intentionally return false.
        /// </summary>
        public static bool IsFeatureEnabled(string featureKey)
        {
            var global = Instance;
            if (global == null || !global.ModEnabled) return false;

            return featureKey switch
            {
                "promotion" => global.AutoPromotionEnabled,
                "prisoner_recruit" => global.AutoRecruitPrisonersEnabled,
                "settlement_recruit" => global.AutoRecruitFromSettlementEnabled,
                _ => false
            };
        }
    }
}
