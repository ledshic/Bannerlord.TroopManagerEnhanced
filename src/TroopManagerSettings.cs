using System.Collections.Generic;
using System.ComponentModel;
using MCM.Abstractions;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Abstractions.Base.PerSave;
using TaleWorlds.CampaignSystem.Party;

namespace TroopManagerEnhanced
{
    /// <summary>
    /// Promotion frequency options (how often the Automatic Promotion logic runs).
    /// </summary>
    public enum PromotionFrequency
    {
        [Description("Once per day (recommended for performance and balance)")]
        Daily = 0,

        [Description("Once per hour (more responsive)")]
        Hourly = 1,

        [Description("On frequent party/game ticks (very responsive, higher CPU)")]
        OnPartyTick = 2
    }

    /// <summary>
    /// How the mod chooses which upgrade path to take when a troop has multiple possible promotions
    /// (e.g. some cultures have branching troop trees).
    /// </summary>
    public enum PromotionSelectionMode
    {
        [Description("Vanilla Order - Use the first upgrade target defined by the game (most predictable)")]
        VanillaFirst = 0,

        [Description("Random - Pick a random valid upgrade branch each time")]
        Random = 1,

        [Description("Prefer Player Culture - Prioritize branches matching your culture/kingdom if available")]
        PreferPlayerCulture = 2,

        [Description("Highest Tier - Always pick the highest tier available target (greedy power)")]
        HighestTier = 3,

        [Description("Lowest Cost - Pick the cheapest (gold) upgrade option first")]
        LowestCost = 4
    }

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
        public override string Id => "TroopManagerEnhanced_v1";
        public override string DisplayName
        {
            get
            {
                var ver = typeof(TroopManagerSettings).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
                return $"{{=TME_MainDisplay}}Troop Manager Enhanced {ver}";
            }
        }
        public override string FolderName => "TroopManagerEnhanced";
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
            HintText = "{=TME_ShowNotifsHint}Display information messages when the mod upgrades, recruits, or dismisses troops.")]
        [SettingPropertyGroup("{=TME_General}General")]
        public bool ShowNotifications { get; set; } = true;

        // Global feature toggles (for quick enable/disable of major systems)
        [SettingPropertyBool(
            "{=TME_TogglePromo}Auto Promotion (Global)",
            RequireRestart = false,
            HintText = "{=TME_TogglePromoHint}Master toggle for Automatic Promotion feature.")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoPromotionEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_TogglePrisoner}Auto Prisoner Recruit (Global)",
            RequireRestart = false,
            HintText = "{=TME_TogglePrisonerHint}Master toggle for automatic prisoner recruitment (Feature 2).")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoRecruitPrisonersEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_ToggleAccel}Accelerated Recruitment (Global)",
            RequireRestart = false,
            HintText = "{=TME_ToggleAccelHint}Master toggle for the accelerated (pay-to-recruit) feature (Feature 3).")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AcceleratedRecruitmentEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_ToggleSettle}Settlement Auto Recruit (Global)",
            RequireRestart = false,
            HintText = "{=TME_ToggleSettleHint}Master toggle for settlement-based auto recruitment.")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoRecruitEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_ToggleDismiss}Auto Dismiss (Global)",
            RequireRestart = false,
            HintText = "{=TME_ToggleDismissHint}Master toggle for auto dismiss / cleanup.")]
        [SettingPropertyGroup("{=TME_Features}Features")]
        public bool AutoDismissEnabled { get; set; } = true;  // mapped in behavior if needed

        #endregion

        #region Automatic Promotion (Feature 1)

        [SettingPropertyDropdown(
            "{=TME_PromoFreq}Promotion Frequency",
            RequireRestart = false,
            HintText = "{=TME_PromoFreqHint}How often the promotion logic should attempt to run.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion", GroupOrder = 1)]
        public PromotionFrequency PromotionFrequency { get; set; } = PromotionFrequency.Daily;

        [SettingPropertyDropdown(
            "{=TME_UpgradePath}Upgrade Path Selection",
            RequireRestart = false,
            HintText = "{=TME_UpgradePathHint}When a troop has multiple possible upgrades, which path should be chosen automatically?")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public PromotionSelectionMode PromotionSelectionMode { get; set; } = PromotionSelectionMode.PreferPlayerCulture;

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
            "{=TME_MaxPromoPer}Max Promotions Per Check",
            1, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_MaxPromoPerHint}Hard cap on how many individual troop promotions can occur in a single check.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public int MaxPromotionsPerCheck { get; set; } = 20;

        [SettingPropertyBool(
            "{=TME_MultiTier}Allow Multi-Tier In One Pass",
            RequireRestart = false,
            HintText = "{=TME_MultiTierHint}After promoting a troop, immediately try to promote it further (limited because new troops start with 0 XP).")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public bool AllowMultiTierInOnePass { get; set; } = false;

        [SettingPropertyBool(
            "{=TME_SkipWounded}Skip Wounded Troops",
            RequireRestart = false,
            HintText = "{=TME_SkipWoundedHint}Do not promote troops that have any wounded in their stack.")]
        [SettingPropertyGroup("{=TME_Promo}Automatic Promotion")]
        public bool SkipWoundedForPromotion { get; set; } = false;

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

        #region Accelerated Recruitment (Feature 3)

        [SettingPropertyFloatingInteger(
            "{=TME_AccelMult}Accelerated Cost Multiplier",
            1f, 100f,
            "0.0",
            RequireRestart = false,
            HintText = "{=TME_AccelMultHint}Multiplier applied to the total daily wage sum of the prisoner group (default 40).")]
        [SettingPropertyGroup("{=TME_Accel}Accelerated Recruitment", GroupOrder = 3)]
        public float AcceleratedCostMultiplier { get; set; } = 40f;

        [SettingPropertyBool(
            "{=TME_AccelBypass}Bypass Conformity for Accelerated",
            RequireRestart = false,
            HintText = "{=TME_AccelBypassHint}If true, recruit even without vanilla conformity being reached.")]
        [SettingPropertyGroup("{=TME_Accel}Accelerated Recruitment")]
        public bool BypassConformityForAccelerated { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_AccelFull}Recruit Full Stack on Accelerate",
            RequireRestart = false,
            HintText = "{=TME_AccelFullHint}Recruit the entire stack of the prisoner type instead of a single quantity.")]
        [SettingPropertyGroup("{=TME_Accel}Accelerated Recruitment")]
        public bool RecruitFullStackOnAccelerate { get; set; } = true;

        // Existing trigger button (from previous feature)
        private bool _triggerAccelerated;
        private bool _isTriggeringAccelerated;

        [SettingPropertyBool(
            "{=TME_TriggerAccel}Trigger Accelerated Recruitment Now",
            RequireRestart = false,
            HintText = "{=TME_TriggerAccelHint}Pay the accelerated cost and immediately recruit qualifying prisoners.")]
        [SettingPropertyGroup("{=TME_Accel}Accelerated Recruitment")]
        public bool TriggerAcceleratedRecruitment
        {
            get => _triggerAccelerated;
            set
            {
                if (value && !_isTriggeringAccelerated)
                {
                    _isTriggeringAccelerated = true;
                    _triggerAccelerated = false;
                    try
                    {
                        var party = MobileParty.MainParty;
                        if (party != null && ModEnabled && AcceleratedRecruitmentEnabled)
                        {
                            RecruitmentManager.AccelerateRecruitment(party, this);
                        }
                    }
                    finally
                    {
                        _isTriggeringAccelerated = false;
                    }
                }
                else if (!value)
                {
                    _triggerAccelerated = false;
                }
            }
        }

        #endregion

        #region Settlement Auto Recruit & Dismiss (additional)

        [SettingPropertyInteger(
            "{=TME_SettleTarget}Recruit Target Party Size %",
            50, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_SettleTargetHint}Target fill percentage for settlement auto-recruit.")]
        [SettingPropertyGroup("{=TME_Settle}Settlement Auto Recruit")]
        public int RecruitTargetPercentage { get; set; } = 85;

        [SettingPropertyInteger(
            "{=TME_SettleMax}Max Recruits Per Day",
            1, 50, "0",
            RequireRestart = false,
            HintText = "{=TME_SettleMaxHint}Cap on settlement recruits per daily tick.")]
        [SettingPropertyGroup("{=TME_Settle}Settlement Auto Recruit")]
        public int MaxRecruitsPerDay { get; set; } = 12;

        [SettingPropertyBool(
            "{=TME_SettleOnly}Recruit Only When In Settlement",
            RequireRestart = false,
            HintText = "{=TME_SettleOnlyHint}Only recruit when physically inside a settlement.")]
        [SettingPropertyGroup("{=TME_Settle}Settlement Auto Recruit")]
        public bool RecruitOnlyInSettlement { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_DismissLow}Auto Dismiss Excess Low Tier",
            RequireRestart = false,
            HintText = "{=TME_DismissLowHint}Dismiss low-tier troops when near capacity.")]
        [SettingPropertyGroup("{=TME_Dismiss}Auto Dismiss / Cleanup")]
        public bool AutoDismissLowTierEnabled { get; set; } = false;

        [SettingPropertyInteger(
            "{=TME_DismissTier}Dismiss Below Tier",
            1, 6, "0",
            RequireRestart = false,
            HintText = "{=TME_DismissTierHint}Tier threshold for dismissal candidates.")]
        [SettingPropertyGroup("{=TME_Dismiss}Auto Dismiss / Cleanup")]
        public int DismissBelowTier { get; set; } = 2;

        [SettingPropertyBool(
            "{=TME_DismissWounded}Dismiss Heavily Wounded",
            RequireRestart = false,
            HintText = "{=TME_DismissWoundedHint}Dismiss stacks that are mostly wounded.")]
        [SettingPropertyGroup("{=TME_Dismiss}Auto Dismiss / Cleanup")]
        public bool DismissHeavilyWounded { get; set; } = false;

        [SettingPropertyInteger(
            "{=TME_DismissWoundPct}Wounded Dismiss Threshold %",
            50, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_DismissWoundPctHint}If wounded % exceeds this, consider for dismissal.")]
        [SettingPropertyGroup("{=TME_Dismiss}Auto Dismiss / Cleanup")]
        public int WoundedDismissThresholdPercent { get; set; } = 80;

        #endregion

        #region Legacy (compatibility)

        [SettingPropertyBool(
            "{=TME_LegacyPromo}Auto Upgrade Troops (Legacy)",
            RequireRestart = false,
            HintText = "{=TME_LegacyPromoHint}Legacy. Use the main Auto Promotion toggle above instead.")]
        [SettingPropertyGroup("{=TME_Legacy}Legacy (Compatibility)", GroupOrder = 99)]
        public bool AutoUpgradeEnabled { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_LegacyMulti}Upgrade All Eligible Tiers (Legacy)",
            RequireRestart = false,
            HintText = "{=TME_LegacyMultiHint}Legacy. See Allow Multi-Tier option in Promotion group.")]
        [SettingPropertyGroup("{=TME_Legacy}Legacy (Compatibility)")]
        public bool UpgradeAllTiers { get; set; } = true;

        #endregion

        /// <summary>
        /// Built-in presets for quick configuration.
        /// </summary>
        public override IEnumerable<ISettingsPreset> GetBuiltInPresets()
        {
            foreach (var p in base.GetBuiltInPresets()) yield return p;

            yield return new MemorySettingsPreset(Id, "balanced", "{=TME_PresetBalanced}Balanced", () => new TroopManagerSettings
            {
                PromotionCostMultiplier = 1.0f,
                AcceleratedCostMultiplier = 40f,
                MaxPromotionsPerCheck = 15,
                MaxPrisonerRecruitsPerTick = 5
            });

            yield return new MemorySettingsPreset(Id, "aggressive", "{=TME_PresetAggressive}Aggressive", () => new TroopManagerSettings
            {
                PromotionCostMultiplier = 0.5f,
                AcceleratedCostMultiplier = 20f,
                MaxPromotionsPerCheck = 30,
                MaxPrisonerRecruitsPerTick = 12,
                AllowMultiTierInOnePass = true,
                BypassConformityForAccelerated = true
            });

            yield return new MemorySettingsPreset(Id, "conservative", "{=TME_PresetConserv}Conservative", () => new TroopManagerSettings
            {
                PromotionCostMultiplier = 2.0f,
                AcceleratedCostMultiplier = 60f,
                MaxPromotionsPerCheck = 8,
                MaxPrisonerRecruitsPerTick = 2,
                SkipWoundedForPromotion = true
            });
        }

        /// <summary>
        /// Helper to combine Global + PerSave settings for a feature.
        /// Update managers to use these (e.g. if (TroopManagerSettings.IsFeatureEnabled("promotion")) ... )
        /// </summary>
        public static bool IsFeatureEnabled(string featureKey)
        {
            var global = Instance;
            if (global == null || !global.ModEnabled) return false;

            var perSave = TroopManagerPerSaveSettings.Instance;

            return featureKey switch
            {
                "promotion" => global.AutoPromotionEnabled && (perSave?.EnablePromotionThisSave ?? true),
                "prisoner_recruit" => global.AutoRecruitPrisonersEnabled && (perSave?.EnablePrisonerRecruitThisSave ?? true),
                "accelerated" => global.AcceleratedRecruitmentEnabled && (perSave?.EnableAcceleratedThisSave ?? true),
                "settlement_recruit" => global.AutoRecruitEnabled,
                "dismiss" => global.AutoDismissLowTierEnabled || global.DismissHeavilyWounded,
                _ => true
            };
        }

        /// <summary>
        /// Returns effective limit, honoring per-save overrides when present.
        /// </summary>
        public int GetEffectiveMaxPromotionsPerCheck()
        {
            var perSave = TroopManagerPerSaveSettings.Instance;
            if (perSave != null && perSave.PerSavePromotionLimitOverride > 0)
                return perSave.PerSavePromotionLimitOverride;
            return MaxPromotionsPerCheck;
        }
    }

    /// <summary>
    /// Example Per-Save / Per-Campaign settings.
    /// These are saved with the campaign/save file.
    /// Useful for options that should vary per playthrough (e.g. whether a feature is active in this save, or save-specific limits).
    /// MCM will expose these separately (or under a per-save tab depending on implementation).
    /// </summary>
    internal sealed class TroopManagerPerSaveSettings : AttributePerSaveSettings<TroopManagerPerSaveSettings>
    {
        public override string Id => "TroopManagerEnhanced_PerSave_v1";
        public override string DisplayName => "{=TME_PerSaveDisplay}Troop Manager Enhanced (Per-Save)";
        public override string FolderName => "TroopManagerEnhanced";

        [SettingPropertyBool(
            "{=TME_PerSavePromo}Enable Promotion in this Campaign/Save",
            RequireRestart = false,
            HintText = "{=TME_PerSavePromoHint}Per-save override for Auto Promotion.")]
        [SettingPropertyGroup("{=TME_PerSaveGroup}Per-Save Feature Toggles")]
        public bool EnablePromotionThisSave { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_PerSavePris}Enable Prisoner Recruitment in this Campaign/Save",
            RequireRestart = false,
            HintText = "{=TME_PerSavePrisHint}Per-save override for prisoner auto-recruit.")]
        [SettingPropertyGroup("{=TME_PerSaveGroup}Per-Save Feature Toggles")]
        public bool EnablePrisonerRecruitThisSave { get; set; } = true;

        [SettingPropertyBool(
            "{=TME_PerSaveAccel}Enable Accelerated Recruitment in this Campaign/Save",
            RequireRestart = false,
            HintText = "{=TME_PerSaveAccelHint}Per-save override for accelerated feature.")]
        [SettingPropertyGroup("{=TME_PerSaveGroup}Per-Save Feature Toggles")]
        public bool EnableAcceleratedThisSave { get; set; } = true;

        [SettingPropertyInteger(
            "{=TME_PerSavePromoLimit}Per-Save Promotion Limit Override",
            0, 100, "0",
            RequireRestart = false,
            HintText = "{=TME_PerSavePromoLimitHint}0 = use global Max Promotions Per Check. Non-zero overrides for this save only.")]
        [SettingPropertyGroup("{=TME_PerSaveGroup}Per-Save Feature Toggles")]
        public int PerSavePromotionLimitOverride { get; set; } = 0;
    }
}
