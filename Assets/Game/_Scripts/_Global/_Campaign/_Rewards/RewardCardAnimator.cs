using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Owns RewardCard's select/deselect/discard visual tweens via DOTween — same pattern as
/// ShieldAnimator. Every Play* call kills whatever tween is currently running first and always
/// animates toward an absolute fixed target (scale 1/selectScale/0, base anchored position or
/// base + offset) rather than a value relative to wherever the card currently is, so rapid
/// re-triggering (fast select/deselect toggling) can never compound or leave the card stuck at a
/// wrong scale.
///
/// Replaces an earlier MMF_Player (MMF_Scale/MMF_Position, "ToDestination" mode) version: that
/// mode still applies MMF_Scale's RemapCurveZero/RemapCurveOne on top of the already-lerped value
/// (left at their class defaults of 1/2, meant for a different mode), so each play landed on the
/// wrong intermediate scale and the next play captured that wrong value as its own starting
/// point — compounding into runaway scale growth across repeated select/deselect. See
/// docs/Rewards.md.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RewardCardAnimator : MonoBehaviour
{
    [Header("Select / Deselect")]
    [SerializeField] private float selectScale = 1.12f;
    [SerializeField] private float selectMoveUp = 15f;
    [SerializeField] private float selectDuration = 0.15f;
    [SerializeField] private Ease selectEase = Ease.OutBack;
    [SerializeField] private Ease deselectEase = Ease.OutQuad;

    [Header("Discard")]
    [SerializeField] private float discardDuration = 0.2f;
    [SerializeField] private Ease discardEase = Ease.InBack;

    public float DiscardDuration => discardDuration;

    private RectTransform _cardTransform;
    private Vector2 _baseAnchoredPosition;
    private Tween _activeScaleTween;
    private Tween _activePositionTween;

    void Awake()
    {
        _cardTransform = GetComponent<RectTransform>();
        _baseAnchoredPosition = _cardTransform.anchoredPosition;
    }

    public void PlaySelect()
    {
        KillActive();
        _activeScaleTween = _cardTransform.DOScale(selectScale, selectDuration).SetEase(selectEase);
        _activePositionTween = _cardTransform
            .DOAnchorPos(_baseAnchoredPosition + new Vector2(0f, selectMoveUp), selectDuration)
            .SetEase(selectEase);
    }

    public void PlayDeselect()
    {
        KillActive();
        _activeScaleTween = _cardTransform.DOScale(1f, selectDuration).SetEase(deselectEase);
        _activePositionTween = _cardTransform.DOAnchorPos(_baseAnchoredPosition, selectDuration).SetEase(deselectEase);
    }

    public void PlayDiscard(Action onComplete)
    {
        KillActive();
        _activeScaleTween = _cardTransform.DOScale(0f, discardDuration)
            .SetEase(discardEase)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void KillActive()
    {
        if (_activeScaleTween != null && _activeScaleTween.IsActive()) _activeScaleTween.Kill();
        if (_activePositionTween != null && _activePositionTween.IsActive()) _activePositionTween.Kill();
    }

    void OnDestroy() => KillActive();
}
