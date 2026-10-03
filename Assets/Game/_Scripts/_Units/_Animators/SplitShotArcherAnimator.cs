using System;
using System.Collections.Generic;
using Game._Scripts.Creatures;
using UnityEngine;

namespace _Scripts.Creatures
{
    /// <summary>
    /// An archer whose shots can split: each shot fires its usual missile plus any number of extra
    /// missiles at other targets, all at the same moment. Knows nothing about what the extra hits do —
    /// a healing archer passes heal hits (docs/Battle.md "Healing shots"), a future split-damage unit
    /// would pass damage hits. Data mutation stays in each hit's OnHit (rule 7).
    /// </summary>
    public class SplitShotArcherAnimator : ArcherAnimator
    {
        [Header("Split Shots")]
        [Tooltip("Fires the extra missiles of each shot. May be the same MissileAnimator as the main shot " +
                 "(HealingShroom uses Spore for both) or a second one with its own projectile.")]
        [SerializeField] private MissileAnimator splitMissileAnimator;

        [Tooltip("Seconds added to the volley's attack duration so the last split missile can land. Only " +
                 "affects how long AttacksResolver waits (the heal phase / battle end), not any outcome.")]
        [SerializeField] private float flightAllowance = 0.8f;

        private List<HitInfo>[] splitHitsPerShot;
        private Action onVolleyFinished;

        /// <summary>
        /// Plays a volley where shot i fires <paramref name="mainHits"/>[i] plus every hit in
        /// <paramref name="splitHits"/>[i]. The shorter side is padded with target-less hits, so a shot
        /// can be split-only (no main target left — e.g. the healing pre-phase passes no main hits at
        /// all) or main-only (no split hit). <paramref name="onFinished"/> runs once the volley's last
        /// shot animation ends.
        /// </summary>
        public void AttackWithSplitHits(List<HitInfo> mainHits, List<HitInfo>[] splitHits, Action onFinished = null)
        {
            splitHitsPerShot = splitHits;
            onVolleyFinished = onFinished;
            base.AttackWithHits(PadToShotCount(mainHits, splitHits.Length));
        }

        /// <summary>A plain volley (no split data) behaves like a normal archer.</summary>
        public override void AttackWithHits(List<HitInfo> hits)
        {
            splitHitsPerShot = null;
            onVolleyFinished = null;
            base.AttackWithHits(hits);
        }

        protected override void OnVolleyFinished()
        {
            var finished = onVolleyFinished;
            onVolleyFinished = null;
            finished?.Invoke();
        }

        private static List<HitInfo> PadToShotCount(List<HitInfo> hits, int shotCount)
        {
            var padded = new List<HitInfo>(hits);
            while (padded.Count < shotCount) padded.Add(new HitInfo());
            return padded;
        }

        protected override Action BuildExtraShot(int shotIndex)
        {
            if (splitHitsPerShot == null || shotIndex >= splitHitsPerShot.Length) return null;

            var shots = new List<Action>();
            foreach (var hit in splitHitsPerShot[shotIndex])
            {
                var shot = BuildShot(splitMissileAnimator, hit);
                if (shot != null) shots.Add(shot);
            }
            return () => shots.ForEach(s => s());
        }

        /// <summary>How long a <paramref name="shotCount"/>-shot volley takes until its last split
        /// missile has landed.</summary>
        public float GetSplitVolleyDuration(int shotCount) => GetAttackDuration(shotCount) + flightAllowance;
    }
}
