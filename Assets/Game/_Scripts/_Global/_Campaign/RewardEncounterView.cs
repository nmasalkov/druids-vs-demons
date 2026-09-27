using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;

/// <summary>
/// UI for RewardEncounter (CLAUDE.md rule 28): spawns/updates reward cards and text purely in
/// reaction to the backend's events, and forwards every click back to the backend as a method call —
/// never mutates RunState or decides completion itself. Cards spawn one per fixed
/// <see cref="cardSlots"/> anchor rather than into a single dynamic container — see CLAUDE.md's
/// anchor+disabled-template rule for why. See docs/Rewards.md.
/// </summary>
[RequireComponent(typeof(RewardEncounter))]
public class RewardEncounterView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button claimButton;
    [SerializeField] private Transform[] cardSlots;
    [SerializeField] private RewardCard cardPrefab;

    private RewardEncounter _backend;
    private readonly List<RewardCard> _spawnedCards = new List<RewardCard>();

    // Subscribing here (not Start()) is required: BattleRewardPresenter calls Instantiate() then
    // Play() synchronously in the same method, and Play() fires OnRewardsDrawn inline — a
    // Start()-based subscription would miss it, since Unity defers Start() to later that frame. See
    // CLAUDE.md rule 28.
    void Awake()
    {
        _backend = GetComponent<RewardEncounter>();
        _backend.OnRewardsDrawn += HandleRewardsDrawn;
        _backend.OnSelectionChanged += HandleSelectionChanged;
        _backend.OnClaimed += HandleClaimed;
    }

    void Start() => claimButton.onClick.AddListener(HandleClaimClicked);
    void OnDestroy()
    {
        claimButton.onClick.RemoveListener(HandleClaimClicked);
        _backend.OnRewardsDrawn -= HandleRewardsDrawn;
        _backend.OnSelectionChanged -= HandleSelectionChanged;
        _backend.OnClaimed -= HandleClaimed;
    }

    /// <summary>Spawns one card per slot, clearing whatever's already parented there first — each
    /// slot's disabled placeholder RewardCard (kept for Inspector debugging, see CLAUDE.md) or a
    /// leftover from a previous draw.</summary>
    private void HandleRewardsDrawn(IReadOnlyList<RewardSO> rewards)
    {
        messageText.text = $"You got {_backend.RewardAmount} energy!";
        claimButton.interactable = false;

        _spawnedCards.Clear();
        for (int i = 0; i < cardSlots.Length; i++)
        {
            var slot = cardSlots[i];
            for (int c = slot.childCount - 1; c >= 0; c--)
                Destroy(slot.GetChild(c).gameObject);

            var card = Instantiate(cardPrefab, slot);
            card.Init(rewards[i]);
            card.OnClicked += HandleCardClicked;
            _spawnedCards.Add(card);
        }
    }

    private void HandleCardClicked(RewardCard card) => _backend.SelectReward(card.Data);

    private void HandleSelectionChanged(RewardSO reward)
    {
        foreach (var card in _spawnedCards) card.SetSelected(card.Data == reward);
        claimButton.interactable = reward != null;
    }

    private void HandleClaimClicked() => _backend.Confirm();

    /// <summary>Plays every other card's discard (shrink-and-destroy) feedback and waits for the
    /// longest of those before actually completing the encounter — so the discard animation is
    /// visible instead of getting cut off by the immediate scene/encounter transition Complete()
    /// triggers.</summary>
    private void HandleClaimed()
    {
        claimButton.interactable = false;

        float discardDuration = 0f;
        foreach (var card in _spawnedCards)
        {
            if (card.Data == _backend.SelectedReward) continue;
            card.PlayDiscard();
            discardDuration = Mathf.Max(discardDuration, card.DiscardDuration);
        }

        DoAfterDelay.Execute(_backend.CompletePresentation, discardDuration);
    }
}
