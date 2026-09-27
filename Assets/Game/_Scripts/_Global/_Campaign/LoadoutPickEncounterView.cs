using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI for LoadoutPickEncounter (CLAUDE.md rule 28): builds the 9 fixed equipped-slot cards, the
/// Available pool grid, and the Comparison panel purely in reaction to the backend's events, and
/// forwards every click back to the backend as a method call — never mutates RunState or decides
/// completion itself.
///
/// Every content update funnels through one Redraw(): pull a single LoadoutPickEncounter.State
/// snapshot via GetState(), then hand it to a few Draw&lt;Region&gt;(state) functions — instead of each
/// event having its own handler that separately queries the backend for just the one value it needs
/// (CLAUDE.md rule 28's state-snapshot addendum). Only two things are structural (Instantiate/Destroy,
/// not just re-Init on an existing instance) and get their own listener: SpawnLoadoutCards (once, on
/// OnLoadoutLoaded — replaces each anchor's disabled placeholder per rule 24) and RebuildAvailablePool
/// (whenever OnPoolChanged fires: load and a real Swap()). Ordering constraint: Play() fires
/// OnPoolChanged before OnLoadoutLoaded, so RebuildAvailablePool cannot call the shared Redraw() (the 9
/// equipped MiniCards don't exist yet on that first firing) — it only draws its own region.
/// SpawnLoadoutCards calls Redraw() itself once everything exists. See docs/Loadout.md.
/// </summary>
[RequireComponent(typeof(LoadoutPickEncounter))]
public class LoadoutPickEncounterView : MonoBehaviour
{
    [Header("Your Deck — fixed equipped-slot anchors")]
    [SerializeField] private Transform tankSlotAnchor;
    [SerializeField] private Transform archerSlotAnchor;
    [SerializeField] private Transform mageSlotAnchor;
    [SerializeField] private Transform[] nukeSlotAnchors = new Transform[3];
    [SerializeField] private Transform[] spellSlotAnchors = new Transform[3];
    [SerializeField] private MiniCard miniCardPrefab;

    [Header("Available pool — dynamic container")]
    [SerializeField] private Transform availableContainer;

    [Header("Comparison")]
    [SerializeField] private Transform leftCardAnchor;
    [SerializeField] private Transform rightCardAnchor;
    [SerializeField] private Button swapButton;

    [Header("Completion")]
    [SerializeField] private Button finishButton;

    private LoadoutPickEncounter _backend;
    private Dictionary<LoadoutPickEncounter.SlotRef, Transform> _equippedAnchors;
    private Dictionary<LoadoutPickEncounter.SlotRef, MiniCard> _equippedCards;
    private readonly List<MiniCard> _availableCards = new List<MiniCard>();
    private RewardCard _leftCard;
    private RewardCard _rightCard;

    // Subscribing here (not Start()) is required: MapManager calls Instantiate() then Play()
    // synchronously in the same method, and Play() fires OnLoadoutLoaded/OnPoolChanged inline — a
    // Start()-based subscription would miss them. See CLAUDE.md rule 28.
    void Awake()
    {
        _backend = GetComponent<LoadoutPickEncounter>();
        _backend.OnLoadoutLoaded += SpawnLoadoutCards;
        _backend.OnPoolChanged += RebuildAvailablePool;
        _backend.OnSelectionChanged += Redraw;
        _backend.OnConfirmed += ClosePresentation;

        _equippedAnchors = new Dictionary<LoadoutPickEncounter.SlotRef, Transform>
        {
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Tank), tankSlotAnchor },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Archer), archerSlotAnchor },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Mage), mageSlotAnchor },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Nuke, 0), nukeSlotAnchors[0] },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Nuke, 1), nukeSlotAnchors[1] },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Nuke, 2), nukeSlotAnchors[2] },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Spell, 0), spellSlotAnchors[0] },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Spell, 1), spellSlotAnchors[1] },
            { new LoadoutPickEncounter.SlotRef(LoadoutPickEncounter.SlotKind.Spell, 2), spellSlotAnchors[2] },
        };
        _equippedCards = new Dictionary<LoadoutPickEncounter.SlotRef, MiniCard>();
    }

    void Start()
    {
        swapButton.onClick.AddListener(RequestSwap);
        finishButton.onClick.AddListener(ConfirmLoadout);
    }

    void OnDestroy()
    {
        swapButton.onClick.RemoveListener(RequestSwap);
        finishButton.onClick.RemoveListener(ConfirmLoadout);

        _backend.OnLoadoutLoaded -= SpawnLoadoutCards;
        _backend.OnPoolChanged -= RebuildAvailablePool;
        _backend.OnSelectionChanged -= Redraw;
        _backend.OnConfirmed -= ClosePresentation;
    }

    /// <summary>Spawns all 9 equipped-slot cards (replacing each anchor's disabled placeholder, per
    /// CLAUDE.md rule 24) and locates the 2 reusable Comparison card placeholders, then draws the
    /// initial state — the Available pool was already built by the OnPoolChanged firing that precedes
    /// this event in Play(), so a full Redraw() here is safe and covers all three regions at once.</summary>
    private void SpawnLoadoutCards()
    {
        foreach (var pair in _equippedAnchors)
            SpawnEquippedCard(pair.Key, pair.Value);

        SpawnComparisonCardPlaceholders();
        Redraw();
    }

    private void SpawnEquippedCard(LoadoutPickEncounter.SlotRef slot, Transform anchor)
    {
        ClearChildren(anchor);
        var card = Instantiate(miniCardPrefab, anchor);
        card.OnClicked += _ => _backend.SetSelectedSlot(slot);
        _equippedCards[slot] = card;
    }

    /// <summary>Reuses the disabled RewardCard prefab instance already placed under each anchor
    /// (CLAUDE.md rule 24) instead of destroying and re-instantiating from a prefab reference — the
    /// placeholder's hand-tuned instance overrides (scale, size) are what make it fit the Comparison
    /// panel; instantiating fresh from the prefab asset loses those overrides and produces an
    /// oversized card. Both are already clickable (RewardCard's own full-card Button, same as a
    /// reward pick) — wired here to clear their own half of the selection, reusing
    /// SetSelectedSlot/SetSelectedCandidate's existing re-click-to-deselect toggle rather than adding
    /// new backend surface.</summary>
    private void SpawnComparisonCardPlaceholders()
    {
        _leftCard = leftCardAnchor.GetComponentInChildren<RewardCard>(true);
        _rightCard = rightCardAnchor.GetComponentInChildren<RewardCard>(true);
        _leftCard.OnClicked += _ => ClearSelectedSlot();
        _rightCard.OnClicked += _ => ClearSelectedCandidate();
    }

    /// <summary>Left card is only ever active while SelectedSlot is set (DrawComparisonPanel), so
    /// re-invoking SetSelectedSlot with its own current value is guaranteed to hit the toggle-off
    /// branch, not a retarget.</summary>
    private void ClearSelectedSlot() => _backend.SetSelectedSlot(_backend.SelectedSlot.Value);

    /// <summary>Right card is only ever active while SelectedCandidate is set, for the same reason.</summary>
    private void ClearSelectedCandidate() => _backend.SetSelectedCandidate(_backend.SelectedCandidate);

    /// <summary>Rebuilds the Available grid in full — the one genuinely variable-length list here
    /// (CLAUDE.md rule 24's carve-out). Fires only when CurrentPool's membership actually changes
    /// (load, Swap), never on a mere browse click. Must NOT call the shared Redraw(): on the very first
    /// firing (from Play()), the 9 equipped MiniCards don't exist yet, so DrawEquippedSlots would throw
    /// — it only rebuilds and draws its own region. The OnLoadoutLoaded/OnSelectionChanged firings that
    /// follow (both call Redraw()) cover the rest.</summary>
    private void RebuildAvailablePool()
    {
        var state = _backend.GetState();

        ClearChildren(availableContainer);
        _availableCards.Clear();

        foreach (var action in state.AvailablePool)
        {
            var card = Instantiate(miniCardPrefab, availableContainer);
            card.Init(action);
            card.OnClicked += SelectPoolCandidate;
            _availableCards.Add(card);
        }

        DrawAvailablePool(state);
    }

    /// <summary>Pulls one State snapshot and redraws every region from it — the single response to
    /// "something about the current selection or equipped content changed" (SetSelectedSlot,
    /// SetSelectedCandidate, Swap). Cheap: a handful of sprite/text assignments on a menu click, not a
    /// per-frame cost, so redrawing all 9 equipped cards unconditionally is simpler than tracking which
    /// one actually changed.</summary>
    private void Redraw()
    {
        var state = _backend.GetState();
        DrawEquippedSlots(state);
        DrawAvailablePool(state);
        DrawComparisonPanel(state);
    }

    private void DrawEquippedSlots(LoadoutPickEncounter.State state)
    {
        foreach (var pair in _equippedAnchors)
        {
            var slot = pair.Key;
            var card = _equippedCards[slot];
            card.Init(state.Equipped[slot]);
            card.SetSelected(state.SelectedSlot.HasValue && state.SelectedSlot.Value.Equals(slot));
            card.SetGreyedOut(!state.IsSlotSelectable(slot));
        }
    }

    private void DrawAvailablePool(LoadoutPickEncounter.State state)
    {
        foreach (var card in _availableCards)
        {
            card.SetSelected(card.Data == state.SelectedCandidate);
            card.SetGreyedOut(!state.IsCandidateSelectable(card.Data));
        }
    }

    /// <summary>Left and Right are fully independent of each other — each shows as soon as its own
    /// half of the selection exists, regardless of order or whether the other half is set yet. Picking
    /// an equipped slot first shows Left immediately (forward flow); picking a pool card first shows
    /// Right immediately (reverse flow) — same milestone, mirrored. Swap only ever enables once both
    /// are set.</summary>
    private void DrawComparisonPanel(LoadoutPickEncounter.State state)
    {
        if (state.SelectedSlot.HasValue) DrawLeftCardForSwap(state);
        else _leftCard.gameObject.SetActive(false);

        if (state.SelectedCandidate != null) DrawRightCardForSwap(state);
        else _rightCard.gameObject.SetActive(false);

        swapButton.interactable = state.SelectedSlot.HasValue && state.SelectedCandidate != null;
    }

    private void DrawLeftCardForSwap(LoadoutPickEncounter.State state)
    {
        var action = state.Equipped[state.SelectedSlot.Value];
        _leftCard.gameObject.SetActive(true);
        _leftCard.Init(action, GetTypeLabel(action));
    }

    private void DrawRightCardForSwap(LoadoutPickEncounter.State state)
    {
        var action = state.SelectedCandidate;
        _rightCard.gameObject.SetActive(true);
        _rightCard.Init(action, GetTypeLabel(action));
    }

    private void SelectPoolCandidate(MiniCard card) => _backend.SetSelectedCandidate(card.Data);
    private void RequestSwap() => _backend.Swap();
    private void ConfirmLoadout() => _backend.Confirm();

    /// <summary>No exit animation — completes immediately. Revisit if a transition beat is wanted
    /// later (mirrors RewardEncounterView's discard-delay shape).</summary>
    private void ClosePresentation() => _backend.CompletePresentation();

    /// <summary>Derives the Comparison panel's type label straight from the action instance, not a
    /// SlotKind — Left and Right each always have a concrete ActionSO by the time they're drawn
    /// (DrawComparisonPanel only calls these once their own half is set), and Right needs this even
    /// when no slot has been picked yet (reverse flow's first click).</summary>
    private static string GetTypeLabel(ActionSO action) => action switch
    {
        TankSO => "Tank",
        ArcherSO => "Archer",
        MageSO => "Mage",
        NukeSO => "Nuke",
        SpellSO => "Spell",
        _ => throw new System.ArgumentOutOfRangeException(nameof(action))
    };

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
