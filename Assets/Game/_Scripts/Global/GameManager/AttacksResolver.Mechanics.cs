using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Creatures;
using Game._Scripts.Creatures;
using UnityEngine;

public partial class AttacksResolver
{
    private static readonly Dictionary<Type, int> PriorityOrder = new()
    {
        { typeof(MageSO), 0 },
        { typeof(ArcherSO), 1 },
        { typeof(TankSO), 2 }
    };

    private Dictionary<Targetable, float> BuildSimulatedHP(List<Creature> creatures, Hero hero, Shield shield)
    {
        var hp = new Dictionary<Targetable, float>();
        foreach (var c in creatures)
            hp[c] = c.Health.CurrentHealth;
        if (hero != null && !hero.Health.IsDead())
            hp[hero] = hero.Health.CurrentHealth;
        if (shield != null && !shield.Health.IsDead())
            hp[shield] = shield.Health.CurrentHealth;
        return hp;
    }

    private int GetPriority(Creature creature)
    {
        var dataType = creature.Data.GetType();
        return PriorityOrder.TryGetValue(dataType, out int p) ? p : 99;
    }

    private List<Creature> SortByPriority(List<Creature> creatures)
    {
        return creatures.OrderBy(GetPriority).ToList();
    }

    /// <summary>
    /// Returns the next attackable target. Priority: enemy shield (if alive) → highest priority
    /// alive creature → enemy hero.
    /// </summary>
    private Targetable GetHighestPriorityAliveTarget(List<Creature> enemies, Hero hero, Shield shield,
        Dictionary<Targetable, float> simHP)
    {
        if (shield != null && simHP.TryGetValue(shield, out float shieldHp) && shieldHp > 0)
            return shield;

        var creature = enemies
            .Where(e => simHP.TryGetValue(e, out float hp) && hp > 0)
            .OrderBy(GetPriority)
            .FirstOrDefault();

        if (creature != null) return creature;

        // Fallback to hero
        if (hero != null && simHP.TryGetValue(hero, out float heroHp) && heroHp > 0)
            return hero;

        return null;
    }

    private List<AttackAssignment> ResolveTeam(
        List<Creature> attackers,
        List<Creature> enemies,
        Hero enemyHero,
        Shield enemyShield,
        Dictionary<Targetable, float> enemySimHP)
    {
        var assignments = new List<AttackAssignment>();
        var sorted = SortByPriority(attackers);

        foreach (var attacker in sorted)
        {
            // Shocked creatures skip their turn entirely.
            if (attacker.StatusesManager.IsShocked) continue;

            var stats = attacker.Data.Stats(attacker.Experience.Level);
            float buffedDamage = stats.damage * attacker.StatusesManager.AttackDamageMultiplier;
            if (buffedDamage <= 0f) continue; // BattleCry Energy Drain reduced damage to 0 — skip turn
            int hitCount = stats.numberOfAttacks;

            for (int i = 0; i < hitCount; i++)
            {
                var target = GetHighestPriorityAliveTarget(enemies, enemyHero, enemyShield, enemySimHP);
                if (target == null) break;

                // Per hit, not per attacker: a multi-hit archer can hit a Shield on hit 1 and a
                // Creature on hit 2, and target-dependent modifiers must see each target.
                float dmgPerHit = ApplySpecialModifiers(attacker, target, buffedDamage);

                float hpBefore = enemySimHP[target];
                float hpAfter = Mathf.Max(0f, hpBefore - dmgPerHit);
                bool fatal = hpAfter <= 0f;

                enemySimHP[target] = hpAfter;

                assignments.Add(new AttackAssignment
                {
                    Attacker = attacker,
                    Target = target,
                    Damage = dmgPerHit,
                    ResultHP = hpAfter,
                    FatalBlow = fatal
                });
            }
        }

        return assignments;
    }

    /// <summary>
    /// Runs the attacker's <see cref="CreatureSO.specialDamageModifiers"/> over
    /// <paramref name="damage"/> in array order, each one taking the running value and returning
    /// the next (so several stack multiplicatively). Applied after the BattleCry multiplier, once
    /// the target is known — the single hook for per-creature damage rules like ShieldBreaker, so
    /// adding one never touches this class. See docs/Battle.md.
    /// </summary>
    private float ApplySpecialModifiers(Creature attacker, Targetable target, float damage)
    {
        var context = new DamageContext(attacker, target);
        foreach (var modifier in attacker.Data.specialDamageModifiers)
            damage = modifier.ModifyDamage(in context, damage);
        return damage;
    }

    private float ExecuteAnimations(List<AttackAssignment> allAttacks)
    {
        // Group assignments by attacker to handle multi-hit archers
        var byAttacker = new Dictionary<Creature, List<AttackAssignment>>();
        foreach (var a in allAttacks)
        {
            if (!byAttacker.ContainsKey(a.Attacker))
                byAttacker[a.Attacker] = new List<AttackAssignment>();
            byAttacker[a.Attacker].Add(a);
        }

        // Build a flat list with one entry per attacker (first target for animation)
        var uniqueAttacks = new List<AttackAssignment>();
        foreach (var kvp in byAttacker)
        {
            uniqueAttacks.Add(kvp.Value[0]);
        }

        // Detect mutual tank fight
        TankAnimator mutualTankA = null;
        TankAnimator mutualTankB = null;

        foreach (var a in uniqueAttacks)
        {
            if (a.Attacker.Animator is not TankAnimator) continue;
            foreach (var b in uniqueAttacks)
            {
                if (b.Attacker.Animator is not TankAnimator) continue;
                if ((Targetable)a.Attacker == b.Target && (Targetable)b.Attacker == a.Target && a.Attacker != b.Attacker)
                {
                    mutualTankA = (TankAnimator)a.Attacker.Animator;
                    mutualTankB = (TankAnimator)b.Attacker.Animator;
                    break;
                }
            }
            if (mutualTankA != null) break;
        }

        var handledTanks = new HashSet<Creature>();
        float maxDuration = 0f;

        // Handle mutual tank battle
        if (mutualTankA != null && mutualTankB != null)
        {
            var creatureA = mutualTankA.GetComponent<Creature>();
            var creatureB = mutualTankB.GetComponent<Creature>();

            var hitsA = BuildHitInfos(byAttacker[creatureA]);
            var hitsB = BuildHitInfos(byAttacker[creatureB]);

            mutualTankA.AttackWithHits(hitsA);

            float arriveTime = mutualTankA.GetRangedDelay() + mutualTankA.GetRunDuration();
            Utils.DoAfterDelay.Execute(() =>
            {
                mutualTankB.SetPendingOnHit(hitsB.Count > 0 ? hitsB[0].OnHit : null);
                mutualTankB.PlayAttackInPlace(null);
            }, arriveTime);

            float dur = mutualTankA.GetAttackDuration();
            if (dur > maxDuration) maxDuration = dur;

            handledTanks.Add(creatureA);
            handledTanks.Add(creatureB);
        }

        // Remaining tanks, grouped by target and chained: tanks sharing one target take turns
        // (each launches as the previous one leaps back) so they never pile up on the same
        // melee position at once.
        var tankChains = uniqueAttacks
            .Where(a => a.Attacker.Animator is TankAnimator && !handledTanks.Contains(a.Attacker))
            .GroupBy(a => a.Target);

        foreach (var chain in tankChains)
        {
            float dur = ExecuteTankChain(chain.ToList(), byAttacker);
            if (dur > maxDuration) maxDuration = dur;
            foreach (var a in chain) handledTanks.Add(a.Attacker);
        }

        // Handle all other attacks (ranged attackers stay in their slots — simultaneous is fine)
        foreach (var a in uniqueAttacks)
        {
            if (handledTanks.Contains(a.Attacker)) continue;

            var hits = BuildHitInfos(byAttacker[a.Attacker]);
            a.Attacker.Animator.AttackWithHits(hits);

            float dur = a.Attacker.Animator.GetAttackDuration();
            if (dur > maxDuration) maxDuration = dur;
        }

        return maxDuration;
    }

    /// <summary>
    /// Plays a group of tank attacks that all share one target as a queue. The head tank follows
    /// the normal rules (waits for a tank opponent's leap-back, otherwise attacks right away);
    /// each subsequent tank waits for the previous chain member's leap-back before starting its
    /// own run. Returns the total choreography duration.
    /// </summary>
    private float ExecuteTankChain(List<AttackAssignment> chain,
        Dictionary<Creature, List<AttackAssignment>> byAttacker)
    {
        float duration = StartChainHead(chain[0], byAttacker, out var previousTank);

        for (int i = 1; i < chain.Count; i++)
        {
            var assignment = chain[i];
            var tank = (TankAnimator)assignment.Attacker.Animator;
            var hits = BuildHitInfos(byAttacker[assignment.Attacker]);

            tank.WaitThenAttack(assignment.Target, hits.Count > 0 ? hits[0].OnHit : null);
            previousTank.OnLeapBackStarted += tank.StartPendingAttack;

            // Chained tanks start with no ranged delay: run + attack + return each.
            duration += tank.GetAttackDuration() - tank.GetRangedDelay();
            previousTank = tank;
        }

        return duration;
    }

    private float StartChainHead(AttackAssignment head,
        Dictionary<Creature, List<AttackAssignment>> byAttacker, out TankAnimator tank)
    {
        tank = (TankAnimator)head.Attacker.Animator;
        var hits = BuildHitInfos(byAttacker[head.Attacker]);
        Action onHit = hits.Count > 0 ? hits[0].OnHit : null;

        // One-way tank→tank: wait for the opponent tank to finish its own attack first —
        // but only if that opponent tank is actually attacking this round (has its own
        // AttackAssignment). A shocked/otherwise-skipped opponent tank never leaves its slot,
        // so it never fires OnLeapBackStarted — waiting on it would stall this tank forever.
        if (head.Target is Creature { Animator: TankAnimator opponentTank } opponentCreature
            && byAttacker.ContainsKey(opponentCreature))
        {
            tank.WaitThenAttack(head.Target, onHit);
            opponentTank.OnLeapBackStarted += tank.StartPendingAttack;
            return opponentTank.GetAttackDuration() + tank.GetAttackDuration() - tank.GetRangedDelay();
        }

        tank.AttackWithHits(hits);
        return tank.GetAttackDuration();
    }

    private List<HitInfo> BuildHitInfos(List<AttackAssignment> assignments)
    {
        var hits = new List<HitInfo>();
        var gemSpawned = new HashSet<(Creature attacker, Targetable target)>();

        foreach (var a in assignments)
        {
            var target = a.Target;
            var attacker = a.Attacker;
            float damage = a.Damage;
            bool targetIsDoomed = DoomedTargets.Contains(target);
            bool isHeroTarget = target is Hero;
            bool isShieldTarget = target is Shield;
            // Shields never grant XP gems even if they're the "doomed" target this round.
            bool shouldSpawnGem = !isShieldTarget && (isHeroTarget || (targetIsDoomed && gemSpawned.Add((attacker, target))));

            hits.Add(new HitInfo
            {
                Target = target,
                OnHit = () =>
                {
                    target.Health.TakeDamage(damage);
                    if (shouldSpawnGem)
                    {
                        ExperienceManager.Instance.SpawnGem(target.transform.position, attacker);
                    }
                }
            });
        }
        return hits;
    }

    // ============================================================
    //  Firepower estimation — consumed by FightSO's Comeback Settings (docs/SlotMachine.md) and
    //  BalanceTool's firepower HUD. Not debug-only: OpponentFirepowerAdvantage is a real gameplay
    //  input to the slot machine's rigging.
    // ============================================================

    /// <summary>
    /// Pre-battle estimate of one side's total damage output — mirrors ResolveTeam's per-attacker
    /// formula above (damage x AttackDamageMultiplier, numberOfAttacks hits, shocked creatures
    /// contribute nothing) without any target/simulated-HP bookkeeping, since nothing has been
    /// targeted yet.
    ///
    /// Deliberately does NOT apply <see cref="CreatureSO.specialDamageModifiers"/> (e.g.
    /// ShieldBreaker): those are target-dependent and this estimate has no targets, so a
    /// shield-breaking roster reads its plain firepower here and hits harder than this says once a
    /// Shield is actually up. See docs/Battle.md.
    /// </summary>
    public static float EstimateFirepower(bool isPlayerSide)
    {
        var creatures = isPlayerSide ? G.PlayerCreaturesManager.GetAllCreatures() : G.EnemyCreaturesManager.GetAllCreatures();
        float total = 0f;
        foreach (var creature in creatures)
        {
            if (creature.StatusesManager.IsShocked) continue;

            var stats = creature.Data.Stats(creature.Experience.Level);
            // No ApplySpecialModifiers here — see the summary above.
            float dmgPerHit = stats.damage * creature.StatusesManager.AttackDamageMultiplier;
            if (dmgPerHit <= 0f) continue;

            total += dmgPerHit * stats.numberOfAttacks;
        }
        return total;
    }

    /// <summary>
    /// Firepower minus the barrier standing in this side's way — what it can actually land on the
    /// opposing hero/creatures. Floored at 0: "can't get through the barrier at all" is as bad as it
    /// gets, so an oversized Shield can never push the opposing side's advantage past its own
    /// firepower.
    /// </summary>
    public static float EstimateEffectiveFirepower(bool isPlayerSide)
        => Mathf.Max(0f, EstimateFirepower(isPlayerSide) - OpposingShieldHp(isPlayerSide));

    /// <summary>
    /// How far the OPPONENT of <paramref name="isPlayerSide"/> leads on effective firepower —
    /// positive means this side is the one falling behind. The firepower-advantage input to
    /// FightSO.ComebackSetting.opponentAdvantage; see docs/SlotMachine.md.
    /// </summary>
    public static float OpponentFirepowerAdvantage(bool isPlayerSide)
        => EstimateEffectiveFirepower(!isPlayerSide) - EstimateEffectiveFirepower(isPlayerSide);

    /// <summary>
    /// HP of the Shield facing this side, or 0 when the opposing hero has none up. HeroView.Shield
    /// is genuinely optional (an empty shield slot reads null), which is why this one is guarded.
    /// </summary>
    private static float OpposingShieldHp(bool isPlayerSide)
    {
        var shield = (isPlayerSide ? G.EnemyView : G.PlayerView).Shield;
        return shield == null ? 0f : shield.Health.CurrentHealth;
    }
}
