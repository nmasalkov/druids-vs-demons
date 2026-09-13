using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.Global;
using Game._Scripts.PlayerView;
using TMPro;
using UnityEngine;

public class BalanceTool : MonoBehaviour
{
    [Header("Debug HUD")]
    [Tooltip("When checked, the current game phase name is shown in the child TMP_Text label.")]
    public bool ShowPhaseName;
    [Tooltip("When checked, a live 'player : enemy' compared-firepower readout is shown in the " +
             "child TMP_Text label, updated every frame. Each side's number is its total creature " +
             "damage output MINUS the opposing barrier (Shield) HP standing in its way, floored at " +
             "0 — the same effective-firepower value FightSO's Comeback Settings read, so the HUD " +
             "and the rigging never disagree. See docs/Battle.md.")]
    public bool ShowFirepowers;
    [Tooltip("When checked, elapsed time since the player's first roll is shown in the child " +
             "TMP_Text label, updated every frame.")]
    public bool ShowTimer;

    private TMP_Text _phaseLabel;
    [SerializeField] private TMP_Text _firepowerLabel;
    [SerializeField] private TMP_Text _timerLabel;

    private bool _timerStarted;
    private float _timerStartTime;

    private void Awake()
    {
        // Rule #14: child helper cached via GetComponentInChildren. Convention: BalanceTool
        // has a child Canvas containing exactly one TMP_Text used as the debug phase label.
        _phaseLabel = GetComponentInChildren<TMP_Text>(true);
    }

    private void Start()
    {
        _phaseLabel.gameObject.SetActive(ShowPhaseName);
        _firepowerLabel.gameObject.SetActive(ShowFirepowers);
        _timerLabel.gameObject.SetActive(ShowTimer);
        GameState.OnAnyStateStarted += HandleStateStarted;
        GameState.OnAnyStateEnded += HandleStateEnded;
        GameManager.OnBattleRestart += ResetTimer;
    }

    private void OnDestroy()
    {
        GameState.OnAnyStateStarted -= HandleStateStarted;
        GameState.OnAnyStateEnded -= HandleStateEnded;
        GameManager.OnBattleRestart -= ResetTimer;
    }

    private void Update()
    {
        if (ShowFirepowers)
            _firepowerLabel.text = $"{AttacksResolver.EstimateEffectiveFirepower(true):0} : {AttacksResolver.EstimateEffectiveFirepower(false):0}";
        if (ShowTimer)
            _timerLabel.text = FormatElapsed();
    }

    private string FormatElapsed()
    {
        float elapsed = _timerStarted ? Time.time - _timerStartTime : 0f;
        var span = System.TimeSpan.FromSeconds(elapsed);
        return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
    }

    private void ResetTimer()
    {
        _timerStarted = false;
        _timerStartTime = 0f;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (_phaseLabel != null) _phaseLabel.gameObject.SetActive(ShowPhaseName);
        if (_firepowerLabel != null) _firepowerLabel.gameObject.SetActive(ShowFirepowers);
        if (_timerLabel != null) _timerLabel.gameObject.SetActive(ShowTimer);
    }

    private void HandleStateStarted(GameState state)
    {
        if (ShowPhaseName) _phaseLabel.text = $"{state.GetType().Name} Start:";
        TryStartTimer(state);
    }

    private void TryStartTimer(GameState state)
    {
        if (_timerStarted) return;
        if (state is not RollState) return;
        if (GameManager.Instance.ActiveSide != ActiveSide.Player) return;
        _timerStarted = true;
        _timerStartTime = Time.time;
    }

    private void HandleStateEnded(GameState state)
    {
        if (!ShowPhaseName) return;
        _phaseLabel.text = $"{state.GetType().Name} End.";
    }

    public void SpawnPlayer(CreatureSO creature)
    {
        var manager = G.PlayerCreaturesManager;
        var slot = GetSlotForCreature(manager, creature);
        if (slot.Creature != null)
        {
            HandleExistingCreature(slot);
            return;
        }
        manager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    public void SpawnEnemy(CreatureSO creature)
    {
        var manager = G.EnemyCreaturesManager;
        var slot = GetSlotForCreature(manager, creature);
        if (slot.Creature != null)
        {
            HandleExistingCreature(slot);
            return;
        }
        manager.SpawnCreatures(new List<CreatureSO> { creature });
    }

    private UnitSlot GetSlotForCreature(CreaturesManager manager, CreatureSO creature) => manager.GetNativeSlot(creature);

    private void HandleExistingCreature(UnitSlot slot)
    {
        var creature = slot.Creature;
        if (creature.Experience.Level >= 4)
        {
            slot.Creature = null;
            Object.Destroy(creature.gameObject);
        }
        else
        {
            creature.Experience.Promote();
        }
    }

    public void SpawnPlayerMage() => SpawnPlayer(G.DefaultCreatures.mage);
    public void SpawnPlayerArcher() => SpawnPlayer(G.DefaultCreatures.archer);
    public void SpawnPlayerTank() => SpawnPlayer(G.DefaultCreatures.tank);

    // Enemy buttons read the enemy pool (this fight's own roster, or its resolved fallback),
    // never G.DefaultCreatures — that's the player's campaign-resolved pool. Crossing the two
    // chains is the exact bug CLAUDE.md rule 29 documents.
    public void SpawnEnemyMage() => SpawnEnemy(G.EnemyCreatures.mage);
    public void SpawnEnemyArcher() => SpawnEnemy(G.EnemyCreatures.archer);
    public void SpawnEnemyTank() => SpawnEnemy(G.EnemyCreatures.tank);

    public bool IsBattleInProgress { get; private set; }

    public void PlayBattle()
    {
        if (IsBattleInProgress) return;
        IsBattleInProgress = true;

        var battle = new BattleState();
        battle.OnStateCompleted += () =>
        {
            var postBattle = new PostBattleState();
            postBattle.OnStateCompleted += () => IsBattleInProgress = false;
            postBattle.OnStateStart();
        };
        battle.BeginBattle();
    }

    // === Nuke Action testing ===

    /// <summary>
    /// 3 nuke slots × 3 levels = 9 toggles. NukeToggles[slotIndex, level-1].
    /// Editor renders this as a 3×3 grid. Player can pick 1..3 of them per cast.
    /// </summary>
    public readonly bool[,] NukeToggles = new bool[3, 3];

    /// <summary>When checked, "Play Nuke Action" casts from the enemy's side onto the player.</summary>
    public bool CastNukeAsEnemy;

    public bool IsNukeActionInProgress { get; private set; }

    public int CountSelectedNukes()
    {
        int count = 0;
        for (int s = 0; s < 3; s++)
            for (int l = 0; l < 3; l++)
                if (NukeToggles[s, l]) count++;
        return count;
    }

    public bool IsNukeSelectionValid()
    {
        int n = CountSelectedNukes();
        return n >= 1 && n <= 3;
    }

    public void ClearNukeToggles()
    {
        for (int s = 0; s < 3; s++)
            for (int l = 0; l < 3; l++)
                NukeToggles[s, l] = false;
    }

    public void PlayNukeAction()
    {
        if (IsNukeActionInProgress) return;
        if (!IsNukeSelectionValid()) return;

        IsNukeActionInProgress = true;

        GameManager.Instance.SetActiveSide(CastNukeAsEnemy ? ActiveSide.Enemy : ActiveSide.Player);

        var entries = BuildEntriesFromToggles();
        RollStateManager.Instance.NukeEntries.Clear();
        RollStateManager.Instance.NukeEntries.AddRange(entries);

        // NOTE: toggles are intentionally NOT cleared on completion. Clearing them would
        // make the "Play Nuke Action" button immediately disabled (selection becomes 0)
        // and look like the state is stuck. Keeping them lets the user fire the same
        // setup again or tweak it manually.
        // NukeState now self-contains its post-cleanup (dead bodies removed inside it),
        // so no separate PostNukeState wiring is needed here.
        var nuke = new NukeState();
        nuke.OnStateCompleted += () => IsNukeActionInProgress = false;
        nuke.OnStateStart();
    }

    private List<RollStateManager.NukeEntry> BuildEntriesFromToggles()
    {
        var entries = new List<RollStateManager.NukeEntry>();
        var nukes = new NukeSO[] { G.DefaultNukes.nukeA, G.DefaultNukes.nukeB, G.DefaultNukes.nukeC };

        for (int s = 0; s < 3; s++)
        {
            // Skip toggled rows whose DefaultNukes slot is empty (e.g. removed asset).
            if (nukes[s] == null) continue;

            for (int l = 0; l < 3; l++)
            {
                if (!NukeToggles[s, l]) continue;
                entries.Add(new RollStateManager.NukeEntry
                {
                    Nuke = nukes[s],
                    Count = l + 1
                });
            }
        }
        return entries;
    }

    // === Spell Action testing ===

    /// <summary>
    /// 3 spell slots × 3 levels = 9 toggles. SpellToggles[slotIndex, level-1].
    /// Mirrors NukeToggles.
    /// </summary>
    public readonly bool[,] SpellToggles = new bool[3, 3];

    /// <summary>When checked, "Play Spell Action" casts from the enemy's side onto the player.</summary>
    public bool CastSpellAsEnemy;

    public bool IsSpellActionInProgress { get; private set; }

    public int CountSelectedSpells()
    {
        int count = 0;
        for (int s = 0; s < 3; s++)
            for (int l = 0; l < 3; l++)
                if (SpellToggles[s, l]) count++;
        return count;
    }

    public bool IsSpellSelectionValid()
    {
        int n = CountSelectedSpells();
        return n >= 1 && n <= 3;
    }

    public void ClearSpellToggles()
    {
        for (int s = 0; s < 3; s++)
            for (int l = 0; l < 3; l++)
                SpellToggles[s, l] = false;
    }

    public void PlaySpellAction()
    {
        if (IsSpellActionInProgress) return;
        if (!IsSpellSelectionValid()) return;

        IsSpellActionInProgress = true;

        GameManager.Instance.SetActiveSide(CastSpellAsEnemy ? ActiveSide.Enemy : ActiveSide.Player);

        var entries = BuildSpellEntriesFromToggles();
        RollStateManager.Instance.SpellEntries.Clear();
        RollStateManager.Instance.SpellEntries.AddRange(entries);

        var spell = new SpellState();
        spell.OnStateCompleted += () => IsSpellActionInProgress = false;
        spell.OnStateStart();
    }

    private List<RollStateManager.SpellEntry> BuildSpellEntriesFromToggles()
    {
        var entries = new List<RollStateManager.SpellEntry>();
        var spells = new SpellSO[] { G.DefaultSpells.spellA, G.DefaultSpells.spellB, G.DefaultSpells.spellC };

        for (int s = 0; s < 3; s++)
        {
            if (spells[s] == null) continue;
            for (int l = 0; l < 3; l++)
            {
                if (!SpellToggles[s, l]) continue;
                entries.Add(new RollStateManager.SpellEntry
                {
                    Spell = spells[s],
                    Count = l + 1
                });
            }
        }
        return entries;
    }
}
