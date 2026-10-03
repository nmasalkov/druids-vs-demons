using System;
using Game._Scripts.Creatures;
using UnityEngine;

namespace _Scripts.Creatures
{
    /// <summary>
    /// View for a <see cref="CounterTankSO"/> creature: it never runs its own melee attack (its damage
    /// is 0, so AttacksResolver never gives it a regular turn) — instead, whenever a creature hit lands
    /// on it, AttacksResolver's hit callback calls <see cref="BeginCounter"/> before applying the damage
    /// and <see cref="PlayCounter"/> right after, and it answers with a missile. Death is postponed
    /// while a counter is winding up, so even the killing blow gets its counter launched first.
    /// Data mutation stays in the resolver's hit callbacks (rule 7). See docs/Battle.md "Counterattacks".
    /// </summary>
    public class CounterTankAnimator : TankAnimator
    {
        [Header("Counterattack")]
        [Tooltip("Fires the counter missile. Its projectile prefab/speed/spawn point are set on the MissileAnimator itself.")]
        [SerializeField] private MissileAnimator missileAnimator;

        [Tooltip("Seconds between a hit landing on this tank and its counter missile launching (the Attack animation's wind-up).")]
        [SerializeField] private float fireDelay = 0.3f;

        [Tooltip("Extra seconds the battle waits after the last counter launch so the missile can land before the battle phase ends. Only affects BattleState's end delay, not damage.")]
        [SerializeField] private float flightAllowance = 1f;

        private int pendingCounters;

        /// <summary>Called right before the triggering hit's damage lands, so a fatal hit is
        /// postponed until this tank's counter has launched.</summary>
        public void BeginCounter()
        {
            pendingCounters++;
            ownerCreature.Health.PostponeDeath = true;
        }

        /// <summary>Plays the Attack animation and launches a missile at <paramref name="target"/>
        /// after <see cref="fireDelay"/>; <paramref name="onHit"/> applies the counter's damage on impact.</summary>
        public void PlayCounter(Targetable target, Action onHit)
        {
            PlayAttack();
            Vector3 targetPos = target.HitFeedback.HitPlacePosition.position;
            Utils.DoAfterDelay.Execute(() => LaunchCounter(targetPos, onHit), fireDelay);
        }

        private void LaunchCounter(Vector3 targetPos, Action onHit)
        {
            missileAnimator.FireOnce(targetPos, onHit);
            pendingCounters--;
            if (pendingCounters > 0) return;
            ReleasePostponedDeath();
        }

        private void ReleasePostponedDeath()
        {
            if (ownerCreature.Health.IsDead())
            {
                ownerCreature.Health.ExecutePostponedDeath();
                return;
            }
            ownerCreature.Health.PostponeDeath = false;
        }

        /// <summary>How long after the triggering hit a counter keeps the battle busy.</summary>
        public float GetCounterDuration() => fireDelay + flightAllowance;
    }
}
