using System;
using Game._Scripts.Creatures;
using UnityEngine;

namespace Game._Scripts.Nukes
{
    /// <summary>
    /// Base class for all nuke action animation prefabs (view layer only). Receives a
    /// <see cref="NukeResolver"/> with pre-computed shots and just plays the visuals — no
    /// damage calculation here. The instant-resolve path skips animations entirely and goes
    /// through <see cref="NukeResolver.ApplyInstant"/>.
    /// </summary>
    public abstract class NukeActionAnimation : MonoBehaviour
    {
        /// <summary>
        /// Play the nuke visuals using the planned shots in <paramref name="resolver"/>.
        /// The animation decides WHEN each shot's <see cref="NukeShot.Apply"/> is invoked
        /// (e.g. on projectile impact). Invoke <paramref name="onComplete"/> when fully done.
        /// Caller is responsible for destroying this instance after onComplete fires.
        /// </summary>
        public abstract void Execute(NukeSO source, Hero caster, NukeResolver resolver, Action onComplete);
    }
}
