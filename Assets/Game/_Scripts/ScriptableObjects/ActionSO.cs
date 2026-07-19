using UnityEngine;

public abstract class ActionSO : ScriptableObject
{
    [Tooltip("Stable identifier used to resolve this asset from campaign save data (RunState) via GameCatalog. Not shown to players — actionName is the display string.")]
    public string id;

    public string actionName;
    public Sprite cardSprite;

    /// <summary>
    /// Animation prefab played by the action's state. Null by default — only "active"
    /// action SOs (Nuke, Spell, …) that go through <see cref="ActionState"/>'s play loop
    /// need to override this. Creature-spawn rolls don't use it.
    /// </summary>
    public virtual ActionAnimation AnimationPrefabBase => null;

    /// <summary>
    /// Create the resolver for this action and immediately resolve it against
    /// <paramref name="ctx"/> at the given <paramref name="level"/>. Default returns null;
    /// override on Nuke/Spell-like SOs. Creature rolls don't use this path.
    /// </summary>
    public virtual ActionResolver CreateAndResolve(ActionContext ctx, int level) => null;
}

