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
    [Tooltip("If true, this nuke skips the enemy shield entirely (e.g. Starfall). Otherwise " +
             "the enemy shield is the highest priority target when present.")]
    [SerializeField] private bool ignoresShield;

    public NukeActionAnimation AnimationPrefab => animationPrefab;
    public override ActionAnimation AnimationPrefabBase => animationPrefab;
    public bool IgnoresShield => ignoresShield;

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
