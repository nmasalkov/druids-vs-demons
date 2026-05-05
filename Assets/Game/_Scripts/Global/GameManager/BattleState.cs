using System.Collections.Generic;
using Game._Scripts.Creatures;

public class BattleState : GameState
{
    public override void OnStateStart()
    {
        // Delay one frame so all creatures' Start() has run
        Utils.DoAfterDelay.Execute(BeginBattle, 0f);
    }

    public void BeginBattle()
    {
        var allCreatures = new List<Creature>();
        allCreatures.AddRange(G.PlayerCreaturesManager.GetAllCreatures());
        allCreatures.AddRange(G.EnemyCreaturesManager.GetAllCreatures());

        if (allCreatures.Count == 0)
        {
            CompleteState();
            return;
        }

        float maxDuration = 0f;

        foreach (var creature in allCreatures)
        {
            creature.Animator.PlayAttack();
            float duration = creature.Animator.GetAttackDuration();
            if (duration > maxDuration) maxDuration = duration;
        }

        Utils.DoAfterDelay.Execute(CompleteState, maxDuration);
    }
}
