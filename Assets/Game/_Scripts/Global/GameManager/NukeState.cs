using System.Collections.Generic;
using Game._Scripts.Global;
using UnityEngine;

public class NukeState : ActionState
{
    public override void OnStateStart()
    {
        var entries = RollStateManager.Instance.NukeEntries;
        if (entries.Count == 0)
        {
            CompleteWithCleanup();
            return;
        }

        PlayEntry(entries, 0);
    }

    /// <summary>
    /// Instant resolution path (no animation, no waits). See project rule 7.
    /// </summary>
    public static void ResolveNukesInstant()
    {
        var entries = RollStateManager.Instance.NukeEntries;
        if (entries.Count == 0) return;

        bool playerCasting = GameManager.Instance.ActiveSide == ActiveSide.Player;
        var caster = playerCasting ? G.PlayerHero : G.EnemyHero;
        var enemyCreatures = playerCasting
            ? G.EnemyCreaturesManager.GetAllCreatures()
            : G.PlayerCreaturesManager.GetAllCreatures();
        var enemyHero = playerCasting ? G.EnemyHero : G.PlayerHero;

        foreach (var entry in entries)
        {
            if (entry.Nuke == null) continue;
            var resolver = entry.Nuke.CreateResolver();
            resolver.Resolve(entry.Nuke, caster, enemyCreatures, enemyHero, entry.Count);
            resolver.ApplyInstant();
        }

        G.PlayerCreaturesManager.CleanUpDead();
        G.EnemyCreaturesManager.CleanUpDead();
    }

    private void PlayEntry(List<RollStateManager.NukeEntry> entries, int index)
    {
        if (index >= entries.Count)
        {
            CompleteWithCleanup();
            return;
        }

        var entry = entries[index];

        if (entry.Nuke == null)
        {
            Debug.LogWarning("[NukeState] NukeEntry has no Nuke assigned, skipping.");
            ScheduleNext(entries, index);
            return;
        }

        var prefab = entry.Nuke.AnimationPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[NukeState] No animation prefab assigned on '{entry.Nuke.name}', skipping (placeholder?).");
            ScheduleNext(entries, index);
            return;
        }

        bool playerCasting = GameManager.Instance.ActiveSide == ActiveSide.Player;
        var caster = playerCasting ? G.PlayerHero : G.EnemyHero;
        var enemyCreatures = playerCasting
            ? G.EnemyCreaturesManager.GetAllCreatures()
            : G.PlayerCreaturesManager.GetAllCreatures();
        var enemyHero = playerCasting ? G.EnemyHero : G.PlayerHero;

        // 1) Data layer: compute targets & damage.
        var resolver = entry.Nuke.CreateResolver();
        resolver.Resolve(entry.Nuke, caster, enemyCreatures, enemyHero, entry.Count);

        // 2) View layer: play the planned shots.
        var instance = Object.Instantiate(prefab);
        instance.Execute(entry.Nuke, caster, resolver, () =>
        {
            Object.Destroy(instance.gameObject);
            ScheduleNext(entries, index);
        });
    }

    private void ScheduleNext(List<RollStateManager.NukeEntry> entries, int currentIndex)
    {
        float pause = NukeStateManager.Instance.PauseBetweenNukes;
        Utils.DoAfterDelay.Execute(() => PlayEntry(entries, currentIndex + 1), pause);
    }

    /// <summary>
    /// Body cleanup that used to live in the separate PostNukeState. Nukes don't grant XP,
    /// so we just wait <c>CleanUpDelay</c> and remove dead creatures before completing.
    /// </summary>
    private void CompleteWithCleanup()
    {
        float cleanUpDelay = PostBattleStateManager.Instance.CleanUpDelay;
        Utils.DoAfterDelay.Execute(() =>
        {
            G.PlayerCreaturesManager.CleanUpDead();
            G.EnemyCreaturesManager.CleanUpDead();
            CompleteState();
        }, cleanUpDelay);
    }
}
