using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Creatures;
using Game._Scripts.Creatures;
using UnityEngine;

public partial class AttacksResolver
{
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

    /// <summary>Mage (0) → Archer (1) → Tank (2). Matched with <c>is</c>, so subclasses such as
    /// <see cref="CounterTankSO"/> keep their class's priority.</summary>
    private int GetPriority(Creature creature) => creature.Data switch
    {
        MageSO => 0,
        ArcherSO => 1,
        TankSO => 2,
        _ => 99
    };

    private List<Creature> SortByPriority(List<Creature> creatures)
    {
        return creatures.OrderBy(GetPriority).ToList();
    }

    /// <summary>Target order only (not attack order): a <see cref="CreatureSO.preferredTarget"/>
    /// creature ranks ahead of every class, then the usual class priority breaks ties.</summary>
    private int GetTargetPriority(Creature creature) =>
        creature.Data.preferredTarget ? GetPriority(creature) - 100 : GetPriority(creature);

    /// <summary>
    /// Returns the next attackable target. Priority: enemy shield (if alive) → preferred-target
    /// creatures → highest priority alive creature → enemy hero.
    /// </summary>
    private Targetable GetHighestPriorityAliveTarget(List<Creature> enemies, Hero hero, Shield shield,
        Dictionary<Targetable, float> simHP)
    {
        if (shield != null && simHP.TryGetValue(shield, out float shieldHp) && shieldHp > 0)
            return shield;

        var creature = enemies
            .Where(e => simHP.TryGetValue(e, out float hp) && hp > 0)
            .OrderBy(GetTargetPriority)
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
        Dictionary<Targetable, float> enemySimHP,
        bool isPlayerSide)
    {
        var assignments = new List<AttackAssignment>();
        var sorted = SortByPriority(attackers);

        foreach (var attacker in sorted)
        {
            // Shocked creatures skip their turn entirely.
            if (attacker.StatusesManager.IsShocked) continue;

            float buffedDamage = BuffedDamage(attacker, isPlayerSide);
            if (buffedDamage <= 0f) continue; // BattleCry Energy Drain reduced damage to 0 — skip turn
            int hitCount = attacker.Data.Stats(attacker.Experience.Level).numberOfAttacks;

            for (int i = 0; i < hitCount; i++)
            {
                var target = GetHighestPriorityAliveTarget(enemies, enemyHero, enemyShield, enemySimHP);
                if (target == null) break;

                // Per hit, not per attacker: a multi-hit archer can hit a Shield on hit 1 and a
                // Creature on hit 2, and target-dependent modifiers must see each target.
                float modifiedDamage = ApplySpecialModifiers(attacker, target, buffedDamage);

                // Critical strike is always the last step, on top of BattleCry and every modifier.
                bool isCritical = RollsCritical(attacker.Data);
                float dmgPerHit = isCritical ? modifiedDamage * attacker.Data.CritDamageMultiplier : modifiedDamage;

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
                    FatalBlow = fatal,
                    IsCritical = isCritical
                });
            }
        }

        return assignments;
    }

    /// <summary>Per-hit damage before target-dependent steps: creature class reward boost (player side
    /// only, docs/Rewards.md), then BattleCry. Also the base of a healing archer's heal.</summary>
    private static float BuffedDamage(Creature attacker, bool isPlayerSide)
    {
        var stats = attacker.Data.Stats(attacker.Experience.Level);
        float baseDamage = RewardBonuses.ApplyCreatureBonus(attacker.Data, stats.damage, isPlayerSide);
        return baseDamage * attacker.StatusesManager.AttackDamageMultiplier;
    }

    // ============================================================
    //  Counterattacks (docs/Battle.md "Counterattacks") — a post-pass over the regular hits, so the
    //  two ResolveTeam passes stay untouched and counters can see who dies anyway.
    // ============================================================

    /// <summary>
    /// For every regular hit landing on a <see cref="CounterTankSO"/> creature, plans a counter back at
    /// the attacker — or at the Shield guarding the attacker's side, while that shield is still up
    /// after all regular attacks. Skipped when that target is already dead after all regular attacks
    /// (it would die anyway, so the counter isn't needed). Counters are appended to AllAttacks as
    /// ordinary assignments and linked from their trigger via HasCounter/CounterIndex.
    /// </summary>
    private void ResolveCounters(Dictionary<Targetable, float> playerSimHP, Dictionary<Targetable, float> enemySimHP,
        Shield playerShield, Shield enemyShield)
    {
        int regularCount = AllAttacks.Count;
        for (int i = 0; i < regularCount; i++)
        {
            bool attackerIsPlayer = i < PlayerAttacks.Count;
            var attackerSimHP = attackerIsPlayer ? playerSimHP : enemySimHP;
            var attackerShield = attackerIsPlayer ? playerShield : enemyShield;
            if (!TryPlanCounter(AllAttacks[i], attackerSimHP, attackerShield, out var counter)) continue;

            var trigger = AllAttacks[i];
            trigger.HasCounter = true;
            trigger.CounterIndex = CounterAttacks.Count;
            AllAttacks[i] = trigger;

            CounterAttacks.Add(counter);
            AllAttacks.Add(counter);
        }
    }

    private bool TryPlanCounter(AttackAssignment trigger, Dictionary<Targetable, float> attackerSimHP,
        Shield attackerShield, out AttackAssignment counter)
    {
        counter = default;
        if (trigger.Target is not Creature { Data: CounterTankSO data } defender) return false;
        if (defender.StatusesManager.IsShocked) return false;

        var target = CounterTarget(trigger.Attacker, attackerShield, attackerSimHP);
        if (attackerSimHP[target] <= 0f) return false; // dies anyway — no counter needed

        // A flat share of the incoming hit — no boost, BattleCry, special modifiers or crit of its own.
        float damage = trigger.Damage * data.CounterFraction(defender.Experience.Level);
        float hpAfter = Mathf.Max(0f, attackerSimHP[target] - damage);
        attackerSimHP[target] = hpAfter;

        counter = new AttackAssignment
        {
            Attacker = defender,
            Target = target,
            Damage = damage,
            ResultHP = hpAfter,
            FatalBlow = hpAfter <= 0f,
            IsCounter = true
        };
        return true;
    }

    /// <summary>The Shield guarding the attacker's side while it's still up after all regular
    /// attacks, else the attacker itself.</summary>
    private static Targetable CounterTarget(Creature attacker, Shield attackerShield,
        Dictionary<Targetable, float> attackerSimHP)
    {
        if (attackerShield != null && attackerSimHP.TryGetValue(attackerShield, out float shieldHp) && shieldHp > 0f)
            return attackerShield;
        return attacker;
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

    /// <summary>
    /// Rolls this hit's critical strike against <see cref="CreatureSO.critChancePercent"/>. Rolled
    /// inside Resolve(), so the animated and instant paths share the same outcome. A creature with no
    /// crit chance never touches the RNG, so adding the mechanic doesn't shift anyone else's rolls.
    /// </summary>
    private static bool RollsCritical(CreatureSO data)
    {
        if (data.critChancePercent <= 0) return false;
        return AIController.RollForProbability(data.critChancePercent);
    }

    private float ExecuteAnimations(List<AttackAssignment> allAttacks)
    {
        // Group assignments by attacker to handle multi-hit archers
        var byAttacker = new Dictionary<Creature, List<AttackAssignment>>();
        foreach (var a in allAttacks)
        {
            // Counters never animate as a turn of their own — they play from their trigger's OnHit.
            if (a.IsCounter) continue;
            if (!byAttacker.ContainsKey(a.Attacker))
                byAttacker[a.Attacker] = new List<AttackAssignment>();
            byAttacker[a.Attacker].Add(a);
        }

        // Pre-phase: healers fire their support volleys first; every regular attack below (healers
        // included) starts through the pre-phase gate.
        float prePhase = PlayPrePhase(byAttacker);

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

            var tankA = mutualTankA;
            var tankB = mutualTankB;
            _prePhaseGate.RunWhenOpen(() =>
            {
                tankA.AttackWithHits(hitsA);

                float arriveTime = tankA.GetRangedDelay() + tankA.GetRunDuration();
                Utils.DoAfterDelay.Execute(() =>
                {
                    tankB.SetPendingOnHit(hitsB.Count > 0 ? hitsB[0].OnHit : null);
                    tankB.PlayAttackInPlace(null);
                }, arriveTime);
            });

            float dur = mutualTankA.GetAttackDuration();
            if (dur > maxDuration) maxDuration = dur;

            handledTanks.Add(creatureA);
            handledTanks.Add(creatureB);
        }

        // Remaining tanks, grouped by target and chained: tanks sharing one target take turns
        // (each launches as the previous one leaps back) so they never pile up on the same
        // melee position at once. Chains on a Shield play first: a tank headed for the hero behind
        // a shield that another tank is breaking this battle waits for that shield chain to strike,
        // instead of visibly running through the still-standing shield.
        var tankChains = uniqueAttacks
            .Where(a => a.Attacker.Animator is TankAnimator && !handledTanks.Contains(a.Attacker))
            .GroupBy(a => a.Target)
            .OrderBy(chain => chain.Key is Shield ? 0 : 1);

        var finishedChains = new Dictionary<Targetable, TankChainEnd>();
        foreach (var chain in tankChains)
        {
            var shieldBreaker = FindShieldBreakerChain(chain.Key, finishedChains);
            var end = ExecuteTankChain(chain.ToList(), byAttacker, shieldBreaker);
            finishedChains[chain.Key] = end;
            if (end.Duration > maxDuration) maxDuration = end.Duration;
            foreach (var a in chain) handledTanks.Add(a.Attacker);
        }

        // Handle all other attacks (ranged attackers stay in their slots — simultaneous is fine)
        foreach (var a in uniqueAttacks)
        {
            if (handledTanks.Contains(a.Attacker)) continue;

            var hits = BuildHitInfos(byAttacker[a.Attacker]);
            var animator = a.Attacker.Animator;
            _prePhaseGate.RunWhenOpen(() => animator.AttackWithHits(hits));

            float dur = animator.GetAttackDuration();
            if (dur > maxDuration) maxDuration = dur;
        }

        return prePhase + maxDuration + LongestCounterDuration();
    }

    /// <summary>Extra time the battle stays open so a counter triggered by the last hit can land.</summary>
    private float LongestCounterDuration()
    {
        float longest = 0f;
        foreach (var counter in CounterAttacks)
            longest = Mathf.Max(longest, ((CounterTankAnimator)counter.Attacker.Animator).GetCounterDuration());
        return longest;
    }

    /// <summary>
    /// Plays a group of tank attacks that all share one target as a queue. The head tank follows
    /// the normal rules (waits for a tank opponent's leap-back, otherwise attacks right away);
    /// each subsequent tank waits for the previous chain member's leap-back before starting its
    /// own run. When <paramref name="shieldBreaker"/> is set, the head instead waits for that
    /// shield chain's last tank to leap back. Returns the chain's last tank and total choreography
    /// duration.
    /// </summary>
    private TankChainEnd ExecuteTankChain(List<AttackAssignment> chain,
        Dictionary<Creature, List<AttackAssignment>> byAttacker, TankChainEnd? shieldBreaker)
    {
        float duration = StartChainHead(chain[0], byAttacker, shieldBreaker, out var previousTank);

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

        return new TankChainEnd(previousTank, duration);
    }

    private float StartChainHead(AttackAssignment head,
        Dictionary<Creature, List<AttackAssignment>> byAttacker, TankChainEnd? shieldBreaker,
        out TankAnimator tank)
    {
        tank = (TankAnimator)head.Attacker.Animator;
        var hits = BuildHitInfos(byAttacker[head.Attacker]);
        Action onHit = hits.Count > 0 ? hits[0].OnHit : null;

        // Hero behind a shield another tank is breaking: strike only once the breaker leaps back.
        if (shieldBreaker is { } breaker)
        {
            tank.WaitThenAttack(head.Target, onHit);
            breaker.LastTank.OnLeapBackStarted += tank.StartPendingAttack;
            return breaker.Duration + tank.GetAttackDuration() - tank.GetRangedDelay();
        }

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

        var headTank = tank;
        _prePhaseGate.RunWhenOpen(() => headTank.AttackWithHits(hits));
        return tank.GetAttackDuration();
    }

    /// <summary>The last tank of an already-started chain and when that chain ends.</summary>
    private readonly struct TankChainEnd
    {
        public readonly TankAnimator LastTank;
        public readonly float Duration;

        public TankChainEnd(TankAnimator lastTank, float duration)
        {
            LastTank = lastTank;
            Duration = duration;
        }
    }

    /// <summary>
    /// The tank chain breaking the shield in front of <paramref name="target"/> this battle, if
    /// <paramref name="target"/> is a hero and one exists. A tank only ever gets assigned a hero
    /// while its shield is up if that shield is doomed earlier in the same resolve, so any tank
    /// chain on the guarding shield is by construction the one that breaks it.
    /// </summary>
    private TankChainEnd? FindShieldBreakerChain(Targetable target,
        Dictionary<Targetable, TankChainEnd> finishedChains)
    {
        if (!_guardingShields.TryGetValue(target, out var shield)) return null;
        if (!finishedChains.TryGetValue(shield, out var breaker)) return null;
        return breaker;
    }

    private List<HitInfo> BuildHitInfos(List<AttackAssignment> assignments)
    {
        var hits = new List<HitInfo>();
        var gemSpawned = new HashSet<(Creature attacker, Targetable target)>();

        foreach (var a in assignments)
            hits.Add(new HitInfo { Target = a.Target, OnHit = BuildOnHit(a, gemSpawned) });
        return hits;
    }

    /// <summary>Gem de-dup for counters, shared across every trigger (a counter-tank killing one
    /// archer through two counters still spawns one gem).</summary>
    private readonly HashSet<(Creature attacker, Targetable target)> _counterGemSpawned = new();

    /// <summary>
    /// The hit callback that lands one planned assignment: damage, hit feedbacks, XP gem — and, when
    /// the hit landed on a counter-tank, that tank's counter (death postponed around the damage so
    /// even a killing blow is answered).
    /// </summary>
    private Action BuildOnHit(AttackAssignment a, HashSet<(Creature attacker, Targetable target)> gemSpawned)
    {
        var target = a.Target;
        var attacker = a.Attacker;
        float damage = a.Damage;
        bool isCritical = a.IsCritical;
        bool shouldSpawnGem = ShouldSpawnGem(a, gemSpawned);
        Action counter = a.HasCounter ? BuildCounter(CounterAttacks[a.CounterIndex]) : null;
        var counterTank = a.HasCounter ? (CounterTankAnimator)((Creature)target).Animator : null;

        return () =>
        {
            counterTank?.BeginCounter();
            target.Health.TakeDamage(damage);
            target.HitFeedback.PlayHitFeedbacks(isCritical);
            if (shouldSpawnGem)
                ExperienceManager.Instance.SpawnGem(target.transform.position, attacker);
            counter?.Invoke();
        };
    }

    private Action BuildCounter(AttackAssignment counter)
    {
        var counterTank = (CounterTankAnimator)counter.Attacker.Animator;
        var onHit = BuildOnHit(counter, _counterGemSpawned);
        return () => counterTank.PlayCounter(counter.Target, onHit);
    }

    private bool ShouldSpawnGem(AttackAssignment a, HashSet<(Creature attacker, Targetable target)> gemSpawned)
    {
        // Shields never grant XP gems even if they're the "doomed" target this round.
        if (a.Target is Shield) return false;
        if (a.Target is Hero) return true;
        return DoomedTargets.Contains(a.Target) && gemSpawned.Add((a.Attacker, a.Target));
    }

    // ============================================================
    //  Firepower estimation — consumed by FightSO's Comeback Settings (docs/SlotMachine.md), the AI's
    //  per-turn reroll budget (RerollBudgetDecision, docs/AI.md) and BalanceTool's firepower HUD. Not
    //  debug-only: OpponentFirepowerAdvantage is a real gameplay input to both the slot machine's
    //  rigging and the AI's reroll spending.
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
    ///
    /// Critical strikes ARE included, at their expected value (<see cref="CreatureSO.ExpectedCritMultiplier"/>):
    /// unlike special modifiers they don't depend on the target, so the average is known up front
    /// without rolling any dice.
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
            // EstimatedDamage: a non-attacker (counter-tank, future healer) counts its nominalDamage.
            float dmgPerHit = RewardBonuses.ApplyCreatureBonus(creature.Data, creature.Data.EstimatedDamage(stats), isPlayerSide)
                              * creature.StatusesManager.AttackDamageMultiplier
                              * creature.Data.ExpectedCritMultiplier;
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
