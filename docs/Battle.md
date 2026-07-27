# Battle

Combat between the two sides' summoned creatures/heroes: the unit class hierarchy, health/damage/
death, the Shield construct, and the attack-resolution algorithm that `BattleState` runs once per
side's turn (after round 1).

## Key files

- `Assets/Game/_Scripts/Units/Targetable.cs` — base class for anything attackable.
- `Assets/Game/_Scripts/Units/Unit.cs` — abstract layer adding animator + statuses (Hero/Creature).
- `Assets/Game/_Scripts/Units/Health.cs` — damage/heal/death component, one per Targetable.
- `Assets/Game/_Scripts/Units/Shield.cs` — the Shield-spell construct (Targetable, not a Unit).
- `Assets/Game/_Scripts/Units/Hero.cs`, `Assets/Game/_Scripts/Units/Creature.cs` — the two `Unit`s.
- `Assets/Game/_Scripts/Units/StatusesManager.cs` — per-unit status flags (shocked, charmed, BattleCry).
- `Assets/Game/_Scripts/Global/GameManager/BattleState.cs` — the `GameState` that runs one battle.
- `Assets/Game/_Scripts/Global/GameManager/AttacksResolver.cs` + `AttacksResolver.Mechanics.cs` —
  pure-data attack planner (partial class split: public API / internal mechanics).
- `Assets/Game/_Scripts/PlayerView/CreaturesManager.cs` — owns a side's creature slots.
- `Assets/Game/_Scripts/PlayerView/HeroView.cs` — owns a side's Hero + Shield slot.
- `Assets/Game/_Scripts/PlayerView/UnitSlot.cs` — a placement slot; `Unit` (a `Targetable` ref) and
  `Creature` (typed convenience cast) are the same backing field.

## Class hierarchy

```
Targetable (MonoBehaviour, [RequireComponent(Health)])
├── Unit (abstract, [RequireComponent(StatusesManager)], adds Animator + StatusesManager)
│   ├── Hero      ([RequireComponent(HeroAnimator)])
│   └── Creature  ([RequireComponent(CreatureAnimator, Experience)])
└── Shield (Targetable directly — no Animator, no StatusesManager, no Experience)
```

- **`Targetable`** (`Targetable.cs:14`): owns `Health` (via `[RequireComponent]`), a `HitFeedback`
  child (via `GetComponentInChildren`, mandatory — throws if missing per rule 5), and a `UnitSlot`
  reference. Provides the shared lifecycle: `OnSummon()` (plays `onSummonFeedback`), `HandleDeath()`
  (virtual, plays `onDeathFeedback`; subscribed to `Health.onDeath` in `Start()`), and `DestroyUnit()`
  (clears its slot, plays `onBodyCleanUpFeedback` if assigned, then `Destroy(gameObject)` — either
  immediately or once that feedback's `OnComplete` fires).
- **`Unit`** (`Unit.cs:8`): abstract; adds `Animator` (`UnitAnimator`) and `StatusesManager`, both
  cached in `Awake()`. Declares `abstract float GetMaxHealth()` and exposes `InitHealth()` →
  `Health.Init(GetMaxHealth())`. This is also the method every restart hook calls to heal a Hero back
  to full (see Restart interaction below).
- **`Hero`** (`Hero.cs:9`): `GetMaxHealth()` returns `Data.health` (`HeroSO`). Fires the static
  `event Action<Hero> OnHeroDied` from `Health.onDeath`, which `GameManager.HandleHeroDied` listens to
  in order to end the round loop and enter `GameOverState` (see `docs/GameLoop.md`).
- **`Creature`** (`Creature.cs:9`): `GetMaxHealth()` reads `Data.Stats(Experience.Level).health` —
  stats scale with the creature's `Experience.Level` (see `docs/Experience.md`). `DestroyCreature()`
  is just a named wrapper over `DestroyUnit()`. Overrides `HandleDeath()` to also splash damage its
  own side's hero — see "Owner-hero splash damage on creature death" below.
- **`Shield`** (`Shield.cs:10`): does **not** extend `Unit` — no animator/statuses/XP. `Init(level)`
  sets HP for that level and plays the summon animation (called once right after instantiation, from
  `ShieldResolver` — see `docs/ActionsAndSpells.md`); `Promote(level)` bumps level and fully refills
  HP when a higher shield level is rolled while one is already up. `HandleDeath()` override frees its
  `Slot.Unit` immediately (so a new shield can be rolled next turn) then plays a scale-out tween that
  destroys the GameObject on completion — so `DestroyUnit()` is overridden to a no-op (the tween
  already handles destruction; the standard cleanup path never fires for Shields).

`UnitSlot.Unit` (`Targetable`, settable) is the single backing field; `UnitSlot.Creature` is a typed
`get`/`set` convenience cast over the same field.

## Health

`Health.cs` is a standalone `MonoBehaviour` (not part of the `Targetable` hierarchy itself, but every
`Targetable` requires one via `[RequireComponent]`).

- `Init(maxHp)` sets `maxHealth`/`currentHealth` to full and refreshes the (optional) `healthBar`.
  Shields and other bar-less Targetables just leave `healthBar` unassigned.
- `TakeDamage(damage)` clamps `currentHealth` to `[0, maxHealth]`, updates the bar, then either fires
  `onDamageTaken` (bar pops visible) or, if now dead, `onDeath` — **unless `PostponeDeath` is true**,
  in which case it just sets an internal `deathPostponed` flag and hides the bar without firing
  `onDeath` yet.
- **`PostponeDeath` / `ExecutePostponedDeath()`**: lets an attacker (Tank) finish its attack animation
  before the target's death plays. Something later calls `ExecutePostponedDeath()` (once the attack
  animation resolves) to actually fire the deferred `onDeath`. `Health.Start()` itself subscribes
  `onDeath` to play the animator's `PlayDead()` and hide the bar; `Targetable.Start()` separately
  subscribes `onDeath → HandleDeath()`.
- `Heal(amount)` is a no-op if already dead; otherwise clamps up to `maxHealth`, fires `onHealed`,
  plays `healFeedback`.
- `IsDead()` is simply `currentHealth <= 0`.
- `MaxHealth` (public getter) exposes the value `Init()` set — used by
  `Creature.HandleDeath()`'s owner-hero splash damage (below), which needs the creature's max HP,
  not its current/overkill HP.

## Owner-hero splash damage on creature death

A Robotek-style mechanic: whenever a creature dies (from any cause — combat, a future nuke/DOT —
not just melee/ranged combat), its own side's hero takes splash damage equal to 20% of that
creature's max HP, rounded down. `Creature.cs`:

```csharp
public Hero OwnerHero => G.PlayerCreaturesManager.GetAllCreatures().Contains(this) ? G.PlayerHero : G.EnemyHero;

private const float OwnerHeroDamageFraction = 0.2f;

protected override void HandleDeath()
{
    base.HandleDeath();
    OwnerHero.Health.TakeDamage(Mathf.Floor(Health.MaxHealth * OwnerHeroDamageFraction));
}
```

- **Hooks `Targetable.HandleDeath()`** (already `virtual`, already the single place `Health.onDeath`
  routes through) rather than `AttacksResolver`/`BattleState` — so it fires for a creature death from
  any cause, symmetrically for both sides, with no changes needed to the attack-resolution code.
- **`OwnerHero` is computed live, not cached at spawn** (CLAUDE.md rule 22) — `HeroView.ReplaceHeroAvatar()`
  (`docs/Encounters.md`) destroys and replaces the enemy `Hero` between encounters, so a field cached
  at spawn time would need its own invalidation; a live lookup through `G` never goes stale. The
  `Contains` scan is over at most ~9 slots and only runs on death, not per-frame.
- **Uses `Health.MaxHealth`, not remaining/overkill HP** — the splash amount is the same whether the
  creature died at 1 HP or was overkilled by 50, matching the design intent directly.
- **Fires once per real death** — `Health.onDeath` (and therefore `HandleDeath()`) only fires on the
  transition into death, and `AttacksResolver.Resolve()`'s simulated-HP tracking already prevents a
  single `Resolve()` call from hitting an already-dead-in-simulation target again — so this can't
  double-fire within one battle resolution.

## Shield mechanics

- Summoned into `HeroView.ShieldSlot` by the Shield spell (see `docs/ActionsAndSpells.md`).
- **Highest-priority target**: `AttacksResolver.GetHighestPriorityAliveTarget` (below) always returns
  a living shield before considering any creature or hero.
- **Grants no XP**: `AttacksResolver.RegisterExperienceRewards` explicitly skips any attack whose
  `Target is Shield`; `AttacksResolver.BuildHitInfos` also excludes Shield targets from
  `shouldSpawnGem` even if the shield is the "doomed" target that round.
- `HeroView.Shield` is a computed property (`shieldSlot.Unit as Shield`); `HeroView.ClearShield()`
  (added for battle restart) destroys it and nulls the slot directly, bypassing the death tween.

## BattleState combat resolution

`BattleState.cs` is a `GameState` (see `docs/GameLoop.md` for the state-machine contract). It has two
paths that must always agree on outcome (rule 7):

- **Animated path** — `OnEnter()` schedules `BeginBattle()` via `Utils.DoAfterDelay.Execute(_, 0f)`.
  `BeginBattle()` early-outs to `CompleteState()` if both sides have no creatures and no living hero.
  Otherwise it builds an `AttacksResolver`, calls `Resolve(...)`, then `ExecuteAttacks(null)` →
  `ExecuteAnimations` (plays the actual attack animations) and schedules `CompleteState` after
  `maxDuration + 1f` seconds.
- **Instant path** — the static `ResolveBattleInstant()` builds and resolves the same
  `AttacksResolver`, calls `ApplyAttacksInstant()` (applies all damage with no animation), cleans up
  dead creatures on both sides, clears BattleCry statuses (`PostBattleState.ClearBattleCryStatuses`),
  and resolves gems instantly (`ExperienceManager.Instance.ResolveGemsInstant()`).

### `AttacksResolver` — the pure-data attack planner

Split across `AttacksResolver.cs` (public API: `AttackAssignment` struct, `Resolve`, `ExecuteAttacks`,
`ApplyAttacksInstant`) and `AttacksResolver.Mechanics.cs` (targeting/priority/animation-grouping
internals) as one `partial class`.

1. **`Resolve(playerCreatures, enemyCreatures, playerHero, enemyHero, playerShield, enemyShield)`**:
   builds a simulated-HP dictionary per side (`BuildSimulatedHP` — snapshots current HP for every
   creature + living hero + living shield) so the whole round can be planned up front without
   mutating real `Health` yet. Calls `ResolveTeam` for both directions, concatenates into `AllAttacks`,
   computes `DoomedTargets` (any target hit by at least one `FatalBlow` assignment), then
   `RegisterExperienceRewards()`.
2. **Targeting priority** (`GetHighestPriorityAliveTarget`): enemy shield (if alive) → highest-priority
   living creature → enemy hero as fallback. Creature priority order is fixed:
   `Mage (0) → Archer (1) → Tank (2)` (lower number attacks/is-preferred-target first — used both to
   decide attacker order via `SortByPriority` and as the tie-break for `OrderBy(GetPriority)` when
   picking a target).
3. **Per-attacker resolution** (`ResolveTeam`): attackers are sorted by priority; a `StatusesManager.
   IsShocked` creature skips its turn entirely; damage is `stats.damage * AttackDamageMultiplier`
   (BattleCry buff/debuff) — if that multiplier reduces damage to `≤ 0` (BattleCry "Energy Drain"),
   the attacker skips its turn too. Each attacker fires `stats.numberOfAttacks` hits, re-picking a
   target each hit against the live simulated HP (so multi-hit archers can finish off a target and
   move to the next).
4. **XP registration** (`RegisterExperienceRewards`, called inside `Resolve` — i.e. XP is registered
   before any animation plays): Shield targets never grant XP. Hero hits grant `damage * 10` XP per
   shot regardless of death. Creature targets grant `CreatureSO.GetExperienceReward(level)` XP once
   per (attacker, target) pair, only if that creature is in `DoomedTargets` (i.e. only on the kill).
   XP is registered via `ExperienceManager.Instance.RegisterPendingXp(attacker, xp)` — see
   `docs/Experience.md` for what happens after that (gem spawn/flight/instant-resolve).
5. **Animation grouping** (`ExecuteAnimations`, `BuildHitInfos`): groups assignments by attacker
   (multi-hit archers get one animation call with multiple `HitInfo`s). Detects a **mutual tank fight**
   (both tanks targeting each other) and choreographs it specially — tank A attacks, tank B's own
   attack is scheduled to start once A's `arriveTime` (`GetRangedDelay() + GetRunDuration()`) elapses.
   Remaining tanks sharing the same target are **chained** (`ExecuteTankChain`) so they take turns
   instead of piling onto one melee position simultaneously — each waits for the previous tank's
   `OnLeapBackStarted` event. All non-tank attackers (ranged) fire simultaneously from their slots.
   Each `HitInfo.OnHit` callback is what actually calls `target.Health.TakeDamage(damage)` and —if
   `shouldSpawnGem`— `ExperienceManager.Instance.SpawnGem(...)`, i.e. real damage/XP only lands when
   the animation's hit callback fires, not when `Resolve()` planned it.

## StatusesManager

One per `Unit` (`[RequireComponent]` on the `Unit` base, rule 14). Owns:

- **`IsShocked`** — creature skips its combat turn entirely (checked in `ResolveTeam`); toggled via
  `ApplyShock`/`ClearShock`, fires `OnShockedChanged`.
- **`IsCharmed`** — true while a creature is stolen from its home side via the Charm spell; a simple
  toggle (charming an already-charmed creature steals it back, per `CharmShot.Apply`). Not cleared by
  `ClearAllStatuses` — persists through heals/promotions; only cleared on death.
- **`AttackDamageMultiplier`** — BattleCry buff/debuff multiplier (default `1f`), read directly by
  `AttacksResolver.ResolveTeam` above. Set via `ApplyBattleCryBuff`/`ApplyBattleCryDebuff`, cleared
  only by `ClearBattleCry()` (called explicitly from `PostBattleState.ClearBattleCryStatuses`, not by
  `ClearAllStatuses`) — the effect is meant to last the whole battle regardless of heals/promotions.
- `ClearAllStatuses()` (currently just clears `IsShocked`) runs on `Health.onHealed`, `Health.onDeath`,
  and `Experience.OnPromoted` (creatures only — a level-up clears statuses with no extra plumbing).

## Restart interaction

Per the project's battle-restart convention (see `docs/GameLoop.md` for the full mechanism): any
script that creates entities or holds battle-scoped state subscribes to the static
`Game._Scripts.Global.GameManager.OnBattleRestart` event in its own `Start()` (unsubscribing in
`OnDestroy()`) with a reset method. In this system:

- **`Hero.Start()`** subscribes `GameManager.OnBattleRestart += InitHealth;` — restart heals the hero
  back to full HP (no death/damage state carries over).
- **`HeroView.Start()`** subscribes `GameManager.OnBattleRestart += ClearShield;` — restart destroys
  any active Shield and clears the slot directly (bypassing the death tween — consistent with rule 7's
  spirit: an instant reset path shouldn't wait on animation).
- **`CreaturesManager.Start()`** subscribes `GameManager.OnBattleRestart += ResetAll;` — destroys every
  creature in every native + charm slot instantly, no death animation (`Assets/Game/_Scripts/
  PlayerView/CreaturesManager.cs`).

If you add new Battle-owned state (e.g. a new per-unit buff, a new construct like Shield, a new
manager holding live references to units), **wire it into `OnBattleRestart` the same way** — see the
rule in `CLAUDE.md`.

## Gotchas

- `AttacksResolver.Resolve()` plans and **registers XP before any animation plays** — the plan is
  fixed up front against simulated HP; the two resolve paths (animated vs. instant) both call the
  exact same `Resolve()`, so outcomes are guaranteed identical, only the *when* of `TakeDamage` differs
  (per-hit animation callback vs. immediate loop in `ApplyAttacksInstant`).
- Damage numbers, attack counts, and creature priority all come from `CreatureSO`/`HeroSO`
  (`Data.Stats(level)`), never from view/animation MonoBehaviours — data/view separation (rule 13).
- `PostponeDeath` is set/cleared per-attack context (not shown in this doc's file list — check the
  Tank attack path/`TankAnimator` if you need the exact toggle site) so a target doesn't play its
  death animation mid-attack-animation; if you add a new attacker type that can kill instantly, check
  whether it needs the same postponement.
- Shield and Hero are both fallback-only targets in `GetHighestPriorityAliveTarget` — a Hero is never
  targeted while any of its side's creatures are alive; a Shield always wins over both while alive.
