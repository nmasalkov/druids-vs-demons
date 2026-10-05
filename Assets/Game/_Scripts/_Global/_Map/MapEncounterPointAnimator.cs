using DG.Tweening;
using UnityEngine;

/// <summary>
/// DOTween-driven transform animation for a MapEncounterPoint (CLAUDE.md rule 27, same shape as
/// RewardCardAnimator/ShieldAnimator): the enemy icon's "tiny to full size" pop when a node is
/// discovered, and the looping "you can click me" pulse of the whole point. Every Play/Start call
/// kills the active tween on that transform first and animates toward an absolute target cached in
/// Awake() (never a value relative to wherever it currently is), so rapid re-triggering can't
/// compound. Pulse magnitude/speed are passed in by MapManager, which owns them as map-wide
/// tunables. See docs/Encounters.md.
/// </summary>
[RequireComponent(typeof(MapEncounterPoint))]
public class MapEncounterPointAnimator : MonoBehaviour
{
    [Tooltip("Seconds the enemy icon takes to pop from zero to its authored scale when its node is discovered.")]
    [SerializeField] private float iconAppearDuration = 0.45f;
    [Tooltip("Ease of the icon pop. OutBack overshoots past full size and settles back — the yoyo-like 'pop'.")]
    [SerializeField] private Ease iconAppearEase = Ease.OutBack;
    [Tooltip("Seconds to settle back to the authored scale when pulsing stops.")]
    [SerializeField] private float pulseStopDuration = 0.15f;

    private Transform _icon;
    private Vector3 _baseScale;
    private Vector3 _iconBaseScale;
    private Tween _pulseTween;
    private Tween _iconTween;

    void Awake()
    {
        _baseScale = transform.localScale;
    }

    /// <summary>Called by MapEncounterPoint.Awake() with its icon transform, before anything plays.</summary>
    public void Init(Transform icon)
    {
        _icon = icon;
        _iconBaseScale = icon.localScale;
    }

    public void PlayIconAppear()
    {
        _iconTween?.Kill();
        _icon.localScale = Vector3.zero;
        _iconTween = _icon.DOScale(_iconBaseScale, iconAppearDuration).SetEase(iconAppearEase);
    }

    public void SetIconFullSize()
    {
        _iconTween?.Kill();
        _icon.localScale = _iconBaseScale;
    }

    public void StartPulse(float magnitude, float halfPeriod)
    {
        _pulseTween?.Kill();
        transform.localScale = _baseScale;
        _pulseTween = transform.DOScale(_baseScale * (1f + magnitude), halfPeriod)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void StopPulse()
    {
        _pulseTween?.Kill();
        _pulseTween = transform.DOScale(_baseScale, pulseStopDuration).SetEase(Ease.OutQuad);
    }

    void OnDestroy()
    {
        _pulseTween?.Kill();
        _iconTween?.Kill();
    }
}
