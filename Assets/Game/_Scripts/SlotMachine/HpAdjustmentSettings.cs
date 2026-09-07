using System;
using UnityEngine;

/// <summary>
/// One side's HP-based Dirty Triple Index adjustment curve (most-severe-tier-wins, not cumulative —
/// see SlotMachineRigger.HpAdjustment). Shared by SlotMachineRigger (author-time/fallback defaults,
/// live-tweakable in Play mode) and FightSO (per-fight authored override, applied at fight start via
/// SlotMachineRigger.ApplyFightOverrides). See docs/SlotMachine.md.
/// </summary>
[Serializable]
public struct HpAdjustmentSettings
{
    [Tooltip("HP% above which the 'high HP' tier applies (0-1, e.g. 0.6 = 60%). Highest HP tier " +
             "checked; only wins if HP is above this and no lower tier's threshold also matched.")]
    public float highHpThreshold;
    [Tooltip("Dirty Triple Index adjustment applied when HP is above highHpThreshold. Usually " +
             "negative — harder to complete a triple while comfortably ahead on HP.")]
    public int highHpAdjustment;
    [Tooltip("HP% below which the 'low HP' tier applies (0-1, e.g. 0.25 = 25%).")]
    public float lowHpThreshold;
    [Tooltip("Dirty Triple Index adjustment applied when HP is below lowHpThreshold (and not also " +
             "below a more severe tier). Usually positive — easier triples when behind on HP.")]
    public int lowHpAdjustment;
    [Tooltip("HP% below which the 'critical HP' tier applies (0-1, e.g. 0.15 = 15%). More severe " +
             "than lowHpThreshold — must be a lower value to ever take effect.")]
    public float criticalHpThreshold;
    [Tooltip("Dirty Triple Index adjustment applied when HP is below criticalHpThreshold (and not " +
             "also below nearDeathHpThreshold).")]
    public int criticalHpAdjustment;
    [Tooltip("HP% below which the 'near death' tier applies (0-1, e.g. 0.10 = 10%). The most severe " +
             "tier — must be the lowest of the four thresholds to ever take effect.")]
    public float nearDeathHpThreshold;
    [Tooltip("Dirty Triple Index adjustment applied when HP is below nearDeathHpThreshold. The " +
             "strongest comeback bias — checked first, wins over every other tier.")]
    public int nearDeathHpAdjustment;

    public static HpAdjustmentSettings Default => new HpAdjustmentSettings
    {
        highHpThreshold = 0.6f, highHpAdjustment = -20,
        lowHpThreshold = 0.25f, lowHpAdjustment = 30,
        criticalHpThreshold = 0.15f, criticalHpAdjustment = 50,
        nearDeathHpThreshold = 0.10f, nearDeathHpAdjustment = 65,
    };
}
