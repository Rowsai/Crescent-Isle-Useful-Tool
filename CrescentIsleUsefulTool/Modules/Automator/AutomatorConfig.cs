using System.Collections.Generic;
using Ocelot.Config.Attributes;
using Ocelot.Modules;

namespace CrescentIsleUsefulTool.Modules.Automator;

public class AutomatorConfig : ModuleConfig
{
    // North Horn entries are rendered as a compact, tabbed catalogue in the
    // config window instead of expanding the generated settings page by 28
    // additional rows. Missing entries default to enabled.
    public Dictionary<uint, bool> NorthCriticalEncounters { get; set; } = [];

    public Dictionary<uint, bool> NorthFates { get; set; } = [];

    [Checkbox]
    [Automation]
    [RequiredPlugin("Lifestream", "vnavmesh")]
    [Label("generic.label.enabled")]
    [Tooltip("enabled")]
    public bool Enabled { get; set; } = false;

    [Enum(typeof(AiType), nameof(AiTypeProvider))]
    public AiType AiProvider { get; set; } = AiType.VBM;

    [Checkbox] public bool ToggleAiProvider { get; set; } = true;

    public bool ShouldToggleAiProvider
    {
        get => IsPropertyEnabled(nameof(ToggleAiProvider));
    }

    [Checkbox] public bool ForceTarget { get; set; } = true;

    public bool ShouldForceTarget
    {
        get => IsPropertyEnabled(nameof(ForceTarget));
    }

    [Checkbox]
    [DependsOn(nameof(ForceTarget))]

    public bool ForceTargetCentralEnemy { get; set; } = true;

    public bool ShouldForceTargetCentralEnemy
    {
        get => IsPropertyEnabled(nameof(ForceTargetCentralEnemy));
    }

    [FloatRange(5f, 30f)] public float EngagementRange { get; set; } = 5f;

    // Critical Encounters
    [Checkbox] public bool DoCriticalEncounters { get; set; } = true;

    public bool ShouldDoCriticalEncounters
    {
        get => IsPropertyEnabled(nameof(DoCriticalEncounters));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]

    public bool DelayCriticalEncounters { get; set; } = false;

    public bool ShouldDelayCriticalEncounters
    {
        get => IsPropertyEnabled(nameof(DelayCriticalEncounters));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoScourgeOfTheMind { get; set; } = true;

    public bool ShouldDoScourgeOfTheMind
    {
        get => IsPropertyEnabled(nameof(DoScourgeOfTheMind));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoTheBlackRegiment { get; set; } = true;

    public bool ShouldDoTheBlackRegiment
    {
        get => IsPropertyEnabled(nameof(DoTheBlackRegiment));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoTheUnbridled { get; set; } = true;

    public bool ShouldDoTheUnbridled
    {
        get => IsPropertyEnabled(nameof(DoTheUnbridled));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoCrawlingDeath { get; set; } = true;

    public bool ShouldDoCrawlingDeath
    {
        get => IsPropertyEnabled(nameof(DoCrawlingDeath));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoCalamityBound { get; set; } = true;

    public bool ShouldDoCalamityBound
    {
        get => IsPropertyEnabled(nameof(DoCalamityBound));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoTrialByClaw { get; set; } = true;

    public bool ShouldDoTrialByClaw
    {
        get => IsPropertyEnabled(nameof(DoTrialByClaw));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoFromTimesBygone { get; set; } = true;

    public bool ShouldDoFromTimesBygone
    {
        get => IsPropertyEnabled(nameof(DoFromTimesBygone));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoCompanyOfStone { get; set; } = true;

    public bool ShouldDoCompanyOfStone
    {
        get => IsPropertyEnabled(nameof(DoCompanyOfStone));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoSharkAttack { get; set; } = true;

    public bool ShouldDoSharkAttack
    {
        get => IsPropertyEnabled(nameof(DoSharkAttack));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoCriticalEncounters))]

    public bool DoOnTheHunt { get; set; } = true;

    public bool ShouldDoOnTheHunt
    {
        get => IsPropertyEnabled(nameof(DoOnTheHunt));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoWithExtremePrejudice { get; set; } = true;

    public bool ShouldDoWithExtremePrejudice
    {
        get => IsPropertyEnabled(nameof(DoWithExtremePrejudice));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoNoiseComplaint { get; set; } = true;

    public bool ShouldDoNoiseComplaint
    {
        get => IsPropertyEnabled(nameof(DoNoiseComplaint));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoCursedConcern { get; set; } = true;

    public bool ShouldDoCursedConcern
    {
        get => IsPropertyEnabled(nameof(DoCursedConcern));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoEternalWatch { get; set; } = true;

    public bool ShouldDoEternalWatch
    {
        get => IsPropertyEnabled(nameof(DoEternalWatch));
    }

    [Checkbox]
    [DependsOn(nameof(DoCriticalEncounters))]
    [Indent]

    public bool DoFlameOfDusk { get; set; } = true;

    public bool ShouldDoFlameOfDusk
    {
        get => IsPropertyEnabled(nameof(DoFlameOfDusk));
    }

    // Fates
    [Checkbox] public bool DoFates { get; set; } = true;

    public bool ShouldDoFates
    {
        get => IsPropertyEnabled(nameof(DoFates));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoRoughWaters { get; set; } = true;

    public bool ShouldDoRoughWaters
    {
        get => IsPropertyEnabled(nameof(DoRoughWaters));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoTheGoldenGuardian { get; set; } = true;

    public bool ShouldDoTheGoldenGuardian
    {
        get => IsPropertyEnabled(nameof(DoTheGoldenGuardian));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoKingOfTheCrescent { get; set; } = true;

    public bool ShouldDoKingOfTheCrescent
    {
        get => IsPropertyEnabled(nameof(DoKingOfTheCrescent));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]
    [Experimental]

    public bool DoTheWingedTerror { get; set; } = false;

    public bool ShouldDoTheWingedTerror
    {
        get => IsPropertyEnabled(nameof(DoTheWingedTerror));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoAnUnendingDuty { get; set; } = true;

    public bool ShouldDoAnUnendingDuty
    {
        get => IsPropertyEnabled(nameof(DoAnUnendingDuty));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoBrainDrain { get; set; } = true;

    public bool ShouldDoBrainDrain
    {
        get => IsPropertyEnabled(nameof(DoBrainDrain));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoADelicateBalance { get; set; } = true;

    public bool ShouldDoADelicateBalance
    {
        get => IsPropertyEnabled(nameof(DoADelicateBalance));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoSwornToSoil { get; set; } = true;

    public bool ShouldDoSwornToSoil
    {
        get => IsPropertyEnabled(nameof(DoSwornToSoil));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoAPryingEye { get; set; } = true;

    public bool ShouldDoAPryingEye
    {
        get => IsPropertyEnabled(nameof(DoAPryingEye));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoFatalAllure { get; set; } = true;

    public bool ShouldDoFatalAllure
    {
        get => IsPropertyEnabled(nameof(DoFatalAllure));
    }

    [Checkbox]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoServingDarkness { get; set; } = true;

    public bool ShouldDoServingDarkness
    {
        get => IsPropertyEnabled(nameof(DoServingDarkness));
    }

    [Checkbox]
    [Experimental]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoPersistentPots { get; set; } = false;

    public bool ShouldDoPersistentPots
    {
        get => IsPropertyEnabled(nameof(DoPersistentPots));
    }

    [Checkbox]
    [Experimental]
    [Indent]
    [DependsOn(nameof(DoFates))]

    public bool DoPleadingPots { get; set; } = false;

    public bool ShouldDoPleadingPots
    {
        get => IsPropertyEnabled(nameof(DoPleadingPots));
    }

    public IReadOnlyDictionary<uint, bool> CriticalEncountersMap
    {
        get => new Dictionary<uint, bool>
        {
            { 33, ShouldDoScourgeOfTheMind },
            { 34, ShouldDoTheBlackRegiment },
            { 35, ShouldDoTheUnbridled },
            { 36, ShouldDoCrawlingDeath },
            { 37, ShouldDoCalamityBound },
            { 38, ShouldDoTrialByClaw },
            { 39, ShouldDoFromTimesBygone },
            { 40, ShouldDoCompanyOfStone },
            { 41, ShouldDoSharkAttack },
            { 42, ShouldDoOnTheHunt },
            { 43, ShouldDoWithExtremePrejudice },
            { 44, ShouldDoNoiseComplaint },
            { 45, ShouldDoCursedConcern },
            { 46, ShouldDoEternalWatch },
            { 47, ShouldDoFlameOfDusk },
        };
    }

    public IReadOnlyDictionary<uint, bool> FatesMap
    {
        get => new Dictionary<uint, bool>
        {
            { 1962, ShouldDoRoughWaters },
            { 1963, ShouldDoTheGoldenGuardian },
            { 1964, ShouldDoKingOfTheCrescent },
            { 1965, ShouldDoTheWingedTerror },
            { 1966, ShouldDoAnUnendingDuty },
            { 1967, ShouldDoBrainDrain },
            { 1968, ShouldDoADelicateBalance },
            { 1969, ShouldDoSwornToSoil },
            { 1970, ShouldDoAPryingEye },
            { 1971, ShouldDoFatalAllure },
            { 1972, ShouldDoServingDarkness },
            { 1976, ShouldDoPersistentPots },
            { 1977, ShouldDoPleadingPots },
        };
    }

    public bool IsNorthCriticalEncounterEnabled(uint id)
    {
        return !NorthCriticalEncounters.TryGetValue(id, out var enabled) || enabled;
    }

    public bool IsNorthFateEnabled(uint id)
    {
        return !NorthFates.TryGetValue(id, out var enabled) || enabled;
    }

    public void SetSouthCriticalEncounterEnabled(uint id, bool enabled)
    {
        switch (id)
        {
            case 33: DoScourgeOfTheMind = enabled; break;
            case 34: DoTheBlackRegiment = enabled; break;
            case 35: DoTheUnbridled = enabled; break;
            case 36: DoCrawlingDeath = enabled; break;
            case 37: DoCalamityBound = enabled; break;
            case 38: DoTrialByClaw = enabled; break;
            case 39: DoFromTimesBygone = enabled; break;
            case 40: DoCompanyOfStone = enabled; break;
            case 41: DoSharkAttack = enabled; break;
            case 42: DoOnTheHunt = enabled; break;
            case 43: DoWithExtremePrejudice = enabled; break;
            case 44: DoNoiseComplaint = enabled; break;
            case 45: DoCursedConcern = enabled; break;
            case 46: DoEternalWatch = enabled; break;
            case 47: DoFlameOfDusk = enabled; break;
        }
    }

    public void SetSouthFateEnabled(uint id, bool enabled)
    {
        switch (id)
        {
            case 1962: DoRoughWaters = enabled; break;
            case 1963: DoTheGoldenGuardian = enabled; break;
            case 1964: DoKingOfTheCrescent = enabled; break;
            case 1965: DoTheWingedTerror = enabled; break;
            case 1966: DoAnUnendingDuty = enabled; break;
            case 1967: DoBrainDrain = enabled; break;
            case 1968: DoADelicateBalance = enabled; break;
            case 1969: DoSwornToSoil = enabled; break;
            case 1970: DoAPryingEye = enabled; break;
            case 1971: DoFatalAllure = enabled; break;
            case 1972: DoServingDarkness = enabled; break;
            case 1976: DoPersistentPots = enabled; break;
            case 1977: DoPleadingPots = enabled; break;
        }
    }
}
