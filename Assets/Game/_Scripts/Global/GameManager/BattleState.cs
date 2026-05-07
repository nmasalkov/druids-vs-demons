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
        var playerCreatures = G.PlayerCreaturesManager.GetAllCreatures();
        var enemyCreatures = G.EnemyCreaturesManager.GetAllCreatures();

        var allCreatures = new List<Creature>();
        allCreatures.AddRange(playerCreatures);
        allCreatures.AddRange(enemyCreatures);

        if (allCreatures.Count == 0)
        {
            CompleteState();
            return;
        }

        float maxDuration = 0f;

        foreach (var creature in playerCreatures)
        {
            Creature target = GetRandomTarget(enemyCreatures);
            creature.Animator.AttackCreature(target);
            float duration = creature.Animator.GetAttackDuration();
            if (duration > maxDuration) maxDuration = duration;
        }

        foreach (var creature in enemyCreatures)
        {
            Creature target = GetRandomTarget(playerCreatures);
            creature.Animator.AttackCreature(target);
            float duration = creature.Animator.GetAttackDuration();
            if (duration > maxDuration) maxDuration = duration;
        }

        Utils.DoAfterDelay.Execute(CompleteState, maxDuration);
    }

    private Creature GetRandomTarget(List<Creature> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }
}
