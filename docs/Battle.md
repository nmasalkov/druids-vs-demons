# Battle

Combat between the two sides' summoned creatures/heroes: the unit class hierarchy, health/damage/
death, the Shield construct, and the attack-resolution algorithm that `BattleState` runs once per
side's turn (after round 1).

## Key files

- `Assets/Game/_Scripts/_Units/Targetable.cs` — base class for anything attackable.
- `Assets/Game/_Scripts/_Units/Unit.cs` — abstract layer adding animator + statuses (Hero/Creature).
- `Assets/Game/_Scripts/_Units/Health.cs` — damage/heal/death component, one per Targetable.
- `Assets/Game/_Scripts/_Units/Shield.cs` — the Shield-spell construct (Targetable, not a Unit).
- `Assets/Game/_Scripts/_Units/Hero.cs`, `Assets/Game/_Scripts/_Units/Creature.cs` — the two `Unit`s.
- `Assets/Game/_Scripts/_Units/StatusesManager.cs` — per-unit status flags (shocked, charmed, BattleCry).
- `Assets/Game/_Scripts/_Units/HitFeedback.cs` + `Assets/Game/_Prefabs/_Feedbacks/HitFeedback.prefab` —
  every Targetable's hit anchor and target-side hit feedbacks (per-hit Unity Events feedback, e.g.
  the boss's Hurt; crit camera shake) — see "Hit feedbacks" below.
- `Assets/Game/_Scripts/_ScriptableObjects/_Modifiers/SpecialDamageModifierSO.cs` +
  `DamageContext.cs` + `ShieldBreakerModifierSO.cs` — per-creature custom damage rules (below).
- `Assets/Game/_Scripts/_Global/_GameManager/BattleState.cs` — the `GameState` that runs one battle.
- `Assets/Game/_Scripts/_Global/_GameManager/AttacksResolver.cs` + `AttacksResolver.Mechanics.cs` —
  pure-data attack planner (partial class split: public API / internal mechanics).
- `Assets/Game/_Scripts/_ScriptableObjects/CounterTankSO.cs` +
  `Assets/Game/_Scripts/_Units/_Animators/CounterTankAnimator.cs` — the non-attacking,
  counterattacking tank (Bulba). See "Counterattacks" below.
- `Assets/Game/_Scripts/_ScriptableObjects/HealingArcherSO.cs` +
  `Assets/Game/_Scripts/_Global/_GameManager/AttacksResolver.Healing.cs` +
  `Assets/Game/_Scripts/_Units/_Animators/SplitShotArcherAnimator.cs` — the archer whose shots also
  heal allies (HealingShroom). See "Healing shots" below.
- `Assets/Game/_Scripts/_PlayerView/CreaturesManager.cs` — owns a side's creature slots.
- `Assets/Game/_Scripts/_PlayerView/HeroView.cs` — owns a side's Hero + Shield slot.
- `Assets/Game/_Scripts/_PlayerView/UnitSlot.cs` — a placement slot; `Unit` (a `Targetable` ref) and
  `Creature` (typed convenience cast) are the same backing field.

## Class hierarchy

```
Targetable (MonoBehaviour, [RequireComponent(Health)])
├── Unit (abstract, [RequireComponent(StatusesManager)], adds Animator + StatusesManager)
│   ├── Hero      ([RequireComponent(HeroAnimator)] — BossAnimator subclass for Animator-driven avatars, docs/AnimationAPI.md)
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
- **Skipped for a scripted board wipe.** `Creature.KillWithoutOwnerSplash()` sets a flag that
  `HandleDeath()` checks before dealing the splash; `CreaturesManager.KillAll()` kills a whole side
  through it. Death animations and feedbacks play exactly as normal — only the splash is skipped,
  because this rule models a creature lost *in combat*, not one removed by a cutscene. The one user
  today is the boss phase transition (`docs/GameLoop.md`), where charging it would be actively harmful:
  the enemy hero is already dead, so its creatures' splash would re-fire `Hero.OnHeroDied` mid-swap,
  and the player would otherwise take ~60% of their board's max HP for something they didn't do —
  enough to die inside the transition. Contrast `CreaturesManager.ResetAll()`, which destroys instantly
  with no death at all and remains the battle-restart path.

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
internals) as one `partial class`. `AttacksResolver.Mechanics.cs` also carries the **firepower
estimation** trio, in its own section at the bottom of the file:

- `public static float EstimateFirepower(bool isPlayerSide)` — pre-battle total-damage-output
  estimate, mirroring `ResolveTeam`'s exact per-attacker formula below (`stats.damage *
  AttackDamageMultiplier * ExpectedCritMultiplier`, `numberOfAttacks` hits, shocked creatures
  contribute 0) without the target/simulated-HP bookkeeping, since it's a total-output estimate
  rather than a resolved plan. Crits count at their average value — see "Critical strike" below.
  Per-hit damage is `CreatureSO.EstimatedDamage(stats)`: the real `damage`, or
  `CreatureStats.nominalDamage` when `damage` is 0. That fallback lets a non-attacker (a counter-tank
  today, healers later) still count toward its side's firepower. Bulba's nominal is Golem's 4/6/8/10.
  Real combat never reads `nominalDamage`.
- `public static float EstimateEffectiveFirepower(bool isPlayerSide)` — the above **minus the
  opposing barrier**: the HP of the `Shield` in the *other* side's `HeroView.ShieldSlot`, i.e. the
  one standing in this side's way. Floored at 0 via `Mathf.Max` — "can't get through the barrier at
  all" is as bad as it gets, so an oversized Shield can never inflate the opposing side's advantage
  past its own firepower. `HeroView.Shield` is genuinely optional (empty slot reads null), so the
  private `OpposingShieldHp` helper null-checks it — allowed under rule 5, same as
  `HeroView.ClearShield` already does.
- `public static float OpponentFirepowerAdvantage(bool isPlayerSide)` — how far the **opponent** of
  the given side leads on effective firepower; positive means that side is falling behind.

This was previously an `AttacksResolver.Debug.cs` (rule 20) file, marked debug-only. It isn't
anymore: `OpponentFirepowerAdvantage` is a real gameplay input with **two** consumers, both reading it
every turn —

- `SlotMachineRigger` evaluates `FightSO`'s Comeback Settings against it (see `docs/SlotMachine.md`).
- `RerollBudgetDecision` discounts the enemy hero's current HP by the player's lead, so an out-gunned
  AI spends its reroll pool as freely as a damaged one (see `docs/AI.md`, exposed to `_AI` code as
  `AIController.PlayerFirepowerLead`).

Both read it live off the same board state at the start of the acting side's turn, so the two can
never disagree about who's ahead. `BalanceTool`'s "compared firepower" HUD toggle reads
`EstimateEffectiveFirepower` too, so the HUD matches what both systems act on.

**The HUD's readout is self-disabling, and deliberately so.** `BalanceTool.DrawFirepowers()` runs every
frame while `Show Firepowers` is checked, which makes it the one place a scene-setup bug behind this
estimate (an unwired `HeroView` on `G`, a missing `CreaturesManager`) gets re-reported ~60 times a
second. Rules 1 and 5 say that null must still throw and be seen — so the first failure is logged in
full, with its real stack trace, and then the readout unchecks `ShowFirepowers` on itself and prints a
follow-up line saying so. The bug stays impossible to miss; it just can't evict every other message
from the console first. This was caught live: one unwired reference filled the entire Unity log buffer
with 41 identical `NullReferenceException`s from this single line and nothing else survived in it.
Re-check the toggle after fixing the reference. The guard is confined to this debug HUD — nothing in
gameplay catches around a firepower read.

1. **`Resolve(playerCreatures, enemyCreatures, playerHero, enemyHero, playerShield, enemyShield)`**:
   builds a simulated-HP dictionary per side (`BuildSimulatedHP` — snapshots current HP for every
   creature + living hero + living shield) so the whole round can be planned up front without
   mutating real `Health` yet. Calls `ResolveTeam` for both directions, concatenates into `AllAttacks`,
   computes `DoomedTargets` (any target hit by at least one `FatalBlow` assignment), then
   `RegisterExperienceRewards()`.
2. **Targeting priority** (`GetHighestPriorityAliveTarget`): enemy shield (if alive) → highest-priority
   living creature → enemy hero as fallback. Creature priority order is fixed:
   `Mage (0) → Archer (1) → Tank (2)`, matched with `is` so SO subclasses like `CounterTankSO` keep
   their class's priority. Lower numbers attack first (`SortByPriority`) and are targeted first.
   **Taunt:** a creature whose `CreatureSO.preferredTarget` is true is targeted before every
   non-preferred creature, still behind a standing shield. `GetTargetPriority` subtracts 100 from
   its class priority, so several preferred creatures keep the class order among themselves. This
   only affects target picking, not attacker order. Nukes ignore it (`NukeResolver.BuildPriorityTargets`
   keeps its own Mage → Tank → Archer order). Bulba is the only user: it draws exactly the hits it
   counters (see "Counterattacks").
3. **Per-attacker resolution** (`ResolveTeam`): attackers are sorted by priority; a `StatusesManager.
   IsShocked` creature skips its turn entirely; damage is `stats.damage` (times the player-only
   creature-class reward boost, `RewardBonuses.ApplyCreatureBonus` — `docs/Rewards.md`)
   `* AttackDamageMultiplier` (BattleCry buff/debuff) — if that multiplier reduces damage to `≤ 0` (BattleCry "Energy Drain"),
   the attacker skips its turn too. Each attacker fires `stats.numberOfAttacks` hits, re-picking a
   target each hit against the live simulated HP (so multi-hit archers can finish off a target and
   move to the next). Once each hit's target is known, `ApplySpecialModifiers` runs the attacker's
   own `CreatureSO.specialDamageModifiers` over that value — see "Special damage modifiers" below —
   and then, always last, the hit rolls for a critical strike (`AttackAssignment.IsCritical`) — see
   "Critical strike" below.
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
   `OnLeapBackStarted` event. **Shield before hero**: chains on a `Shield` start first, and a chain
   whose target is the hero *behind* that shield (`_guardingShields`, filled in `Resolve()`) has its
   head wait on the shield chain's last tank's `OnLeapBackStarted` (`FindShieldBreakerChain`) — so
   when one tank breaks a weak shield and a second tank goes for the hero, the second visibly waits for
   the break instead of running through the still-standing shield. A tank can only be assigned the
   hero while its shield is up if that shield is doomed earlier in the same resolve, so any tank chain
   on it is the breaker. Ranged breakers (a mage/archer breaking the shield) don't hold a tank back.
   All non-tank attackers (ranged) fire simultaneously from their slots.
   Each `HitInfo.OnHit` callback is what actually calls `target.Health.TakeDamage(damage)`, then
   `target.HitFeedback.PlayHitFeedbacks(isCritical)` (see "Hit feedbacks" below) and —if
   `shouldSpawnGem`— `ExperienceManager.Instance.SpawnGem(...)`, i.e. real damage/XP only lands when
   the animation's hit callback fires, not when `Resolve()` planned it.

## Special damage modifiers

A per-creature hook for custom damage rules ("this creature hits shields harder", "…deals less to
tanks", …) that would otherwise have to be special-cased inside `AttacksResolver`. Three files, all
under `Assets/Game/_Scripts/_ScriptableObjects/_Modifiers/`:

- **`SpecialDamageModifierSO`** — abstract `ScriptableObject` with one method,
  `float ModifyDamage(in DamageContext context, float damage)`. The logic lives on the SO itself,
  the same shape as `RewardSO.Claim`/`IsOwned` (`docs/Rewards.md`) and `ActionSO.CreateAndResolve`.
- **`DamageContext`** — `readonly struct { Creature Attacker; Targetable Target; }`, mirroring
  `ActionContext`. Passed by `in`, so the chain allocates nothing; new fields can be added later
  without touching existing modifier subclasses.
- **`ShieldBreakerModifierSO`** — the first concrete rule:
  `context.Target is Shield ? damage * shieldDamageMultiplier : damage`, with
  `shieldDamageMultiplier = 1.5` on the `ShieldBreaker.asset`. Carried by the ork roster
  (`OrkMage`/`OrkTank`/`Kodo`), matching their shield-breaking `OrkBreaker` hero avatar.

`CreatureSO.specialDamageModifiers` is a plain `SpecialDamageModifierSO[]`, empty for most creatures.
Assets live in `Assets/Game/_ScriptableObjects/_Modifiers/`.

**Where it runs.** `AttacksResolver.ApplySpecialModifiers` (`AttacksResolver.Mechanics.cs`), called
from `ResolveTeam` **inside the per-hit loop**, after the BattleCry multiplier and after the target
for that hit has been picked:

```csharp
float buffedDamage = stats.damage * attacker.StatusesManager.AttackDamageMultiplier;
if (buffedDamage <= 0f) continue;          // BattleCry Energy Drain — skip turn
...
var target = GetHighestPriorityAliveTarget(...);
float dmgPerHit = ApplySpecialModifiers(attacker, target, buffedDamage);
```

Three things about that placement are load-bearing:

- **Per hit, not per attacker.** A multi-hit archer re-picks its target every hit, so it can hit a
  Shield on hit 1 and a Creature on hit 2. Hoisting the call out of the loop (where the old
  per-attacker `dmgPerHit` used to be computed) would silently apply hit 1's modifier to every
  later hit.
- **After the Energy Drain guard, never before.** The skip-turn check stays on `buffedDamage`, so a
  modifier can't resurrect a turn BattleCry zeroed out.
- **Inside `Resolve()`, so both resolve paths get it for free.** `ResolveTeam` is the single producer
  of `AttackAssignment.Damage`, and that one value feeds the animated path (`BuildHitInfos` →
  `HitInfo.OnHit` → `TakeDamage`) and the instant path (`ApplyAttacksInstant`) alike. There is no
  second copy of the formula to keep in sync — rule 7 parity holds by construction.

**Chained, multiplicative stacking.** Each modifier takes the running value and returns the next, in
array order, so several compound. The bonus multiplies the already-BattleCry-adjusted damage: Kodo at
level 1 (3 dmg) under a ×2 BattleCry buff hits a shield for `3 × 2 × 1.5 = 9`. A critical strike, if
rolled, multiplies after the whole chain — see "Critical strike" below.

**Not persisted, so no `id` and no catalog entry** — unlike `ActionSO`/`RewardSO` (rule 23), these are
referenced directly from `CreatureSO` assets as real Unity asset references, never stored in
`RunState`, so they never make a `JsonUtility` round-trip.

**`EstimateFirepower` deliberately excludes them** (`AttacksResolver.Mechanics.cs`) — unlike crits,
which it includes at their expected value. It's a pre-battle
total-output estimate with no targets picked yet, so a target-dependent modifier structurally can't
apply — a shield-breaking roster reads its plain firepower in BalanceTool's HUD and then hits harder
than the HUD says once a Shield is actually up. Worth knowing that this understatement now also
reaches gameplay, not just the HUD: a ShieldBreaker roster's `OpponentFirepowerAdvantage` reads lower
than its real threat, so it earns its opponent slightly less comeback assistance than it should
(see `docs/SlotMachine.md`).

**Adding a new rule:** subclass `SpecialDamageModifierSO`, implement `ModifyDamage`, add a
`[CreateAssetMenu(... menuName = "Game/Modifiers/…")]`, create the asset under
`_ScriptableObjects/_Modifiers/`, and drop it into the relevant creatures' `specialDamageModifiers`.
`AttacksResolver` never changes.

## Critical strike

Every creature can crit. Two plain fields on `CreatureSO` (not a `SpecialDamageModifierSO` — every
creature has them, and they must always run last, which an array-ordered modifier can't guarantee):

- `critChancePercent` (`[Range(0,100)]` int) — chance each individual hit is a crit.
- `critDamageBonusPercent` (`[Min(0)]` int) — extra damage on a crit: `50` = ×1.5, `100` = ×2.
  Exposed as `CreatureSO.CritDamageMultiplier` (`1 + bonus/100`).

Both default to 0, so a creature without authored values never crits. Crits are currently on the ghost
roster (`GhostArcher`/`GhostTank`/`GhostMage`, 20% for +50%) and the player reward mage `Lizard`
(35% for +100%).

**Where it runs.** `ResolveTeam`, inside the per-hit loop, right after `ApplySpecialModifiers`:

```csharp
float modifiedDamage = ApplySpecialModifiers(attacker, target, buffedDamage);
bool isCritical = RollsCritical(attacker.Data);
float dmgPerHit = isCritical ? modifiedDamage * attacker.Data.CritDamageMultiplier : modifiedDamage;
```

- **Always the last multiplier** — on top of BattleCry and every special modifier. A 3-damage hit
  under a ×2 BattleCry, from a ShieldBreaker (×1.5) creature with +50% crit, against a shield, crits
  for `3 × 2 × 1.5 × 1.5 = 13.5`.
- **Rolled per hit** — a multi-hit archer can crit some hits and not others.
- **Rolled inside `Resolve()`**, so the result is baked into `AttackAssignment.Damage`/`IsCritical`
  and the animated and instant paths land identical damage (rule 7), same as special modifiers.
- `RollsCritical` returns `false` without touching `UnityEngine.Random` when the chance is 0, so the
  mechanic doesn't shift any other system's RNG sequence for the (majority) crit-less creatures.
  Otherwise it uses `AIController.RollForProbability`.
- XP for hero hits (`damage * 10`) scales with crit damage automatically.

**Firepower estimate includes crits at their expected value** —
`CreatureSO.ExpectedCritMultiplier` (`1 + chance × bonus`, e.g. 1.1 for 20% × +50%). Unlike special
modifiers, crits don't depend on the target, so the average is knowable up front without rolling.
This keeps `OpponentFirepowerAdvantage` (the comeback-rigging input, `docs/SlotMachine.md`) and
BalanceTool's HUD honest about a crit roster's real threat.

## Counterattacks

A creature whose data is a **`CounterTankSO`** (`TankSO` subclass, `_ScriptableObjects/CounterTankSO.cs`)
never attacks on its own turn — its `levelStats.damage` is 0, so `ResolveTeam`'s `buffedDamage <= 0`
guard skips it. Neither the tank class reward boost nor a BattleCry buff can lift it, since both
multiply. Instead, every regular creature hit that lands on it is answered with a ranged counter
for a flat `counterDamageFractions[level]` of that hit's damage — aimed at the Shield guarding the
attacker's side while that shield is up, else at the attacker. Bulba (`id: bulba`, the tank reward creature)
is the first user: 35/38/41/45% by level. Bulba also has `preferredTarget` set, so enemy creature
attacks hit it first (see "Targeting priority" above).

**Resolution: a post-pass, `ResolveCounters`** (`AttacksResolver.Mechanics.cs`), run in `Resolve()`
right after both `ResolveTeam` passes, before `BuildDoomedTargets`/`RegisterExperienceRewards`:

- **Target (`CounterTarget`).** The Shield guarding the attacker's side if it is still alive in the
  simulated HP after all regular attacks, else the attacker. A shield the tank's own side already
  breaks this battle isn't targeted, so the counter goes to the attacker instead.
- It walks the regular assignments in order. A hit on a counter-tank gets a counter unless one of the
  following holds:
  - the tank is shocked;
  - **the counter's target is already dead in the simulated HP after all regular attacks** (it would
    die anyway, so the counter isn't needed and isn't animated). The same check drops the 2nd and 3rd
    counters against a multi-hit archer that an earlier counter already killed.
- **The killing blow is still countered.** Whether the hit kills the tank is never checked.
- **Damage is flat:** `hit.Damage` (final, after the attacker's own boosts, BattleCry and crit) ×
  fraction, nothing else. The tank's own class reward boost, BattleCry/Energy Drain, special modifiers
  and crit never touch the counter, so a BattleCry on the attacker's side is what makes it bigger.
- **Storage.** Each counter is an ordinary `AttackAssignment` (`Attacker` = the tank, `Target` = the
  hitter or its shield, `IsCounter = true`), appended to `AllAttacks` and to `CounterAttacks`. It also
  decrements the target's simulated HP and sets `FatalBlow`. A counter on a shield gives no XP or gem,
  like any shield hit. Because it's a regular entry, XP (kill reward to the
  tank), doomed targets and `ApplyAttacksInstant` all handle it with no special code (rule 7 parity).
  The trigger is linked through `HasCounter`/`CounterIndex`.
- **Nukes never trigger counters.** They don't go through `AttacksResolver`.

**Animation — `CounterTankAnimator`** (a `TankAnimator` subclass holding a `MissileAnimator`):

- `ExecuteAnimations` skips `IsCounter` entries when grouping by attacker, so the tank never runs a
  melee turn. The counter instead plays from its trigger's `OnHit` (`BuildOnHit`), in this order:
  1. `BeginCounter()`, which raises a pending count and sets `Health.PostponeDeath`;
  2. `TakeDamage`;
  3. hit feedbacks and gem;
  4. `PlayCounter(attacker, counterOnHit)`, which plays Attack and fires the missile after `fireDelay`.
- **Postponed death.** At launch the pending count drops. At zero, the tank either runs
  `ExecutePostponedDeath()` (if it's dead) or clears the postpone flag. So a fatal hit still gets its
  counter launched before the tank dies.
- **Melee attackers.** One that is hit while standing in the tank's face keeps its own
  `TankAnimator` death postponement, so it dies after leaping back.
- **Timing.** `ExecuteAnimations` adds the longest `GetCounterDuration()` (`fireDelay +
  flightAllowance`) to the battle's end delay.
- **Gems.** Counter gems de-dup through one resolver-wide set (`_counterGemSpawned`).

## Healing shots

A creature whose data is a **`HealingArcherSO`** (`ArcherSO` subclass) **shoots twice per battle**:
1. **A support volley in the battle's pre-phase.** Its `numberOfAttacks` shots each heal one ally for
   `healFractions[level]` × its `BuffedDamage`.
2. **Its regular attack, like any archer**, in the regular phase.

The heal base, `BuffedDamage`, is the class reward boost × BattleCry, before crits and special
modifiers. So the archer boost and BattleCry scale healing too, and Shock or Energy Drain (0 damage)
cancel it. HealingShroom (`id: healing_shroom`, the archer reward creature) is the first user. It heals
75% at every level, with 65% of Bubka's damage and 75% of its HP.

**Planning — `ResolveHeals`** (`AttacksResolver.Healing.cs`) runs in `Resolve()` for both sides
**before** the two `ResolveTeam` passes, so the damage passes see the healed simulated HP.
- **Target.** Each shot picks a uniformly random **wounded** ally: another living creature or the
  hero, never the healer itself and never the Shield, with simulated HP below max. Repeats are allowed.
  If no ally is wounded, that shot heals nothing.
- **Effective amount.** The planned amount is capped at the target's missing HP at plan time
  (`HealAssignment.Amount`). Heals land before any hit on screen, so the animated and instant paths
  end identical.
- **Storage.** Heals live in `Heals`, not in `AllAttacks`. They give no XP, and `EstimateFirepower`
  ignores them.
- **Instant path.** `ApplyAttacksInstant` applies every heal, then every hit.
- **Statuses.** Heals go through `Health.Heal(amount, clearsStatuses: false)`. That skips
  `onHealed`, so a heal shot doesn't cure Shock the way the repeat-summon heal does.

**Animation: the battle pre-phase** (`PlayPrePhase`, called at the top of `ExecuteAnimations`):

- **Support volleys first.** Every healer with heals plays
  `SplitShotArcherAnimator.AttackWithSplitHits(no main hits, healHitsPerShot, onFinished)`. Every shot
  is split-only: heal missiles at allies, nothing at the enemy. Healers with nothing to heal skip the
  pre-phase.
- **The gate.** A `PrePhaseGate` counts every pending heal plus every pre-phase volley's animation.
  The whole regular battle starts through `_prePhaseGate.RunWhenOpen(...)`:
  - mutual tanks;
  - tank-chain heads (followers hang off their leader's `OnLeapBackStarted`);
  - every ranged attacker, the healers' regular volleys included.

  So the regular phase plays exactly as it would without healers, just later.
- **Opening.** The gate opens once the last heal has landed **and** the last volley animation has
  finished, so a healer's regular volley never starts on top of its own support volley. A
  generation-guarded fallback timeout opens it anyway after the pre-phase duration.
- **Why heals must land first on screen.** If a hit landed first, a target the plan healed out of
  lethal range would die before its heal arrived.
- **Timing.** `ExecuteAnimations` returns `prePhase + maxDuration + counters`.
  - `prePhase` is the longest `GetSplitVolleyDuration(shots)`: the archer volley time plus
    `flightAllowance`.
  - Without heals it is 0 and the gate is open from the start, so nothing changes.
- **Future pre-phase units.** Any other pre-phase effect should plan in `Resolve()` before
  `ResolveTeam` and play from `PlayPrePhase`, adding its own pending events to the same gate.
- **`SplitShotArcherAnimator` is generic.** It fires any extra `HitInfo`s per shot from
  `splitMissileAnimator`, through `ArcherAnimator.BuildExtraShot`. The main missile can be empty
  (support volley) or present (a future split-damage unit hitting two enemies per shot).
  `onFinished` hangs off `ArcherAnimator.OnVolleyFinished`.
- **Heal VFX.** `Health.healFeedback` is wired on `ParentUnit.prefab` and `Player.prefab` to a nested
  `_Feedbacks/HealFeedback.prefab`. That prefab holds an `MMF_Player` plus `MMF_Particles` playing
  `_Effects/_Heal/HealOnce` (an ETFX copy, Scaling Mode Local, `Shield` layer). It plays for every
  `Heal`, so the repeat-summon heal shows it too.

## Hit feedbacks

Every `Targetable` — the 13 unit prefabs (via `ParentUnit.prefab`), the 7 hero avatars (via
`Player.prefab`), and `Shield.prefab`/`ShieldRed` — carries exactly one nested instance of
`_Prefabs/_Feedbacks/HitFeedback.prefab`, cached by `Targetable.Awake()` via
`GetComponentInChildren<HitFeedback>()` (rule 14). It has two jobs:

- **`HitPlacePosition`** (`HitPlaceAcnhor` child) — where projectiles and nuke effects land. Each unit
  type tunes it with a position override on its type parent (`CatapultParentGreen` (0.24, 1.12),
  `DragonParentGreen` (0.89, 1.47), `_GolemParentGreen` (0.603, 1.198), the Big variants the same;
  `Player` (-0.28, 2.21); shields (0, 0)).
- **`PlayHitFeedbacks(bool isCritical)`** — called right after damage lands, animated path only:
  from `BuildHitInfos`'s `OnHit` right after `TakeDamage` for creature attacks
  (`ApplyAttacksInstant` plays nothing), and from the projectile impact callback of the **damaging**
  nuke animations (`StarfallAnimation`, `FireMagicAnimation`) right after `shot.Apply()`, with
  `isCritical = false` (`NukeResolver.ApplyInstant` plays nothing). `ShockAnimation` deliberately
  doesn't call it — Shock deals no damage, heroes are immune to it, and a standing `Shield` blocks it
  outright (`docs/ActionsAndSpells.md` §2c), so a hit reaction would be false feedback — except for improved-Shock bolts that actually damage
  a shield (`ShockShot.ShieldDamage > 0`, player-only reward), which do play it. The nuke call is guarded by `shot.Target != null`, same as
  `NukeShot.ApplyDamage`: the target can be destroyed while the projectile is still flying.
  - Every hit plays `hitFeedback`, the `MMF_Player` on the prefab **root**, holding one
    `MMF_Events` ("Hit Events", Feel's Events/Unity Events feedback). Its `PlayEvents` is empty in
    the base prefab, so for most targets it does nothing. A prefab that wants a reaction overrides
    that UnityEvent: `FinalBoss.prefab` wires it to `BossAnimator.PlayHurt`, which plays the boss's
    Hurt animation (see `docs/AnimationAPI.md`). A future per-unit hit reaction (flash, sound,
    another animation) is the same one-override change, no code.
  - A crit additionally plays `critFeedback`, the `MMF_Player` on the `CritFeedback` child, whose
    single `MMF_CameraShake` (0.2s, 0.15 x/y amplitude, 40 Hz) shakes the screen.

**Camera shake goes through Feel's camera rig, never the camera directly.** A feedback living inside
a prefab can't reference the scene camera, so `MMF_CameraShake` broadcasts an `MMCameraShakeEvent`
(channel 0) and the `MMCameraShaker` (+ `MMWiggle`) on `BattleScene`'s `CameraRig/CameraShaker`
consumes it by wiggling its own local position, with `Main Camera` nested underneath. Wiggling a rig
child, not the camera itself, is Feel's documented setup — an earlier attempt at shaking the camera
directly didn't look right. Any future screen-shake feedback should just add another
`MMF_CameraShake`; no new wiring needed.

**Shields needed the prefab too, not just units/avatars**: creature attacks hit shields, and
`PlayHitFeedbacks(true)` on a shield with no `critFeedback` wired would throw (rule 1 forbids a
defensive null-check). `Shield.prefab`'s own root-level `HitFeedback` component was removed in
favour of the nested prefab; its anchor used to be `ShieldVisual` at (0, 0), which the nested
anchor's default matches.

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
  _PlayerView/CreaturesManager.cs`).

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
