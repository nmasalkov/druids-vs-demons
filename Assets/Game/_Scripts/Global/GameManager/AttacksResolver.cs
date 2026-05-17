using System;
using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;

public struct AttackAssignment
{
    public Creature Attacker;
    public Unit Target;
    public float Damage;
    public float ResultHP;
    public bool FatalBlow;
}

public partial class AttacksResolver
{
    public List<AttackAssignment> PlayerAttacks { get; private set; }
    public List<AttackAssignment> EnemyAttacks { get; private set; }
    public List<AttackAssignment> AllAttacks { get; private set; }

    /// <summary>
    /// Set of targets that will die this battle (have at least one FatalBlow assignment).
    /// </summary>
    public HashSet<Unit> DoomedTargets { get; private set; }

    public void Resolve(List<Creature> playerCreatures, List<Creature> enemyCreatures,
        Hero playerHero, Hero enemyHero)
    {
        var enemySimHP = BuildSimulatedHP(enemyCreatures, enemyHero);
        var playerSimHP = BuildSimulatedHP(playerCreatures, playerHero);

        PlayerAttacks = ResolveTeam(playerCreatures, enemyCreatures, enemyHero, enemySimHP);
        EnemyAttacks = ResolveTeam(enemyCreatures, playerCreatures, playerHero, playerSimHP);

        AllAttacks = new List<AttackAssignment>();
        AllAttacks.AddRange(PlayerAttacks);
        AllAttacks.AddRange(EnemyAttacks);

        BuildDoomedTargets();
        RegisterExperienceRewards();
    }

    private void BuildDoomedTargets()
    {
        DoomedTargets = new HashSet<Unit>(
            AllAttacks.Where(a => a.FatalBlow).Select(a => a.Target)
        );
    }

    private void RegisterExperienceRewards()
    {
        var rewarded = new HashSet<(Creature attacker, Unit target)>();

        foreach (var a in AllAttacks)
        {
            if (a.Target is Hero)
            {
                // Hero hit: damage × 10 XP per shot, regardless of death
                int xp = (int)(a.Damage * 10f);
                ExperienceManager.Instance.RegisterPendingXp(a.Attacker, xp);
            }
            else if (a.Target is Creature targetCreature)
            {
                if (!DoomedTargets.Contains(a.Target)) continue;
                if (!rewarded.Add((a.Attacker, a.Target))) continue;

                int xp = targetCreature.Data.GetExperienceReward(targetCreature.Experience.Level);
                ExperienceManager.Instance.RegisterPendingXp(a.Attacker, xp);
            }
        }
    }

    public float ExecuteAttacks(Action onComplete)
    {
        return ExecuteAnimations(AllAttacks);
    }
}
