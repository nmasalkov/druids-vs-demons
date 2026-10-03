using System;
using System.Collections.Generic;
using System.Linq;
using Game._Scripts.Creatures;

public struct AttackAssignment
{
    public Creature Attacker;
    public Targetable Target;
    public float Damage;
    public float ResultHP;
    public bool FatalBlow;
    public bool IsCritical;

    /// <summary>True for a counterattack (docs/Battle.md "Counterattacks"): Attacker is the
    /// counter-tank, Target is the creature whose hit triggered it (or the Shield guarding that
    /// creature's side, while it's up). Never animated on its own — it
    /// plays from its trigger's hit callback.</summary>
    public bool IsCounter;

    /// <summary>True when this hit landed on a counter-tank and triggered a counter, stored at
    /// <see cref="AttacksResolver.CounterAttacks"/>[<see cref="CounterIndex"/>].</summary>
    public bool HasCounter;
    public int CounterIndex;
}

public partial class AttacksResolver
{
    public List<AttackAssignment> PlayerAttacks { get; private set; }
    public List<AttackAssignment> EnemyAttacks { get; private set; }
    public List<AttackAssignment> AllAttacks { get; private set; }

    /// <summary>Counterattacks, also present in <see cref="AllAttacks"/> (so XP, doomed targets and
    /// the instant path treat them as ordinary hits); indexed by the trigger's CounterIndex.</summary>
    public List<AttackAssignment> CounterAttacks { get; } = new();

    /// <summary>
    /// Set of targets that will die this battle (have at least one FatalBlow assignment).
    /// </summary>
    public HashSet<Targetable> DoomedTargets { get; private set; }

    /// <summary>
    /// Hero → the living Shield standing in front of it this battle. Lets the animation pass hold a
    /// tank headed for a hero until the tank breaking that hero's shield has struck (see
    /// ExecuteAnimations), so the second tank never appears to run straight through the shield.
    /// </summary>
    private readonly Dictionary<Targetable, Shield> _guardingShields = new();

    public void Resolve(List<Creature> playerCreatures, List<Creature> enemyCreatures,
        Hero playerHero, Hero enemyHero, Shield playerShield, Shield enemyShield)
    {
        RegisterGuardingShield(playerHero, playerShield);
        RegisterGuardingShield(enemyHero, enemyShield);

        var enemySimHP = BuildSimulatedHP(enemyCreatures, enemyHero, enemyShield);
        var playerSimHP = BuildSimulatedHP(playerCreatures, playerHero, playerShield);

        // Heals first (docs/Battle.md "Healing shots"): the damage passes below see healed HP.
        ResolveHeals(playerCreatures, playerHero, playerSimHP, isPlayerSide: true);
        ResolveHeals(enemyCreatures, enemyHero, enemySimHP, isPlayerSide: false);

        PlayerAttacks = ResolveTeam(playerCreatures, enemyCreatures, enemyHero, enemyShield, enemySimHP, isPlayerSide: true);
        EnemyAttacks = ResolveTeam(enemyCreatures, playerCreatures, playerHero, playerShield, playerSimHP, isPlayerSide: false);

        AllAttacks = new List<AttackAssignment>();
        AllAttacks.AddRange(PlayerAttacks);
        AllAttacks.AddRange(EnemyAttacks);

        ResolveCounters(playerSimHP, enemySimHP, playerShield, enemyShield);

        BuildDoomedTargets();
        RegisterExperienceRewards();
    }

    private void RegisterGuardingShield(Hero hero, Shield shield)
    {
        if (hero == null || shield == null || shield.Health.IsDead()) return;
        _guardingShields[hero] = shield;
    }

    private void BuildDoomedTargets()
    {
        DoomedTargets = new HashSet<Targetable>(
            AllAttacks.Where(a => a.FatalBlow).Select(a => a.Target)
        );
    }

    private void RegisterExperienceRewards()
    {
        var rewarded = new HashSet<(Creature attacker, Targetable target)>();

        foreach (var a in AllAttacks)
        {
            if (a.Target is Shield) continue;

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

    /// <summary>
    /// Instant path (rule 7): lands every planned heal, then every planned hit, immediately in resolve order — no
    /// animations, no delays. Gems don't spawn; the XP registered during
    /// <see cref="Resolve"/> is granted via <c>ExperienceManager.ResolveGemsInstant()</c>.
    /// </summary>
    public void ApplyAttacksInstant()
    {
        foreach (var h in Heals)
            h.Target.Health.Heal(h.Amount, clearsStatuses: false);
        foreach (var a in AllAttacks)
            a.Target.Health.TakeDamage(a.Damage);
    }
}
