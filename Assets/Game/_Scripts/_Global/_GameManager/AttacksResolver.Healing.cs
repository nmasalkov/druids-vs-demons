using System;
using System.Collections.Generic;
using System.Linq;
using _Scripts.Creatures;
using Game._Scripts.Creatures;
using Game._Scripts.Global;
using UnityEngine;

/// <summary>One planned heal from a <see cref="HealingArcherSO"/> shot (docs/Battle.md "Healing shots").</summary>
public struct HealAssignment
{
    public Creature Healer;
    public Unit Target;

    /// <summary>Effective heal: already capped at the target's missing HP at plan time, so landing it
    /// after other hits never overflows max HP and both resolve paths end identical.</summary>
    public float Amount;

    /// <summary>Which shot of the healer's volley carries this heal.</summary>
    public int ShotIndex;
}

public partial class AttacksResolver
{
    /// <summary>Heals planned this battle. Applied before every attack — in the instant path and, via
    /// the pre-phase, on screen.</summary>
    public List<HealAssignment> Heals { get; } = new();

    /// <summary>Holds back the regular battle until the pre-phase (support volleys) is over; open from
    /// the start when nothing heals.</summary>
    private PrePhaseGate _prePhaseGate = new(0);

    // ============================================================
    //  Planning — runs before both ResolveTeam passes, so damage sees the healed HP.
    // ============================================================

    private void ResolveHeals(List<Creature> allies, Hero hero, Dictionary<Targetable, float> simHP, bool isPlayerSide)
    {
        foreach (var healer in allies)
        {
            if (healer.Data is not HealingArcherSO data) continue;
            if (healer.StatusesManager.IsShocked) continue;

            float healPerShot = HealPerShot(healer, data, isPlayerSide);
            if (healPerShot <= 0f) continue; // BattleCry Energy Drain cancels heals too

            int shots = data.Stats(healer.Experience.Level).numberOfAttacks;
            for (int shot = 0; shot < shots; shot++)
                PlanHeal(healer, shot, healPerShot, allies, hero, simHP);
        }
    }

    private float HealPerShot(Creature healer, HealingArcherSO data, bool isPlayerSide)
        => BuffedDamage(healer, isPlayerSide) * data.HealFraction(healer.Experience.Level);

    private void PlanHeal(Creature healer, int shot, float healPerShot, List<Creature> allies, Hero hero,
        Dictionary<Targetable, float> simHP)
    {
        if (!TryPickWoundedAlly(healer, allies, hero, simHP, out var target)) return; // nobody hurt — shot heals nothing

        float amount = Mathf.Min(healPerShot, target.Health.MaxHealth - simHP[target]);
        simHP[target] += amount;
        Heals.Add(new HealAssignment { Healer = healer, Target = target, Amount = amount, ShotIndex = shot });
    }

    /// <summary>A uniformly random living ally below max HP — another creature or the hero, never the
    /// healer itself and never the Shield.</summary>
    private static bool TryPickWoundedAlly(Creature healer, List<Creature> allies, Hero hero,
        Dictionary<Targetable, float> simHP, out Unit target)
    {
        var wounded = allies.Where(c => c != healer && IsWounded(c, simHP)).Cast<Unit>().ToList();
        if (hero != null && IsWounded(hero, simHP)) wounded.Add(hero);

        target = wounded.Count > 0 ? wounded[UnityEngine.Random.Range(0, wounded.Count)] : null;
        return target != null;
    }

    private static bool IsWounded(Unit unit, Dictionary<Targetable, float> simHP)
        => simHP.TryGetValue(unit, out float hp) && hp > 0f && hp < unit.Health.MaxHealth;

    // ============================================================
    //  Pre-phase — support volleys (heals today) play before the regular battle. Every regular
    //  attack, healers' own damage volleys included, starts once the pre-phase gate opens.
    // ============================================================

    /// <summary>
    /// Starts every healer's support volley: split shots only (its heals), no main missile. Healers
    /// stay in <paramref name="byAttacker"/>, so they attack normally in the regular phase. Returns
    /// the pre-phase's length — also the gate's fallback timeout. 0 when nothing heals this battle.
    /// </summary>
    private float PlayPrePhase(Dictionary<Creature, List<AttackAssignment>> byAttacker)
    {
        var volleys = Heals.GroupBy(h => h.Healer).ToList();
        // Waits for every heal to land AND every volley's animation to finish, so a healer's
        // regular attack never starts on top of its own still-playing support volley.
        _prePhaseGate = new PrePhaseGate(Heals.Count + volleys.Count);

        float prePhase = 0f;
        foreach (var volley in volleys)
            prePhase = Mathf.Max(prePhase, PlayHealVolley(volley.Key, volley.ToList()));

        if (prePhase > 0f) OpenGateAfter(prePhase);
        return prePhase;
    }

    private float PlayHealVolley(Creature healer, List<HealAssignment> heals)
    {
        var splitHits = BuildHealHitsPerShot(heals, heals.Max(h => h.ShotIndex) + 1);
        var animator = (SplitShotArcherAnimator)healer.Animator;
        var gate = _prePhaseGate;
        animator.AttackWithSplitHits(new List<HitInfo>(), splitHits, gate.Land);
        return animator.GetSplitVolleyDuration(splitHits.Length);
    }

    private List<HitInfo>[] BuildHealHitsPerShot(List<HealAssignment> heals, int shotCount)
    {
        var perShot = new List<HitInfo>[shotCount];
        for (int i = 0; i < shotCount; i++) perShot[i] = new List<HitInfo>();
        foreach (var heal in heals)
            perShot[heal.ShotIndex].Add(new HitInfo { Target = heal.Target, OnHit = BuildHealOnHit(heal) });
        return perShot;
    }

    private Action BuildHealOnHit(HealAssignment heal)
    {
        var gate = _prePhaseGate;
        return () =>
        {
            heal.Target.Health.Heal(heal.Amount, clearsStatuses: false);
            gate.Land();
        };
    }

    /// <summary>Fallback so a support missile that never arrives can't stall the battle.</summary>
    private void OpenGateAfter(float delay)
    {
        var gate = _prePhaseGate;
        int generation = GameManager.Instance.Generation;
        Utils.DoAfterDelay.Execute(() =>
        {
            if (GameManager.IsStale(generation)) return;
            gate.Open();
        }, delay);
    }

    /// <summary>
    /// Counts pending pre-phase events (landed heals, finished volleys); queued actions run once the
    /// last one arrives (or <see cref="Open"/> is called by the fallback timeout). Created open when
    /// there's nothing to wait for.
    /// </summary>
    private sealed class PrePhaseGate
    {
        private int _pending;
        private bool _open;
        private readonly List<Action> _queued = new();

        public PrePhaseGate(int pending)
        {
            _pending = pending;
            _open = pending == 0;
        }

        public void RunWhenOpen(Action action)
        {
            if (_open) action?.Invoke();
            else _queued.Add(action);
        }

        public void Land()
        {
            _pending--;
            if (_pending <= 0) Open();
        }

        public void Open()
        {
            if (_open) return;
            _open = true;
            foreach (var action in _queued) action?.Invoke();
            _queued.Clear();
        }
    }
}
