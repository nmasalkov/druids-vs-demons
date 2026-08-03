using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Data/backend layer for the future pre-battle loadout picker (CLAUDE.md rule 28) — no UI yet, not
/// wired to any prefab/EncounterSO/scene. The existing LoadoutPickSO/LoadoutEncounter placeholder
/// (briefing text + dismiss-on-click) stays untouched and live; this is a separate, standalone script
/// ready to be paired with a LoadoutPickEncounterView once the visual design/player controls exist.
///
/// Holds a temporary "pending" edit state seeded from RunState's loadout ids on Play(),
/// mutated only via the Set* methods, and persisted into RunState only on Confirm(). Availability is
/// gated by RunState's gathered pools (gatheredCreatureIds/gatheredNukeIds/gatheredSpellIds) unless
/// unlockAll bypasses it. See docs/Encounters.md and docs/Campaign.md.
/// </summary>
public class LoadoutPickEncounter : Encounter
{
    [Tooltip("Debug bypass: when checked, every catalog entry is available regardless of the gathered pools.")]
    [SerializeField] private bool unlockAll;

    public enum Slot { A, B, C }

    public readonly struct Option<T> where T : ActionSO
    {
        public readonly T Action;
        public readonly bool InUse;

        public Option(T action, bool inUse)
        {
            Action = action;
            InUse = inUse;
        }
    }

    public ArcherSO PendingArcher { get; private set; }
    public TankSO PendingTank { get; private set; }
    public MageSO PendingMage { get; private set; }

    private readonly NukeSO[] _pendingNukes = new NukeSO[3];
    private readonly SpellSO[] _pendingSpells = new SpellSO[3];

    public NukeSO GetPendingNuke(Slot slot) => _pendingNukes[(int)slot];
    public SpellSO GetPendingSpell(Slot slot) => _pendingSpells[(int)slot];

    /// <summary>Fired whenever a Set* call changes the pending edit state.</summary>
    public event Action OnLoadoutChanged;

    /// <summary>Fired once Confirm() has written the pending state into RunState and saved.</summary>
    public event Action OnConfirmed;

    private GameCatalog _catalog;
    private RunState _run;

    /// <summary>
    /// data is unused — this backend derives everything from RunState/GameCatalog, not from any
    /// EncounterSO field. Deciding what concrete EncounterSO eventually drives this is a future task.
    /// </summary>
    public override void Play(EncounterSO data)
    {
        _catalog = CampaignStateManager.Instance.Catalog;
        _run = CampaignStateManager.Instance.CurrentRun;

        PendingArcher = _catalog.FindArcher(_run.archerId);
        PendingTank = _catalog.FindTank(_run.tankId);
        PendingMage = _catalog.FindMage(_run.mageId);

        _pendingNukes[0] = _catalog.FindNuke(_run.nukeAId);
        _pendingNukes[1] = _catalog.FindNuke(_run.nukeBId);
        _pendingNukes[2] = _catalog.FindNuke(_run.nukeCId);

        _pendingSpells[0] = _catalog.FindSpell(_run.spellAId);
        _pendingSpells[1] = _catalog.FindSpell(_run.spellBId);
        _pendingSpells[2] = _catalog.FindSpell(_run.spellCId);
    }

    public IReadOnlyList<Option<ArcherSO>> GetArcherOptions() =>
        _catalog.allCreatures.OfType<ArcherSO>()
            .Where(a => unlockAll || _run.gatheredCreatureIds.Contains(a.id) || a == PendingArcher)
            .Select(a => new Option<ArcherSO>(a, a == PendingArcher))
            .ToList();

    public IReadOnlyList<Option<TankSO>> GetTankOptions() =>
        _catalog.allCreatures.OfType<TankSO>()
            .Where(t => unlockAll || _run.gatheredCreatureIds.Contains(t.id) || t == PendingTank)
            .Select(t => new Option<TankSO>(t, t == PendingTank))
            .ToList();

    public IReadOnlyList<Option<MageSO>> GetMageOptions() =>
        _catalog.allCreatures.OfType<MageSO>()
            .Where(m => unlockAll || _run.gatheredCreatureIds.Contains(m.id) || m == PendingMage)
            .Select(m => new Option<MageSO>(m, m == PendingMage))
            .ToList();

    public IReadOnlyList<Option<NukeSO>> GetNukeOptions() =>
        _catalog.allNukes
            .Where(n => unlockAll || _run.gatheredNukeIds.Contains(n.id) || _pendingNukes.Contains(n))
            .Select(n => new Option<NukeSO>(n, _pendingNukes.Contains(n)))
            .ToList();

    public IReadOnlyList<Option<SpellSO>> GetSpellOptions() =>
        _catalog.allSpells
            .Where(s => unlockAll || _run.gatheredSpellIds.Contains(s.id) || _pendingSpells.Contains(s))
            .Select(s => new Option<SpellSO>(s, _pendingSpells.Contains(s)))
            .ToList();

    public void SetArcher(ArcherSO archer) { PendingArcher = archer; OnLoadoutChanged?.Invoke(); }
    public void SetTank(TankSO tank) { PendingTank = tank; OnLoadoutChanged?.Invoke(); }
    public void SetMage(MageSO mage) { PendingMage = mage; OnLoadoutChanged?.Invoke(); }
    public void SetNuke(Slot slot, NukeSO nuke) { _pendingNukes[(int)slot] = nuke; OnLoadoutChanged?.Invoke(); }
    public void SetSpell(Slot slot, SpellSO spell) { _pendingSpells[(int)slot] = spell; OnLoadoutChanged?.Invoke(); }

    /// <summary>
    /// Writes the pending edit state into RunState and saves immediately. Headless must be set true
    /// before Play() for this to ever complete, since no View exists yet to call
    /// CompletePresentation() itself.
    /// </summary>
    public void Confirm()
    {
        _run.archerId = PendingArcher.id;
        _run.tankId = PendingTank.id;
        _run.mageId = PendingMage.id;
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
}
