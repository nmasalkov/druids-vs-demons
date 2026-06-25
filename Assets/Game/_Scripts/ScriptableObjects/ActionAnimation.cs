using System;
using Game._Scripts.Creatures;
using UnityEngine;

/// <summary>
/// Shared base for all action animation prefabs (Nuke, Spell, …). View layer only:
/// receives a pre-resolved <see cref="ActionResolver"/> and plays the visuals. Concrete
/// typed bases (e.g. <c>NukeActionAnimation</c>, <c>SpellActionAnimation</c>) downcast
/// and forward to their own strongly-typed <c>Execute</c> overload.
/// </summary>
public abstract class ActionAnimation : MonoBehaviour
{
    public abstract void Execute(ActionSO source, Hero caster, ActionResolver resolver, Action onComplete);
}

