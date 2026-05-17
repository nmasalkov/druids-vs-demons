using System;
using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;

public struct AttackAssignment
{
    public Creature Attacker;
    public Creature Target;
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
    public HashSet<Creature> DoomedTargets { get; private set; }

    public void Resolve(List<Creature> playerCreatures, List<Creature> enemyCreatures)
    {
        var enemySimHP = BuildSimulatedHP(enemyCreatures);
        var playerSimHP = BuildSimulatedHP(playerCreatures);

        PlayerAttacks = ResolveTeam(playerCreatures, enemyCreatures, enemySimHP);
        EnemyAttacks = ResolveTeam(enemyCreatures, playerCreatures, playerSimHP);

        AllAttacks = new List<AttackAssignment>();
        AllAttacks.AddRange(PlayerAttacks);
        AllAttacks.AddRange(EnemyAttacks);

        BuildDoomedTargets();
        RegisterExperienceRewards();
    }

    private void BuildDoomedTargets()
    {
        DoomedTargets = new HashSet<Creature>(
            AllAttacks.Where(a => a.FatalBlow).Select(a => a.Target)
        );
    }

    private void RegisterExperienceRewards()
    {
        var rewarded = new HashSet<(Creature attacker, Creature target)>();

        foreach (var a in AllAttacks)
        {
            if (!DoomedTargets.Contains(a.Target)) continue;
            if (!rewarded.Add((a.Attacker, a.Target))) continue;

            int xp = a.Target.Data.GetExperienceReward(a.Target.Experience.Level);
            ExperienceManager.Instance.RegisterPendingXp(a.Attacker, xp);
        }
    }

    public float ExecuteAttacks(Action onComplete)
    {
        return ExecuteAnimations(AllAttacks);
    }
}
