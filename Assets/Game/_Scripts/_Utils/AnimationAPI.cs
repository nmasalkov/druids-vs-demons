using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Generic string-keyed playback layer over a plain Unity <see cref="Animator"/>, so gameplay/view
    /// code never has to know a controller's parameter names or transition graph. Works on any
    /// controller wired by the convention below — use the "Wire controller" button in this
    /// component's Inspector (AnimationAPIControllerWiring) to generate that wiring from the
    /// controller's states in one click. See docs/AnimationAPI.md.
    ///
    /// Convention, per animation (= state) <c>N</c>:
    /// <list type="bullet">
    /// <item>trigger parameter <c>N</c> — <see cref="TriggerAnimation"/>: play N right now, once, then
    /// fall back to the enabled animation.</item>
    /// <item>bool parameter <c>IsN</c> — <see cref="EnableAnimation"/>: N becomes the animation the
    /// Animator rests in (every other <c>Is*</c> bool is cleared).</item>
    /// </list>
    /// Animation names are discovered at runtime from the Animator's parameters, so there is nothing
    /// to regenerate when a controller changes — only re-run the wiring.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class AnimationAPI : MonoBehaviour
    {
        public const string BoolPrefix = "Is";

        private Animator _animator;

        private readonly Dictionary<string, int> _triggers = new();
        private readonly Dictionary<string, int> _bools = new();
        private readonly Dictionary<int, string> _namesByStateHash = new();
        private readonly Dictionary<string, float> _clipLengths = new();
        private readonly List<string> _animationNames = new();
        private int[] _boolHashes;

        /// <summary>Every animation name that has a trigger and/or an <c>Is*</c> bool.</summary>
        public IReadOnlyList<string> AnimationNames => _animationNames;

        /// <summary>The last name passed to <see cref="TriggerAnimation"/> (Animator triggers are
        /// consumed, so this is the only place that history lives). Inspector/debug only.</summary>
        public string LastTriggeredAnimation { get; private set; }

        /// <summary>The animation whose <c>Is*</c> bool is currently set, read live from the Animator;
        /// null if none is.</summary>
        public string EnabledAnimation
        {
            get
            {
                foreach (var pair in _bools)
                {
                    if (_animator.GetBool(pair.Value)) return pair.Key;
                }
                return null;
            }
        }

        /// <summary>Name of the state layer 0 is currently in, read live from the Animator; null if it
        /// isn't one of the discovered animations.</summary>
        public string CurrentStateName =>
            _namesByStateHash.GetValueOrDefault(_animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

        public Animator Animator => _animator;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            CacheParameters();
            CacheClipLengths();
        }

        /// <summary>Plays <paramref name="animationName"/> immediately, once (an Any State
        /// transition, interrupting whatever is playing), then returns to the enabled animation.</summary>
        public void TriggerAnimation(string animationName)
        {
            _animator.SetTrigger(RequireHash(_triggers, animationName, "trigger", animationName));
            LastTriggeredAnimation = animationName;
        }

        /// <summary>
        /// Makes <paramref name="animationName"/> the animation the Animator rests in: sets its
        /// <c>Is*</c> bool and clears every other one. No-op if it's already enabled, so a one-shot
        /// currently playing on top of it isn't cut off.
        /// </summary>
        /// <param name="playImmediately">True (default): also fires the animation's trigger so the switch
        /// happens now. False: whatever is playing finishes first — use it when a one-shot is
        /// triggered in the same call as the new resting animation (e.g. Die, then rest in Dead),
        /// because two triggers set on the same frame fight each other.</param>
        public void EnableAnimation(string animationName, bool playImmediately = true)
        {
            int boolHash = RequireHash(_bools, animationName, "bool", BoolPrefix + animationName);
            if (_animator.GetBool(boolHash)) return;

            foreach (int hash in _boolHashes)
                _animator.SetBool(hash, hash == boolHash);

            if (playImmediately && _triggers.ContainsKey(animationName))
                TriggerAnimation(animationName);
        }

        public bool HasAnimation(string animationName) =>
            _triggers.ContainsKey(animationName) || _bools.ContainsKey(animationName);

        /// <summary>Length in seconds of the clip named <paramref name="animationName"/> (clip name ==
        /// state name by convention), or 0 if the controller has no clip by that name.</summary>
        public float GetClipLength(string animationName) =>
            _clipLengths.GetValueOrDefault(animationName);

        private void CacheParameters()
        {
            var boolHashes = new List<int>();
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    AddName(parameter.name);
                    _triggers[parameter.name] = parameter.nameHash;
                    continue;
                }

                if (!IsAnimationBool(parameter)) continue;
                string animationName = parameter.name.Substring(BoolPrefix.Length);
                AddName(animationName);
                _bools[animationName] = parameter.nameHash;
                boolHashes.Add(parameter.nameHash);
            }
            _boolHashes = boolHashes.ToArray();
        }

        private static bool IsAnimationBool(AnimatorControllerParameter parameter) =>
            parameter.type == AnimatorControllerParameterType.Bool
            && parameter.name.Length > BoolPrefix.Length
            && parameter.name.StartsWith(BoolPrefix, StringComparison.Ordinal);

        private void AddName(string animationName)
        {
            if (_animationNames.Contains(animationName)) return;
            _animationNames.Add(animationName);
            _namesByStateHash[Animator.StringToHash(animationName)] = animationName;
        }

        private void CacheClipLengths()
        {
            foreach (var clip in _animator.runtimeAnimatorController.animationClips)
                _clipLengths[clip.name] = clip.length;
        }

        private int RequireHash(Dictionary<string, int> map, string animationName, string kind, string parameterName)
        {
            if (map.TryGetValue(animationName, out int hash)) return hash;
            throw new ArgumentException(
                $"AnimationAPI on '{name}': no {kind} parameter '{parameterName}' for animation " +
                $"'{animationName}' in controller '{_animator.runtimeAnimatorController.name}'. " +
                $"Available animations: {string.Join(", ", _animationNames)}. Re-run " +
                "\"Wire controller\" if the controller changed (docs/AnimationAPI.md).");
        }
    }
}
