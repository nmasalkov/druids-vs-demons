using System;
using System.Collections.Generic;
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

    public void Resolve(List<Creature> playerCreatures, List<Creature> enemyCreatures)
    {
        var enemySimHP = BuildSimulatedHP(enemyCreatures);
        var playerSimHP = BuildSimulatedHP(playerCreatures);

        PlayerAttacks = ResolveTeam(playerCreatures, enemyCreatures, enemySimHP);
        EnemyAttacks = ResolveTeam(enemyCreatures, playerCreatures, playerSimHP);
    }

    public float ExecuteAttacks(Action onComplete)
    {
        var allAttacks = new List<AttackAssignment>();
        allAttacks.AddRange(PlayerAttacks);
        allAttacks.AddRange(EnemyAttacks);
        return ExecuteAnimations(allAttacks);
    }
}
