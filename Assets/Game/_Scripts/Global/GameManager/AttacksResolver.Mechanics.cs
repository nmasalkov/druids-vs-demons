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
            float dmgPerHit = stats.damage;
            int hitCount = stats.numberOfAttacks;

            for (int i = 0; i < hitCount; i++)
            {
                var target = GetHighestPriorityAliveTarget(enemies, enemyHero, enemyShield, enemySimHP);
                if (target == null) break;

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

        // Handle one-way tank→tank
        foreach (var a in uniqueAttacks)
        {
            if (a.Attacker.Animator is not TankAnimator thisTank) continue;
            if (handledTanks.Contains(a.Attacker)) continue;
            if (a.Target is not Unit unitTarget) continue;
            if (unitTarget.Animator is not TankAnimator opponentTank) continue;

            var attacker = a.Attacker;
            var hitsThis = BuildHitInfos(byAttacker[attacker]);

            thisTank.WaitThenAttack(a.Target, hitsThis.Count > 0 ? hitsThis[0].OnHit : null);

            opponentTank.OnLeapBackStarted += () =>
            {
                thisTank.StartPendingAttack();
            };

            float opponentDur = opponentTank.GetAttackDuration();
            float waitingDur = thisTank.GetRunDuration() + thisTank.GetAttackDuration()
                               - thisTank.GetRangedDelay() + thisTank.GetRunDuration();
            float dur = opponentDur + waitingDur;
            if (dur > maxDuration) maxDuration = dur;

            handledTanks.Add(attacker);
        }

        // Handle all other attacks
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
}

