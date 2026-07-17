# Experience & Gems

## What this system does

Creatures earn XP for participating in kills during battle and level up when their cumulative XP
crosses a threshold defined on their `CreatureSO`. XP isn't granted the instant a kill happens —
it's staged as "pending," then materializes as a gem that visually flies from the kill location to
the earning creature and grants XP on arrival. `ExperienceManager` is the singleton that owns this
staging → gem-flight → grant pipeline; `Experience` (on each `Creature`) owns the actual level/XP
state and promotion math.

## Key files

- `Assets/Game/_Scripts/Units/ExperienceManager.cs` — singleton; owns pending XP + in-flight gem
  bookkeeping, drives the gem-flight sequence, exposes the rule-7 instant path and the
  battle-restart reset.
- `Assets/Game/_Scripts/Units/Expirience.cs` — despite the filename, declares `class Experience`
  (in namespace `Game._Scripts.Creatures`). Lives on every `Creature` (`[RequireComponent(typeof(Experience))]`
  on `Creature.cs`). Owns `Level`, `TotalExperience`, and promotion.
- `Assets/Game/_Scripts/Pickups/ExpirienceGem.cs` — the flying/dropping gem visual (DOTween-based
  curved paths), no gameplay state of its own.
- `Assets/Game/_Scripts/ScriptableObjects/CreatureSO.cs` — per-level stats, XP rewards, XP
  thresholds (the balance data this whole system reads).
- `Assets/Game/_Scripts/Units/Creature.cs` — exposes `Experience` as a cached component reference.
- Callers: `Assets/Game/_Scripts/Global/GameManager/AttacksResolver.cs` /
  `AttacksResolver.Mechanics.cs` (registers pending XP and spawns gems from battle resolution —
  see the Battle system's own doc for how attacks are planned/executed),
  `Assets/Game/_Scripts/Global/GameManager/PostBattleState.cs` (animated resolve),
  `Assets/Game/_Scripts/Global/GameManager/BattleState.cs` (instant resolve).

## The pending-XP → gem flow

1. **`RegisterPendingXp(Creature owner, int amount)`** — called from `AttacksResolver` right after
   battle resolve, once per attacker that participated in a kill. Just appends a
   `PendingXpEntry {Owner, Amount}` to `pendingXp`. No gem exists yet.
2. **`SpawnGem(Vector3 position, Creature owner)`** — called from an attack's `OnHit` callback when
   a projectile that participated in a kill actually lands (`AttacksResolver.Mechanics.cs`).
   Finds the *first* unmatched `pendingXp` entry for that owner (`FindIndex`), removes it from
   `pendingXp`, instantiates `experienceGemPrefab` at the hit position, calls `gem.Drop()` (small
   random-arc DOTween hop), and tracks it in `activeGems` as a `GemVisual {Owner, Amount, Gem}`.
3. **`ResolveGems()`** — called once from `PostBattleState` after the battle animation window:
   - `GrantOrphanedPendingXp()` — any `pendingXp` entries that never got a matching `SpawnGem` call
     (e.g. a kill with no on-hit gem spawn) grant XP immediately, no gem, then the list is cleared.
   - `CleanUpDeadOwnerGems()` — for any `activeGems` entry whose owner is null/destroyed or dead,
     destroys the gem GameObject and drops the entry (a creature can die from other damage between
     the kill it participated in and the gem landing).
   - If no gems remain, fires `OnAllGemsCollected` immediately; otherwise `LaunchGemsToOwners()`.
4. **`LaunchGemsToOwners()`** — sends every remaining active gem flying to its owner
   (`ExpirienceGem.FlyTo`), staggered 0.15s apart via `Utils.DoAfterDelay`. On arrival, calls
   `owner.Experience.AddExperience(amount)` and decrements `gemsInFlight`; when it hits zero, fires
   `OnAllGemsCollected`. `activeGems` is cleared immediately (before the flights resolve) since
   ownership of each gem is captured per-closure.

## Instant vs animated resolution (rule 7)

- **`ResolveGems()`** — the animated path described above; used by the normal `PostBattleState`.
- **`ResolveGemsInstant()`** — the rule-7 instant-resolve counterpart. Grants all `pendingXp` and
  all `activeGems`' XP synchronously, destroys any gem GameObjects with no flight animation, no
  waits. Used by `BattleState.ResolveBattleInstant()` (a static method that instant-resolves an
  *entire* battle round — attacks, cleanup, BattleCry-status clearing, and XP — for headless
  automated testing, per the project's stated goal of running whole battles without animation).
  Note the asymmetry: `BattleState` calls `ResolveGemsInstant()` while normal animated play calls
  `ResolveGems()` from `PostBattleState` — these are two different entry points into the same round
  (instant-resolve bypasses `PostBattleState` entirely), not a bug.

## The `Experience` component's leveling model

- `Level` (starts at 1) and `TotalExperience` are both `[SerializeField]`-backed auto-properties,
  visible/tunable in the Inspector per-instance for debugging.
- `AddExperience(amount)` adds to `TotalExperience` then calls `HandlePromotion()`, which asks
  `CreatureSO.GetLevelForXp(TotalExperience)` for the level implied by the new total and promotes
  via `ApplyLevel` only if that's higher than the current `Level` (never demotes).
- `CreatureSO.GetLevelForXp` walks `xpThresholds` (cumulative XP required per level, index 0 =
  level 1 = always 0) from the top down and returns the first level whose threshold is met.
- `ApplyLevel(newLevel)` sets `Level`, re-reads `creature.Data.Stats(Level)` and calls
  `creature.Health.Init(stats.health)` — **leveling up fully heals the creature to its new max HP**,
  it doesn't just raise the ceiling. Then updates the level-text TMP label and fires `OnPromoted`
  (consumed by `StatusesManager` to clear statuses on level-up — see the Battle doc).
- `Promote()` force-advances to `Level + 1` ignoring XP entirely (used by the balance tool).
- `PromoteToLevel(targetLevel)` sets `TotalExperience` directly to that level's threshold and
  promotes — this is the path used when a matching roll (2 or 3 of the same creature card) should
  jump a creature straight to a specific level rather than accumulate XP normally.
- `AddExperienceInstant(amount)` is just `AddExperience(amount)` under another name — kept as a
  named rule-7 entry point even though the underlying call is identical.
- Damage output (`CreatureSO.Stats(level).damage`), attack count, and max health are all indexed by
  `Experience.Level` via `CreatureSO.Stats(int level)`, which clamps to the array bounds — a level
  higher than the configured `levelStats` array just reuses the last entry rather than throwing.

## Restart interaction

- `ExperienceManager` subscribes to the static `GameManager.OnBattleRestart` event in its own
  `Start()` (unsubscribing in `OnDestroy()`) — this is the project-wide convention: any script that
  creates entities or holds battle-scoped state subscribes to this event with its own reset method
  (see `docs/GameLoop.md` for the full mechanism and the current list of subscribers).
- **`ClearForRestart()`** destroys any in-flight gem GameObjects and clears `pendingXp`,
  `activeGems`, and `gemsInFlight` — deliberately *without* granting any XP. This is intentionally
  distinct from `ResolveGemsInstant()`, which grants XP: a restart must discard in-flight rewards,
  not pay them out, since the battle they were earned in no longer exists.
- The gem-flight closures inside `LaunchGemsToOwners()` capture `int generation = GameManager.Instance.Generation`
  at schedule time and check `GameManager.IsStale(generation)` before touching `d.Owner` or calling
  `AddExperience`, both at flight-start and on arrival. This guards a real race: a battle restart can
  happen while a gem from the *previous* battle is still mid-flight (its owner `Creature` may already
  be destroyed by `CreaturesManager.ResetAll()`), and without the guard the arrival callback would
  call `AddExperience` on a creature reset/destroyed after this delay was scheduled. The staleness
  guard itself is documented in full in `docs/GameLoop.md`; this is one concrete call site of it.
- `Utils.DoAfterDelay` (used for both the gem stagger delay and the fly-then-grant callback timing)
  was changed this session from `WaitForSecondsRealtime` to `WaitForSeconds`, so gem-flight timing
  now respects `Time.timeScale` and correctly freezes while the game is paused.

## Gotchas

- The misspelling "Expirience" is consistent across the codebase (`Expirience.cs`'s class is
  correctly named `Experience`, but `ExpirienceGem` and its prefab/folder keep the typo) — don't
  "fix" it in isolation, it'd break asset/prefab references; match the existing spelling for any
  new code that touches gems specifically.
- `SpawnGem` matches pending XP to owner via `FindIndex(p => p.Owner == owner)` — the *first*
  unmatched entry for that owner, not a specific one tied to the exact kill. If a single creature
  participates in multiple kills before any gem lands, the pairing is FIFO by owner, not by which
  kill's projectile is currently landing.
- Dead-owner handling exists in three places with slightly different framing:
  `GrantOrphanedPendingXp`/`ResolveGemsInstant` skip granting XP to a dead owner (XP is simply
  lost), while `CleanUpDeadOwnerGems` destroys the gem outright without granting anything either.
  There's no "redirect XP to the team" fallback — a dead creature's earned XP just evaporates.
- `Level`/`TotalExperience` never persist across a restart via any explicit reset — they don't need
  to, since `CreaturesManager.ResetAll()` destroys every creature GameObject outright on restart
  (see `docs/GameLoop.md`), so there's no lingering `Experience` component to reset in place.
