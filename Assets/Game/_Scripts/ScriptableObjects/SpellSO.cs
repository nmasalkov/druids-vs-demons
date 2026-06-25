using Game._Scripts.Spells;
using UnityEngine;

[CreateAssetMenu(fileName = "NewSpell", menuName = "Game/Actions/Spell")]
public class SpellSO : ActionSO
{
    [Header("View")]
    [Tooltip("Prefab containing the SpellActionAnimation that plays this spell's visuals.")]
    [SerializeField] private SpellActionAnimation animationPrefab;

    public SpellActionAnimation AnimationPrefab => animationPrefab;
    public override ActionAnimation AnimationPrefabBase => animationPrefab;

    /// <summary>
    /// Create the resolver that computes WHAT happens for this spell. Default returns a
    /// no-op resolver (suitable for placeholder SpellSO assets). Concrete subclasses
    /// (e.g. <see cref="ShieldSO"/>) override to return their gameplay resolver.
    /// </summary>
    public virtual SpellResolver CreateResolver() => new NoOpSpellResolver();

    public override ActionResolver CreateAndResolve(ActionContext ctx, int level)
    {
        var resolver = CreateResolver();
        resolver.Resolve(this, ctx.Caster, ctx.CasterView, level);
        return resolver;
    }
}

