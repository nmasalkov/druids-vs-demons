using System;
using System.Collections.Generic;
using Game._Scripts.Creatures;
using Game._Scripts.Global;
using Game._Scripts.Pickups;
using UnityEngine;

public class ExperienceManager : MonoBehaviour
{
    public static ExperienceManager Instance { get; private set; }

    [SerializeField] private GameObject experienceGemPrefab;

    private readonly List<PendingXpEntry> pendingXp = new();
    private readonly List<GemVisual> activeGems = new();
    private int gemsInFlight;

    public event Action OnAllGemsCollected;

    private struct PendingXpEntry
    {
        public Creature Owner;
        public int Amount;
    }

    private struct GemVisual
    {
        public Creature Owner;
        public int Amount;
        public ExpirienceGem Gem;
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameManager.OnBattleRestart += ClearForRestart;
    }

    void OnDestroy()
    {
        GameManager.OnBattleRestart -= ClearForRestart;
    }

    /// <summary>
    /// Called right after battle resolve. Registers that this attacker earned XP for participating in a kill.
    /// No gem is spawned yet — that happens when the projectile actually lands.
    /// </summary>
    public void RegisterPendingXp(Creature owner, int amount)
    {
        pendingXp.Add(new PendingXpEntry { Owner = owner, Amount = amount });
    }

    /// <summary>
    /// Called from OnHit callback when a projectile that participated in a kill lands.
    /// Spawns a gem at the target position and links it to the first unmatched pending XP entry for this owner.
    /// </summary>
    public void SpawnGem(Vector3 position, Creature owner)
    {
        // Find the matching pending XP entry
        int index = pendingXp.FindIndex(p => p.Owner == owner);
        if (index < 0)
        {
            return;
        }

        var entry = pendingXp[index];
        pendingXp.RemoveAt(index);

        var go = Instantiate(experienceGemPrefab, position, Quaternion.identity);
        var gem = go.GetComponent<ExpirienceGem>();
        gem.Drop();

        activeGems.Add(new GemVisual
        {
            Owner = entry.Owner,
            Amount = entry.Amount,
            Gem = gem
        });
    }

    /// <summary>
    /// Spawns a test gem at the given position with a drop animation. For debugging only.
    /// </summary>
    public void SpawnTestGem(Vector3 position)
    {
        var go = Instantiate(experienceGemPrefab, position, Quaternion.identity);
        var gem = go.GetComponent<ExpirienceGem>();
        gem.Drop();
    }

    /// <summary>
    /// Sends all active gems flying to their owners with staggered launches.
    /// Gems whose owner is dead are destroyed. Remaining pending XP without gems is granted directly.
    /// Fires OnAllGemsCollected when complete.
    /// </summary>
    public void ResolveGems()
    {
        GrantOrphanedPendingXp();
        CleanUpDeadOwnerGems();

        if (activeGems.Count == 0)
        {
            OnAllGemsCollected?.Invoke();
            return;
        }

        LaunchGemsToOwners();
    }

    private void GrantOrphanedPendingXp()
    {
        foreach (var p in pendingXp)
        {
            if (p.Owner != null && !p.Owner.Health.IsDead())
                p.Owner.Experience.AddExperience(p.Amount);
        }
        pendingXp.Clear();
    }

    private void CleanUpDeadOwnerGems()
    {
        activeGems.RemoveAll(g => g.Gem == null);

        for (int i = activeGems.Count - 1; i >= 0; i--)
        {
            var data = activeGems[i];
            if (data.Owner == null || data.Owner.Health.IsDead())
            {
                Destroy(data.Gem.gameObject);
                activeGems.RemoveAt(i);
            }
        }
    }

    private void LaunchGemsToOwners()
    {
        const float stagger = 0.15f;
        gemsInFlight = activeGems.Count;
        int generation = GameManager.Instance.Generation;

        for (int i = 0; i < activeGems.Count; i++)
        {
            var d = activeGems[i];
            float localDelay = i * stagger;

            Utils.DoAfterDelay.Execute(() =>
            {
                if (GameManager.IsStale(generation)) return;
                d.Gem.FlyTo(d.Owner.transform, () =>
                {
                    if (GameManager.IsStale(generation)) return;
                    d.Owner.Experience.AddExperience(d.Amount);
                    OnGemArrived();
                });
            }, localDelay);
        }

        activeGems.Clear();
    }

    private void OnGemArrived()
    {
        gemsInFlight--;
        if (gemsInFlight <= 0)
            OnAllGemsCollected?.Invoke();
    }

    /// <summary>
    /// Instantly resolves all pending XP and active gems: grants experience, destroys gems, no animations.
    /// </summary>
    public void ResolveGemsInstant()
    {
        foreach (var p in pendingXp)
        {
            if (p.Owner != null && !p.Owner.Health.IsDead())
                p.Owner.Experience.AddExperience(p.Amount);
        }
        pendingXp.Clear();

        foreach (var data in activeGems)
        {
            if (data.Gem != null)
                Destroy(data.Gem.gameObject);

            if (data.Owner != null && !data.Owner.Health.IsDead())
                data.Owner.Experience.AddExperience(data.Amount);
        }
        activeGems.Clear();
    }

    /// <summary>
    /// Discards all pending XP and in-flight gems without granting anything. Used by battle restart.
    /// </summary>
    public void ClearForRestart()
    {
        foreach (var g in activeGems)
            if (g.Gem != null) Destroy(g.Gem.gameObject);
        activeGems.Clear();
        pendingXp.Clear();
        gemsInFlight = 0;
    }
}






