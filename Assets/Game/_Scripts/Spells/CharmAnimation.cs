using System;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Spells
{
    /// <summary>
    /// Charm's animation: heart / heart_broken beat, then on success applies the data change and
    /// hands the actual movement to <see cref="_Scripts.Creatures.CreatureAnimator.RunToCurrentSlot"/> —
    /// spell animations only sequence the effect, creature locomotion lives on the creature's
    /// own animator.
    /// </summary>
    public class CharmAnimation : SpellActionAnimation
    {
        [Header("Timing")]
        [Tooltip("Wait after PlayCharmSuccess before the run starts. Must cover the whole heart sequence: heart burst, then the heart_success systems (0.75s start delay + 2s duration) = 2.75s.")]
        [SerializeField] private float heartPause = 2.75f;
        [SerializeField] private float failDuration = 1.2f;
        [SerializeField] private float arriveSettleDelay = 0.3f;

        public override void Execute(SpellSO source, Hero caster, SpellResolver resolver, Action onComplete)
        {
            if (resolver.Shots.Count == 0)
            {
                onComplete?.Invoke();
                return;
            }

            var shot = (CharmShot)resolver.Shots[0];
            var creature = shot.Charmed;

            if (!shot.Success)
            {
                creature.StatusesManager.PlayCharmFail();
                Utils.DoAfterDelay.Execute(onComplete, failDuration);
                return;
            }

            creature.StatusesManager.PlayCharmSuccess();
            Utils.DoAfterDelay.Execute(() => StartRun(shot, creature, onComplete), heartPause);
        }

        private void StartRun(CharmShot shot, Creature creature, Action onComplete)
        {
            var startPos = creature.transform.position;

            // Data change happens now: reparents into the destination slot and toggles the
            // charmed status (see CharmShot.Apply). The run below is pure view catch-up.
            shot.Apply();

            void HandleArrived()
            {
                creature.Animator.OnRunToSlotArrived -= HandleArrived;
                Utils.DoAfterDelay.Execute(onComplete, arriveSettleDelay);
            }

            creature.Animator.OnRunToSlotArrived += HandleArrived;
            creature.Animator.RunToCurrentSlot(startPos);
        }
    }
}
