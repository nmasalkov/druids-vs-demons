using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.Global;
using Game._Scripts.PlayerView;
using UnityEngine;

/// <summary>
/// Base for "action" states (Nuke, Spell, …) cast by the currently active side.
/// Shares the caster/target resolution helpers, the per-entry play loop, and the
/// post-action cleanup tail.
/// </summary>
public abstract class ActionState : GameState
{
    // --- Side / caster helpers (subclasses use these instead of re-deriving). ---

    protected static bool IsPlayerCasting =>
        GameManager.Instance.ActiveSide == ActiveSide.Player;

    protected static Hero Caster =>
        IsPlayerCasting ? G.PlayerHero : G.EnemyHero;

    protected static HeroView CasterView =>
        IsPlayerCasting ? G.PlayerView : G.EnemyView;

    protected static Hero EnemyHero =>
        IsPlayerCasting ? G.EnemyHero : G.PlayerHero;

    protected static Shield EnemyShield =>
        IsPlayerCasting ? G.EnemyView.Shield : G.PlayerView.Shield;

    protected static List<Creature> EnemyCreatures =>
        IsPlayerCasting
            ? G.EnemyCreaturesManager.GetAllCreatures()
            : G.PlayerCreaturesManager.GetAllCreatures();

    // --- Cleanup tail shared by NukeState / SpellState. ---

    /// <summary>
    /// Removes dead creatures from both sides. Safe to call from instant-resolve paths.
    /// </summary>
    protected static void CleanUpDeadCreatures()
    {
        G.PlayerCreaturesManager.CleanUpDead();
        G.EnemyCreaturesManager.CleanUpDead();
    }

    /// <summary>
    /// Waits <see cref="PostBattleStateManager.CleanUpDelay"/>, cleans up dead bodies,
    /// then completes this state. Used as the tail of animated action sequences.
    /// </summary>
    protected void CompleteWithCleanup()
    {
        float cleanUpDelay = PostBattleStateManager.Instance.CleanUpDelay;
        Utils.DoAfterDelay.Execute(() =>
        {
            CleanUpDeadCreatures();
            CompleteState();
        }, cleanUpDelay);
    }

    /// <summary>Bundle of side/target data for <see cref="ActionSO.CreateAndResolve"/>.</summary>
    protected static ActionContext BuildContext() => new()
    {
        Caster = Caster,
        CasterView = CasterView,
        EnemyCreatures = EnemyCreatures,
        EnemyHero = EnemyHero,
        EnemyShield = EnemyShield,
    };

    /// <summary>
    /// Plays each entry in order: resolves the action via <see cref="ActionSO.CreateAndResolve"/>,
    /// instantiates its animation prefab, waits for completion + <paramref name="pauseBetween"/>,
    /// then completes the state with a cleanup tail. Entries with a missing source or animation
    /// prefab are logged and skipped (placeholders).
    /// </summary>
    protected void PlayEntries<TEntry>(IReadOnlyList<TEntry> entries, float pauseBetween, string stateName)
        where TEntry : IActionEntry
    {
        if (entries.Count == 0)
        {
            CompleteWithCleanup();
            return;
        }
        PlayEntry(entries, 0, pauseBetween, stateName);
    }

    private void PlayEntry<TEntry>(IReadOnlyList<TEntry> entries, int index, float pauseBetween, string stateName)
        where TEntry : IActionEntry
    {
        if (index >= entries.Count)
        {
            CompleteWithCleanup();
            return;
        }

        var entry = entries[index];

        if (entry.Source == null)
        {
            Debug.LogWarning($"[{stateName}] Entry has no Source assigned, skipping.");
            ScheduleNext(entries, index, pauseBetween, stateName);
            return;
        }

        var prefab = entry.Source.AnimationPrefabBase;
        if (prefab == null)
        {
            Debug.LogWarning($"[{stateName}] No animation prefab on '{entry.Source.name}', skipping (placeholder?).");
            ScheduleNext(entries, index, pauseBetween, stateName);
            return;
        }

        // 1) Data layer: build & pre-resolve.
        var resolver = entry.Source.CreateAndResolve(BuildContext(), entry.Level);

        // 2) View layer: play the planned shots.
        var instance = Object.Instantiate(prefab);
        instance.Execute(entry.Source, Caster, resolver, () =>
        {
            Object.Destroy(instance.gameObject);
            ScheduleNext(entries, index, pauseBetween, stateName);
        });
    }

    private void ScheduleNext<TEntry>(IReadOnlyList<TEntry> entries, int currentIndex, float pauseBetween, string stateName)
        where TEntry : IActionEntry
    {
        Utils.DoAfterDelay.Execute(() => PlayEntry(entries, currentIndex + 1, pauseBetween, stateName), pauseBetween);
    }

    /// <summary>
    /// Instant-resolve path shared across action types (rule #7). Iterates entries, builds
    /// each resolver, applies its shots immediately, then cleans up dead bodies.
    /// </summary>
    public static void ResolveInstant<TEntry>(IReadOnlyList<TEntry> entries) where TEntry : IActionEntry
    {
        if (entries.Count == 0) return;
        var ctx = BuildContext();
        foreach (var e in entries)
        {
            if (e.Source == null) continue;
            e.Source.CreateAndResolve(ctx, e.Level).ApplyInstant();
        }
        CleanUpDeadCreatures();
    }
}
