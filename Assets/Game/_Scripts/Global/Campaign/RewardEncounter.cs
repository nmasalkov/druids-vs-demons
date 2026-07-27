using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utils;

/// <summary>
/// Post-fight encounter: grants the guaranteed energy reward (RewardPickSO.energyReward)
/// immediately, then offers 3 reward cards drawn by RewardDrawer — pick one, then Claim to
/// advance. Only the Claim button advances it — unlike LoadoutEncounter, a stray click elsewhere
/// must not grant a reward early. See docs/Rewards.md and docs/Encounters.md.
///
/// Cards spawn one per fixed <see cref="cardSlots"/> anchor rather than into a single dynamic
/// container — see CLAUDE.md's anchor+disabled-template rule for why.
/// </summary>
public class RewardEncounter : Encounter
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private Button claimButton;
    [SerializeField] private Transform[] cardSlots;
    [SerializeField] private RewardCard cardPrefab;

    private RewardPickSO _data;
    private readonly List<RewardCard> _spawnedCards = new List<RewardCard>();
    private RewardCard _selectedCard;

    void Start() => claimButton.onClick.AddListener(HandleClaimClicked);
    void OnDestroy() => claimButton.onClick.RemoveListener(HandleClaimClicked);

    public override void Play(EncounterSO data)
    {
        _data = (RewardPickSO)data;
        var run = CampaignStateManager.Instance.CurrentRun;

        run.currentEnergy += _data.energyReward;
        messageText.text = $"You got {_data.energyReward} energy!";
        CampaignStateManager.Instance.Save();

        SpawnCards(run);
        claimButton.interactable = false;
    }

    /// <summary>Spawns one card per slot, clearing whatever's already parented there first — each
    /// slot's disabled placeholder RewardCard (kept for Inspector debugging, see CLAUDE.md) or a
    /// leftover from a previous draw.</summary>
    private void SpawnCards(RunState run)
    {
        var rewards = DebugRewards.Instance != null
            ? DebugRewards.Instance.ConsumeOverrideDraw() ?? RewardDrawer.DrawThree(G.RewardList, run)
            : RewardDrawer.DrawThree(G.RewardList, run);
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

    private void HandleCardClicked(RewardCard card)
    {
        if (_selectedCard != null) _selectedCard.SetSelected(false);
        _selectedCard = card;
        _selectedCard.SetSelected(true);
        claimButton.interactable = true;
    }

    /// <summary>Claims the selected card, plays every other card's discard (shrink-and-destroy)
    /// feedback, and waits for the longest of those before actually completing the encounter — so
    /// the discard animation is visible instead of getting cut off by the immediate scene/encounter
    /// transition Complete() triggers.</summary>
    private void HandleClaimClicked()
    {
        if (_selectedCard == null) return;

        claimButton.interactable = false;

        var run = CampaignStateManager.Instance.CurrentRun;
        _selectedCard.Data.Claim(run);
        CampaignStateManager.Instance.Save();

        float discardDuration = 0f;
        foreach (var card in _spawnedCards)
        {
            if (card == _selectedCard) continue;
            card.PlayDiscard();
            discardDuration = Mathf.Max(discardDuration, card.DiscardDuration);
        }

        DoAfterDelay.Execute(Complete, discardDuration);
    }
}
