/// <summary>
/// Applies claimed BoostSO reward bonuses to a resolver's raw balance-number read. Call at the
/// point a resolver reads damage/hp/chance/etc. off an ActionSO — see docs/Rewards.md for the
/// full list of call sites (AttacksResolver, FireMagicResolver, StarfallResolver, CharmResolver,
/// BattleCryResolver, ShieldResolver).
/// </summary>
public static class RewardBonuses
{
    public static float ApplyBonuses(ActionSO action, float baseValue)
    {
        float bonus = 0f;
        foreach (var id in CampaignStateManager.Instance.CurrentRun.boostRewardIds)
        {
            if (G.RewardList.Find(id) is BoostSO boost && boost.action == action)
                bonus += boost.percentage;
        }
        return baseValue * (1f + bonus);
    }
}
