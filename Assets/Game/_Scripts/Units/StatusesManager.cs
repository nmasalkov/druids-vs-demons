using System;
using Game._Scripts.Creatures;
using MoreMountains.Feedbacks;
using UnityEngine;

namespace Game._Scripts.Units
{
    /// <summary>
    /// Owns runtime status flags (e.g. <see cref="IsShocked"/>) for a <see cref="Unit"/>
    /// and toggles the matching feedback prefabs via events. Auto-added to every Unit via
    /// <c>[RequireComponent]</c> on the base class (rule #14: helper component owned by base).
    /// Statuses are cleared on heal and on level-up so designers don't need extra plumbing.
    /// </summary>
    public class StatusesManager : MonoBehaviour
    {
        [Header("Feedbacks")]
        [Tooltip("MMF_Player on the ShockedFeedback child. Played while IsShocked is true.")]
        [SerializeField] private MMF_Player shockedFeedback;
        [Tooltip("MMF_Player on the BattleCryEffect/BattleCryBuffFeedback child. Played while a BattleCry buff is active.")]
        [SerializeField] private MMF_Player battleCryBuffFeedback;
        [Tooltip("MMF_Player on the BattleCryEffect/BattleCryDebuffFeedback child. Played while a BattleCry debuff is active.")]
        [SerializeField] private MMF_Player battleCryDebuffFeedback;

        public bool IsShocked { get; private set; }

        /// <summary>
        /// 1 = no BattleCry effect. Set by <see cref="ApplyBattleCryBuff"/>/<see cref="ApplyBattleCryDebuff"/>,
        /// cleared only by <see cref="ClearBattleCry"/> (called explicitly from PostBattleState) — NOT by
        /// <see cref="ClearAllStatuses"/>, since the effect must last the whole battle regardless of
        /// heals/promotions.
        /// </summary>
        public float AttackDamageMultiplier { get; private set; } = 1f;

        /// <summary>Fired whenever <see cref="IsShocked"/> flips. Argument is the new value.</summary>
        public event Action<bool> OnShockedChanged;

        private Unit unit;
        private ParticleSystem battleCryBuffParticles;
        private ParticleSystem battleCryDebuffParticles;

        void Awake()
        {
            unit = GetComponent<Unit>();

            // Hero units (RequireComponent via the Unit base) don't get the BattleCryEffect
            // child wired — that status only ever targets creatures, so these are genuinely
            // optional here, unlike this class's other, mandatory serialized refs.
            if (battleCryBuffFeedback != null)
                battleCryBuffParticles = battleCryBuffFeedback.GetComponentInChildren<ParticleSystem>();
            if (battleCryDebuffFeedback != null)
                battleCryDebuffParticles = battleCryDebuffFeedback.GetComponentInChildren<ParticleSystem>();
        }

        void Start()
        {
            unit.Health.onHealed += ClearAllStatuses;
            unit.Health.onDeath += ClearAllStatuses;

            // Creatures additionally lose statuses when they level up.
            var experience = GetComponent<Experience>();
            if (experience != null)
                experience.OnPromoted += ClearAllStatuses;
        }

        public void ApplyShock() => SetShocked(true);
        public void ClearShock() => SetShocked(false);

        private void SetShocked(bool value)
        {
            if (IsShocked == value) return;
            IsShocked = value;

            if (value) shockedFeedback.PlayFeedbacks();
            else shockedFeedback.StopFeedbacks();

            OnShockedChanged?.Invoke(value);
        }

        public void ApplyBattleCryBuff(float multiplier)
        {
            AttackDamageMultiplier = multiplier;
            StopBattleCryDebuffVisual();
            battleCryBuffFeedback.PlayFeedbacks();
        }

        public void ApplyBattleCryDebuff(float multiplier)
        {
            AttackDamageMultiplier = multiplier;
            StopBattleCryBuffVisual();
            battleCryDebuffFeedback.PlayFeedbacks();
        }

        public void ClearBattleCry()
        {
            if (AttackDamageMultiplier == 1f) return;
            AttackDamageMultiplier = 1f;
            StopBattleCryBuffVisual();
            StopBattleCryDebuffVisual();
        }

        /// <summary>
        /// MMF_Particles.Stop() only halts emission (StopEmitting) — already-alive particles
        /// keep rendering out their lifetime, leaving visible residue. Force an immediate clear
        /// on top of the MMF stop so switching/ending the effect leaves nothing on screen.
        /// </summary>
        private void StopBattleCryBuffVisual()
        {
            battleCryBuffFeedback.StopFeedbacks();
            battleCryBuffParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void StopBattleCryDebuffVisual()
        {
            battleCryDebuffFeedback.StopFeedbacks();
            battleCryDebuffParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ClearAllStatuses()
        {
            ClearShock();
        }
    }
}


