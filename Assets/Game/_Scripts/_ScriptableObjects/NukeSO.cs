using Game._Scripts.Nukes;
using UnityEngine;

[CreateAssetMenu(fileName = "NewNuke", menuName = "Game/Actions/Nuke")]
public class NukeSO : ActionSO
{
    [Header("Damage")]
    [Tooltip("Total damage per level. Index 0 = level 1, 1 = level 2, 2 = level 3.")]
    [SerializeField] private float[] damagePerLevel = { 14f, 30f, 60f };

    [Header("View")]
    [Tooltip("Prefab containing the NukeActionAnimation that plays this nuke's visuals.")]
    [SerializeField] private NukeActionAnimation animationPrefab;

    [Header("Targeting")]
    [Tooltip("If true, this nuke skips the enemy shield entirely and hits what's behind it even " +
             "while the shield stands (Starfall). If false, the enemy shield is the highest " +
             "priority target whenever one is up, so it soaks the nuke first. Note Shock is false " +
             "AND is blocked outright by a standing shield at every level — that stronger rule " +
             "lives in ShockResolver, not in this flag. See docs/ActionsAndSpells.md.")]
    [SerializeField] private bool ignoresShield;

    [Tooltip("Order this nuke resolves in when one roll lands several different nukes — lower " +
             "resolves first; equal values keep the order they were rolled in. Matters because a " +
             "standing enemy shield changes what a nuke can reach: it's the top priority target " +
             "for any nuke that doesn't ignore it (see NukeResolver.BuildPriorityTargets), and it " +
             "blocks Shock completely (see ShockResolver). Running damage first (FireMagic 10, " +
             "Starfall 20) and Shock last (30) gives the damage a chance to break the shield, " +
             "after which Shock can reach the creatures behind it. Leave gaps when authoring a new " +
             "nuke so it can be slotted between two existing ones. See docs/ActionsAndSpells.md.")]
    [SerializeField] private int resolutionOrder = 10;

    public NukeActionAnimation AnimationPrefab => animationPrefab;
    public override ActionAnimation AnimationPrefabBase => animationPrefab;
    public bool IgnoresShield => ignoresShield;
    public int ResolutionOrder => resolutionOrder;

    public float GetDamageForLevel(int level)
    {
        int idx = Mathf.Clamp(level - 1, 0, damagePerLevel.Length - 1);
        return damagePerLevel[idx];
    }

    /// <summary>
    /// Create the resolver that computes targets/damage for this nuke. Default returns a no-op
    /// resolver (suitable for placeholder NukeSO assets). Concrete subclasses (e.g. FireMagicSO)
    /// override to return their gameplay resolver.
    /// </summary>
    public virtual NukeResolver CreateResolver() => new NoOpNukeResolver();

    public override ActionResolver CreateAndResolve(ActionContext ctx, int level)
    {
        var resolver = CreateResolver();
        resolver.Resolve(this, ctx.Caster, ctx.EnemyCreatures, ctx.EnemyHero, ctx.EnemyShield, level);
        return resolver;
    }
}
