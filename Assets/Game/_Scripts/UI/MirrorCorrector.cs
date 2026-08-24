using _Scripts.Creatures;
using UnityEngine;

/// <summary>
/// Cancels an inherited negative-X ancestor scale so this world-space Canvas (health bar fill
/// direction + all its text) always renders right-reading, regardless of which side it's currently
/// parented under. The enemy side is mirrored via a single -1 localScale.x at EnemyView's root
/// (CLAUDE.md rule 15); everything under it — including a unit's HpbarCanvas — inherits that flip by
/// ordinary parent/child scale composition.
///
/// Corrects once at spawn, then again whenever the ancestor scale can change: a creature's own root
/// flips mid-battle via CreatureAnimator.RunToCurrentSlot (used by CharmShot to move a creature to
/// the opposite side) — Unity has no "an ancestor's scale changed" event, so this hooks the one
/// event that already fires once that settles, OnRunToSlotArrived, instead of polling every frame.
/// A Hero (no CreatureAnimator, never reparented after spawn) only ever needs the initial Awake()
/// correction.
/// </summary>
public class MirrorCorrector : MonoBehaviour
{
    private Vector3 _baseScale;
    private bool _correctingMirror;
    private CreatureAnimator _animator;

    void Awake()
    {
        _baseScale = transform.localScale;
        Correct();

        _animator = GetComponentInParent<CreatureAnimator>();
        if (_animator != null) _animator.OnRunToSlotArrived += Correct;
    }

    void OnDestroy()
    {
        if (_animator != null) _animator.OnRunToSlotArrived -= Correct;
    }

    private void Correct()
    {
        bool mirrored = transform.parent != null && transform.parent.lossyScale.x < 0;
        if (mirrored == _correctingMirror) return;

        _correctingMirror = mirrored;
        var scale = _baseScale;
        if (mirrored) scale.x = -scale.x;
        transform.localScale = scale;
    }
}
