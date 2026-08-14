using System.Collections.Generic;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>
/// The enemy AI's decision service (see docs/AI.md). Every Decide* method is a pure function of
/// live game state — it never touches SlotMachine/SlotColumn or any UI, never subscribes to their
/// events, and has no coroutines. RollState is the only thing that ever acts on a decision by
/// calling into SlotMachine, the same way a player's UI click would.
///
/// The one piece of genuine state this class owns is the fight-wide reroll pool
/// (RerollsRemaining/SpendReroll) — that's the AI's own resource, mirroring how EnergyController
/// owns the player's energy, not a SlotMachine coupling.
/// </summary>
public partial class AIController : MonoBehaviour
{
    public static AIController Instance { get; private set; }
    private int _rerollsRemaining;

    void Awake() { Instance = this; }

    void Start()
    {
        ResetRerollPool();
        GameManager.OnBattleRestart += ResetRerollPool;
    }

    void OnDestroy() { GameManager.OnBattleRestart -= ResetRerollPool; }

    private void ResetRerollPool()
    {
        var fight = CampaignStateManager.Instance.CurrentFight;
        if (fight == null) return; // boot-time redirect race into a non-fight encounter, see docs/Campaign.md
        _rerollsRemaining = fight.enemyData.rerollsAmount;
    }

    public static int RerollsRemaining => Instance._rerollsRemaining;
    public static void SpendReroll() => Instance._rerollsRemaining = Mathf.Max(0, Instance._rerollsRemaining - 1);

    public static bool RollForProbability(int successChancePercent) =>
        Random.Range(0, 100) < successChancePercent;

    public static bool DecideShouldSummonCreatures() => new ShouldSummonCreaturesDecision().Decide();

    public static CreatureSO DecidePreferredCreatureType(CreatureSO excludeCreature = null) =>
        new PreferredCreatureTypeDecision().Decide(excludeCreature);

    public static ActionSO DecidePreferredNukeOrSpell(ActionSO excludeAction = null) =>
        new PickActionDecision().Decide(excludeAction);

    public static int DecideRerollBudget(IReadOnlyList<ActionSO> initialLandedSlots, ActionSO desiredAction) =>
        new RerollBudgetDecision().Decide(initialLandedSlots, desiredAction);

    public static RerollChoice DecideReroll(IReadOnlyList<ActionSO> currentSlots, ActionSO desiredAction,
        int rerollsUsedSoFar, int rerollBudget) =>
        new ShouldRerollDecision().Decide(currentSlots, desiredAction, rerollsUsedSoFar, rerollBudget);
}
