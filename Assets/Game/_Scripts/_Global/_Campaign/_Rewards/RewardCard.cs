using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reward-pick card in a RewardEncounter's offer of 3. A single full-card Button is the
/// click target (clicking anywhere on the card selects it, not a smaller sub-button) — see
/// docs/Rewards.md. Select/deselect/discard animation is owned by the sibling
/// <see cref="RewardCardAnimator"/> (DOTween), not inline here.
/// </summary>
[RequireComponent(typeof(RewardCardAnimator))]
public class RewardCard : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button button;

    private RewardCardAnimator _animator;

    public RewardSO Data { get; private set; }
    public event Action<RewardCard> OnClicked;

    /// <summary>Duration of the discard shrink-and-destroy animation, read by RewardEncounter so
    /// it can delay Complete() by exactly as long as the animation actually takes — no duplicated
    /// magic-number duration to keep in sync (rule 22).</summary>
    public float DiscardDuration => _animator.DiscardDuration;

    void Awake() => _animator = GetComponent<RewardCardAnimator>();
    void Start() => button.onClick.AddListener(NotifyClicked);
    void OnDestroy() => button.onClick.RemoveListener(NotifyClicked);

    public void Init(RewardSO reward)
    {
        Data = reward;
        iconImage.sprite = reward.EffectiveIcon;
        nameText.text = reward.rewardName;
        typeText.text = reward.typeLabel;
        descriptionText.text = reward.description;
    }

    /// <summary>Pure-display overload for LoadoutPickEncounterView's Comparison panel: shows an
    /// ActionSO (not a RewardSO) with a caller-supplied type label instead of RewardSO.typeLabel. Data
    /// (RewardSO-typed) stays null. Leaves Button.interactable alone deliberately —
    /// LoadoutPickEncounterView never wires OnClicked for these instances, so interactable is a no-op
    /// either way, and setting it false paints Button's default Disabled Color (semi-transparent grey)
    /// over the card, which is never wanted here.</summary>
    public void Init(ActionSO action, string typeLabel)
    {
        iconImage.sprite = action.cardSprite;
        nameText.text = action.actionName;
        typeText.text = typeLabel;
        descriptionText.text = action.description;
    }

    public void SetSelected(bool selected)
    {
        if (selected) _animator.PlaySelect();
        else _animator.PlayDeselect();
    }

    /// <summary>Plays the shrink-to-zero animation and self-destructs once it's done — the losing
    /// cards' fate once another card is claimed.</summary>
    public void PlayDiscard()
    {
        button.interactable = false;
        _animator.PlayDiscard(() => Destroy(gameObject));
    }

    private void NotifyClicked() => OnClicked?.Invoke(this);
}
