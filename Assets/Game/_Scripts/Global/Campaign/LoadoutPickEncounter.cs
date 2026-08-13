using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Backend for the pre-battle loadout picker (CLAUDE.md rule 28) — owns the pending edit state for
/// all 9 loadout slots (tank/archer/mage + 3 nukes + 3 spells), gated by RunState's gathered pools
/// (gatheredCreatureIds/gatheredNukeIds/gatheredSpellIds) unless unlockAll bypasses it. Presentation
/// (slot cards, the Available pool grid, the Comparison panel, Swap/Finish buttons) lives on the
/// paired LoadoutPickEncounterView component, which reads everything it needs through one snapshot —
/// GetState() — rather than several piecemeal backend reads (CLAUDE.md rule 28's state-snapshot
/// addendum). Confirm() is the only thing that ever writes into RunState — SetSelectedSlot/
/// SetSelectedCandidate only change what's being browsed/compared, exactly like
/// RewardEncounter.SelectReward() never claims anything by itself. See docs/Loadout.md.
/// </summary>
public class LoadoutPickEncounter : Encounter
{
    [Tooltip("Debug bypass: when checked, every catalog entry is available regardless of the gathered pools.")]
    [SerializeField] private bool unlockAll;

    public enum SlotKind { Tank, Archer, Mage, Nuke, Spell }

    /// <summary>Addresses one of the 9 loadout slots. Index is only meaningful for Nuke/Spell (0-2)
    /// — Tank/Archer/Mage each have exactly one slot, so Index stays 0 for those.</summary>
    public readonly struct SlotRef : IEquatable<SlotRef>
    {
        public readonly SlotKind Kind;
        public readonly int Index;

        public SlotRef(SlotKind kind, int index = 0)
        {
            Kind = kind;
            Index = index;
        }

        public bool Equals(SlotRef other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is SlotRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Kind, Index);
    }

    /// <summary>One-shot read snapshot for the View to redraw from (CLAUDE.md rule 28's state-snapshot
    /// addendum) — everything a redraw needs in a single GetState() call instead of several piecemeal
    /// backend reads spread across handlers. Not a cache: re-fetched fresh on every redraw, never held
    /// and mutated between frames, so rule 22 (single source of truth) still applies to the backend's
    /// own fields underneath it.</summary>
    public readonly struct State
    {
        public readonly IReadOnlyDictionary<SlotRef, ActionSO> Equipped;
        public readonly IReadOnlyList<ActionSO> AvailablePool;
        public readonly SlotRef? SelectedSlot;
        public readonly ActionSO SelectedCandidate;

        public State(IReadOnlyDictionary<SlotRef, ActionSO> equipped, IReadOnlyList<ActionSO> availablePool,
            SlotRef? selectedSlot, ActionSO selectedCandidate)
        {
            Equipped = equipped;
            AvailablePool = availablePool;
            SelectedSlot = selectedSlot;
            SelectedCandidate = selectedCandidate;
        }

        /// <summary>True if slot could accept SelectedCandidate right now — always true while no
        /// candidate is anchoring the selection. Drives equipped-slot greying in "Your Deck".</summary>
        public bool IsSlotSelectable(SlotRef slot) =>
            SelectedCandidate == null || CategoryMatches(slot.Kind, SelectedCandidate);

        /// <summary>True if candidate could be swapped into SelectedSlot right now — always true while
        /// no slot is anchoring the selection. Drives pool-card greying in Available.</summary>
        public bool IsCandidateSelectable(ActionSO candidate) =>
            !SelectedSlot.HasValue || CategoryMatches(SelectedSlot.Value.Kind, candidate);
    }

    private static readonly SlotKind[] AllKinds = (SlotKind[])Enum.GetValues(typeof(SlotKind));

    private ArcherSO _pendingArcher;
    private TankSO _pendingTank;
    private MageSO _pendingMage;
    private readonly NukeSO[] _pendingNukes = new NukeSO[3];
    private readonly SpellSO[] _pendingSpells = new SpellSO[3];
    private List<ActionSO> _currentPool;

    /// <summary>Currently targeted slot, or null while nothing anchors the selection from the equipped
    /// side. Kept as a standalone property (alongside GetState()) since headless test code often only
    /// needs this one value.</summary>
    public SlotRef? SelectedSlot { get; private set; }

    /// <summary>Currently highlighted candidate, or null while nothing anchors the selection from the
    /// pool side. Independent of SelectedSlot — either can be set without the other (bidirectional
    /// selection), but SetSelectedSlot/SetSelectedCandidate's own guards mean that whenever BOTH are
    /// set, they are always the same category — Swap()'s cast can never mismatch.</summary>
    public ActionSO SelectedCandidate { get; private set; }

    public event Action OnLoadoutLoaded;
    public event Action OnPoolChanged;
    public event Action OnSelectionChanged;
    public event Action OnConfirmed;

    private GameCatalog _catalog;
    private RunState _run;

    public override void Play(EncounterSO data)
    {
        _catalog = CampaignStateManager.Instance.Catalog;
        _run = CampaignStateManager.Instance.CurrentRun;

        _pendingArcher = _catalog.FindArcher(_run.archerId);
        _pendingTank = _catalog.FindTank(_run.tankId);
        _pendingMage = _catalog.FindMage(_run.mageId);
        _pendingNukes[0] = _catalog.FindNuke(_run.nukeAId);
        _pendingNukes[1] = _catalog.FindNuke(_run.nukeBId);
        _pendingNukes[2] = _catalog.FindNuke(_run.nukeCId);
        _pendingSpells[0] = _catalog.FindSpell(_run.spellAId);
        _pendingSpells[1] = _catalog.FindSpell(_run.spellBId);
        _pendingSpells[2] = _catalog.FindSpell(_run.spellCId);

        RefreshPool();
        OnPoolChanged?.Invoke();
        OnLoadoutLoaded?.Invoke();
    }

    /// <summary>Everything the View needs for one redraw pass, in a single call. See State's own doc
    /// comment for why this replaces several piecemeal getters (CLAUDE.md rule 28).</summary>
    public State GetState() => new State(BuildEquippedLookup(), _currentPool, SelectedSlot, SelectedCandidate);

    /// <summary>True if slot could accept SelectedCandidate right now. Same predicate State.
    /// IsSlotSelectable exposes on a snapshot — this instance version reads the live fields directly,
    /// used by SetSelectedSlot's own guard.</summary>
    public bool IsSlotSelectable(SlotRef slot) =>
        SelectedCandidate == null || CategoryMatches(slot.Kind, SelectedCandidate);

    /// <summary>True if candidate could be swapped into SelectedSlot right now. Live-field counterpart
    /// of State.IsCandidateSelectable, used by SetSelectedCandidate's own guard.</summary>
    public bool IsCandidateSelectable(ActionSO candidate) =>
        !SelectedSlot.HasValue || CategoryMatches(SelectedSlot.Value.Kind, candidate);

    /// <summary>Targets a slot for editing — the equipped-side half of bidirectional selection.
    /// No-ops if a candidate is already anchoring the selection and this slot doesn't match it (mirrors
    /// how a mismatched pool click is a no-op). Clicking the currently-targeted slot again untargets
    /// it; clicking a different (matching) slot always retargets — SelectedCandidate is left alone
    /// either way, since the guard above already guarantees it still matches whenever it's set.</summary>
    public void SetSelectedSlot(SlotRef slot)
    {
        if (!IsSlotSelectable(slot)) return;

        SelectedSlot = SelectedSlot.HasValue && SelectedSlot.Value.Equals(slot) ? (SlotRef?)null : slot;
        OnSelectionChanged?.Invoke();
    }

    /// <summary>Highlights a candidate for comparison — the pool-side half of bidirectional selection.
    /// No-ops if a slot is already anchoring the selection and this candidate doesn't match it. Clicking
    /// the already-highlighted candidate again clears it; clicking a different (matching) candidate
    /// always switches — SelectedSlot is left alone either way, for the same reason as above.</summary>
    public void SetSelectedCandidate(ActionSO candidate)
    {
        if (!IsCandidateSelectable(candidate)) return;

        SelectedCandidate = SelectedCandidate == candidate ? null : candidate;
        OnSelectionChanged?.Invoke();
    }

    /// <summary>Writes SelectedCandidate into SelectedSlot's pending backing field, then clears the
    /// selection and refreshes the pool. No-ops if either half of the selection is missing.</summary>
    public void Swap()
    {
        if (!SelectedSlot.HasValue || SelectedCandidate == null) return;

        var slot = SelectedSlot.Value;
        SetPending(slot, SelectedCandidate);

        SelectedSlot = null;
        SelectedCandidate = null;
        RefreshPool();

        OnPoolChanged?.Invoke();
        OnSelectionChanged?.Invoke();
    }

    /// <summary>Writes the pending edit state into RunState and saves immediately, unaffected by
    /// whatever SelectedSlot/SelectedCandidate currently are — only Swap() ever writes the pending
    /// fields this reads, exactly like RewardEncounter's Confirm()/SelectReward() split.</summary>
    public void Confirm()
    {
        _run.archerId = _pendingArcher.id;
        _run.tankId = _pendingTank.id;
        _run.mageId = _pendingMage.id;
        _run.nukeAId = _pendingNukes[0].id;
        _run.nukeBId = _pendingNukes[1].id;
        _run.nukeCId = _pendingNukes[2].id;
        _run.spellAId = _pendingSpells[0].id;
        _run.spellBId = _pendingSpells[1].id;
        _run.spellCId = _pendingSpells[2].id;
        CampaignStateManager.Instance.Save();

        OnConfirmed?.Invoke();
        if (Headless) CompletePresentation();
    }

    private void SetPending(SlotRef slot, ActionSO action)
    {
        switch (slot.Kind)
        {
            case SlotKind.Tank: _pendingTank = (TankSO)action; break;
            case SlotKind.Archer: _pendingArcher = (ArcherSO)action; break;
            case SlotKind.Mage: _pendingMage = (MageSO)action; break;
            case SlotKind.Nuke: _pendingNukes[slot.Index] = (NukeSO)action; break;
            case SlotKind.Spell: _pendingSpells[slot.Index] = (SpellSO)action; break;
        }
    }

    private Dictionary<SlotRef, ActionSO> BuildEquippedLookup() => new Dictionary<SlotRef, ActionSO>
    {
        { new SlotRef(SlotKind.Tank), _pendingTank },
        { new SlotRef(SlotKind.Archer), _pendingArcher },
        { new SlotRef(SlotKind.Mage), _pendingMage },
        { new SlotRef(SlotKind.Nuke, 0), _pendingNukes[0] },
        { new SlotRef(SlotKind.Nuke, 1), _pendingNukes[1] },
        { new SlotRef(SlotKind.Nuke, 2), _pendingNukes[2] },
        { new SlotRef(SlotKind.Spell, 0), _pendingSpells[0] },
        { new SlotRef(SlotKind.Spell, 1), _pendingSpells[1] },
        { new SlotRef(SlotKind.Spell, 2), _pendingSpells[2] },
    };

    private void RefreshPool() => _currentPool = ComputeFullUnequippedPool();

    private static bool CategoryMatches(SlotKind kind, ActionSO action) => kind switch
    {
        SlotKind.Tank => action is TankSO,
        SlotKind.Archer => action is ArcherSO,
        SlotKind.Mage => action is MageSO,
        SlotKind.Nuke => action is NukeSO,
        SlotKind.Spell => action is SpellSO,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private List<ActionSO> ComputeFullUnequippedPool()
    {
        var pending = AllPendingIds();
        var pool = new List<ActionSO>();
        foreach (var kind in AllKinds)
            pool.AddRange(CategoryPool(kind).Where(a => (unlockAll || IsGathered(kind, a.id)) && !pending.Contains(a.id)));
        return pool;
    }

    private IEnumerable<ActionSO> CategoryPool(SlotKind kind) => kind switch
    {
        SlotKind.Tank => _catalog.allCreatures.OfType<TankSO>().Cast<ActionSO>(),
        SlotKind.Archer => _catalog.allCreatures.OfType<ArcherSO>().Cast<ActionSO>(),
        SlotKind.Mage => _catalog.allCreatures.OfType<MageSO>().Cast<ActionSO>(),
        SlotKind.Nuke => _catalog.allNukes.Cast<ActionSO>(),
        SlotKind.Spell => _catalog.allSpells.Cast<ActionSO>(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private bool IsGathered(SlotKind kind, string id) => kind switch
    {
        SlotKind.Tank or SlotKind.Archer or SlotKind.Mage => _run.gatheredCreatureIds.Contains(id),
        SlotKind.Nuke => _run.gatheredNukeIds.Contains(id),
        SlotKind.Spell => _run.gatheredSpellIds.Contains(id),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private HashSet<string> AllPendingIds() => new HashSet<string>
    {
        _pendingArcher.id, _pendingTank.id, _pendingMage.id,
        _pendingNukes[0].id, _pendingNukes[1].id, _pendingNukes[2].id,
        _pendingSpells[0].id, _pendingSpells[1].id, _pendingSpells[2].id,
    };
}
