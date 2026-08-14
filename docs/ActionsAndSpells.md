# Actions & Spells (Nukes + Spells)

Nukes and Spells are two *rolled action types* (alongside Creature rolls) that share one
architecture end-to-end: a `ScriptableObject` describes the action, a `Resolver` computes what
happens, and an `Animation` plays how it looks. This doc covers that shared pipeline plus how to
add a new one. See [GameLoop.md](GameLoop.md) for how these fit into the round/state sequence, and
[Battle.md](Battle.md) for `Health`/`Targetable`/creature mechanics referenced here.

Robotek terminology used by the user: **hack = Charm**, **robots = creatures**, **mainframe = hero**.
Charm is a Spell — see the worked example in §3b.

## 1. Key files

Data layer (`Assets/Game/_Scripts/ScriptableObjects/`):
- `ActionSO.cs` — abstract base. Holds `actionName`/`cardSprite`, virtual `AnimationPrefabBase`
  (null by default), virtual `CreateAndResolve(ActionContext, level)` (null by default).
- `NukeSO.cs` / `SpellSO.cs` — abstract subclasses. Add `AnimationPrefab` (typed) and a virtual
  `CreateResolver()` defaulting to a no-op resolver (`NoOpNukeResolver`/`NoOpSpellResolver`) so
  placeholder balance-only SO assets don't throw.
- Concrete SOs, one file each: `FireMagicSO.cs`, `ShockSO.cs`, `StarfallSO.cs` (nukes);
  `ShieldSO.cs`, `CharmSO.cs`, `BattleCrySO.cs` (spells). Each just overrides `CreateResolver()`
  and holds its own balance arrays (damage/HP/chance per level).

Logic layer:
- `Assets/Game/_Scripts/ScriptableObjects/ActionResolver.cs` — abstract base, one method:
  `ApplyInstant()`.
- `Assets/Game/_Scripts/Nukes/NukeResolver.cs` — adds `List<NukeShot> Shots`, abstract
  `Resolve(NukeSO, Hero caster, List<Creature> enemyCreatures, Hero enemyHero, Shield enemyShield, int level)`,
  and `BuildPriorityTargets(...)` (shield → mages → tanks → archers → hero, all-alive-of-class, not
  just the first) shared by nuke resolvers that need a priority order.
- `Assets/Game/_Scripts/Spells/SpellResolver.cs` (not read in full here, but mirrors NukeResolver:
  `List<SpellShot> Shots` + `Resolve(SpellSO, Hero caster, HeroView casterView, List<Creature> enemyCreatures, int level)`).
- Concrete resolvers, one per action: `FireMagicResolver.cs`, `ShockResolver.cs`,
  `StarfallResolver.cs` (Nukes/); `ShieldResolver.cs`, `CharmResolver.cs`, `BattleCryResolver.cs`
  (Spells/). Each just fills `Shots` — no animation, no `Object.Instantiate` of gameplay effects.
- `NukeShot.cs` / `SpellShot.cs` (base "shot" types, one instance = one target + one effect) and
  their concrete shots (`FireMagicShot`, `StarfallShot`, `ShockShot`; `ShieldSpawnShot`/
  `ShieldPromoteShot`/`ShieldHealShot`, `CharmShot`, `BattleCryBuffShot`/`BattleCryDebuffShot`).
  **All actual data mutation lives in a shot's `Apply()`** — this is what makes the animated path
  and the instant path reach the identical end state (rule 7).

View layer:
- `Assets/Game/_Scripts/ScriptableObjects/ActionAnimation.cs` — abstract MonoBehaviour base, one
  method: `Execute(ActionSO source, Hero caster, ActionResolver resolver, Action onComplete)`.
- `Assets/Game/_Scripts/Nukes/NukeActionAnimation.cs` / `Assets/Game/_Scripts/Spells/SpellActionAnimation.cs`
  — typed intermediate bases; `Execute` is `sealed override` and downcasts to a typed
  `Execute(NukeSO/SpellSO, Hero, NukeResolver/SpellResolver, Action onComplete)` that concrete
  animations implement.
- Concrete animation prefabs: `FireMagicAnimation.cs`, `ShockAnimation.cs`, `StarfallAnimation.cs`;
  `ShieldSpellAnimation.cs`, `CharmAnimation.cs`, `BattleCryAnimation.cs`, `NoOpSpellAnimation.cs`.

Orchestration:
- `Assets/Game/_Scripts/ScriptableObjects/ActionContext.cs` — struct: `Caster`, `CasterView`,
  `EnemyCreatures`, `EnemyHero`, `EnemyShield`.
- `Assets/Game/_Scripts/ScriptableObjects/IActionEntry.cs` — `ActionSO Source { get; }`,
  `int Level { get; }` (level doubles as "match count", see §4).
- `Assets/Game/_Scripts/Global/GameManager/ActionState.cs` — shared base for the two states below;
  owns `PlayEntries`/`PlayEntry`/`ScheduleNext`/`CompleteWithCleanup`/`ResolveInstant`/`BuildContext`.
- `Assets/Game/_Scripts/Global/GameManager/NukeState.cs` / `SpellState.cs` — one-liners: call
  `PlayEntries(RollStateManager.Instance.NukeEntries/SpellEntries, pauseBetween, stateName)` in
  `OnEnter()`, and expose a static `ResolveNukesInstant()`/`ResolveSpellsInstant()`.
- `Assets/Game/_Scripts/Global/RollStateManager/RollStateManager.cs` — `NukeEntry`/`SpellEntry`
  structs (`{ NukeSO/SpellSO; int Count; }`, `IActionEntry.Level => Count`), populated by
  `AnalyzeRoll()` after a roll finishes (groups the 3 rolled symbols, one entry per distinct
  symbol, `Count` = how many of that symbol landed).

## 2. Multiplicity: how 1/2/3 matches become `level`

A roll lands 3 symbols across 3 slot-machine columns. `RollStateManager.AnalyzeRoll()` groups them
by the underlying `ActionSO` and creates one `NukeEntry`/`SpellEntry` per distinct symbol, with
`Count` = how many columns landed that symbol (1, 2, or 3). If all 3 columns match the same symbol,
`TripleRolled = true` and `GameManager.TakeTurn()` re-rolls and replays the action (see GameLoop.md).

`IActionEntry.Level` is just an alias for that count — so "level" here means "how many of this
symbol were rolled this turn," NOT a creature's XP level (`Experience.Level`, unrelated — see
Experience.md). Every concrete SO's balance array is indexed by `level - 1` (`GetDamageForLevel`,
`GetHpForLevel`, `GetChanceForLevel`, etc.), clamped so an out-of-range level falls back to the
highest configured tier.

## 3. Worked examples

### 3a. FireMagic (Nuke) — single damage pool, priority targets

1. `FireMagicSO` (`ScriptableObjects/FireMagicSO.cs`) just overrides `CreateResolver() => new FireMagicResolver()`.
   Balance (`damagePerLevel`) and targeting flag (`ignoresShield`) live on the shared `NukeSO` base.
2. `FireMagicResolver.Resolve(...)` (`Nukes/FireMagicResolver.cs`) builds a priority target list via
   `NukeResolver.BuildPriorityTargets` (shield first unless `IgnoresShield`, then all alive mages,
   tanks, archers, then hero), then walks it distributing one flat damage pool
   (`source.GetDamageForLevel(level)`): each target absorbs up to its current HP, one `FireMagicShot`
   per target that actually receives damage, stops once the pool is exhausted.
3. `FireMagicAnimation.Execute(...)` (`Nukes/FireMagicAnimation.cs`) is pure view: for each shot,
   after `castDelay + i*pauseBetweenShots`, instantiates a `SimpleProjectile` toward
   `shot.Target.HitFeedback.HitPlacePosition`, and on impact calls `shot.Apply()` (which calls
   `Target.Health.TakeDamage(damage)` via `NukeShot.ApplyDamage`) then a per-shot completion
   callback; once all shots resolved, waits `trailingDelay` then calls `onComplete`.
4. Instant path: `NukeState.ResolveNukesInstant()` → `ActionState.ResolveInstant` →
   `FireMagicSO.CreateAndResolve(ctx, level).ApplyInstant()` → calls every shot's `Apply()`
   immediately, no projectiles, same end HP values.

### 3b. Shield (Spell) — spawns a persistent Targetable, three-way branch

1. `ShieldSO` (`ScriptableObjects/ShieldSO.cs`) holds `hpPerLevel`/`healPerLevel` arrays and
   separate player/enemy `Shield` prefabs (`GetPrefab(isPlayer)`); overrides
   `CreateResolver() => new ShieldResolver()`.
2. `ShieldResolver.Resolve(...)` (`Spells/ShieldResolver.cs`) branches on `casterView.Shield`
   (`HeroView.Shield => shieldSlot.Unit as Shield`):
   - no shield yet → `ShieldSpawnShot` (instantiate `Data.GetPrefab(isPlayer)` into
     `casterView.ShieldSlot`, `Slot.Unit = instance`, `instance.Init(level)`, `instance.OnSummon()`)
   - rolled level higher than existing → `ShieldPromoteShot` (`((Shield)Target).Promote(level)`,
     full HP refill at the new level)
   - rolled level ≤ existing → `ShieldHealShot` (flat `Target.Health.Heal(...)`)
3. `ShieldSpellAnimation.Execute(...)` (`Spells/ShieldSpellAnimation.cs`) is a minimal host: calls
   `resolver.ApplyInstant()` immediately and completes — the actual grow-in tween lives on the
   summoned `Shield`'s own `ShieldAnimator` (`PlaySummon`/`PlayPromote`), triggered from
   `Shield.OnSummon()`/`Promote()` themselves, not from the ActionAnimation.
4. **Restart coverage**: `Shield` is a persistent `Targetable` sitting in `HeroView.ShieldSlot` — it
   is cleaned up on battle restart via `HeroView.ClearShield()`, which subscribes to
   `GameManager.OnBattleRestart` (see GameLoop.md §Restart). No gap found.

### 3c. Charm (Spell, Robotek "hack") — steals/re-steals a creature

`CharmResolver.Resolve(...)` (`Spells/CharmResolver.cs`) picks one alive enemy creature whose class
has a free charm slot on the caster's board (`CreaturesManager.GetFreeCharmSlot`), ranks candidates
by `CharmSO.GetStrength` (`currentHp + creatureLevel * levelStrengthWeight`), and picks the
weakest/median/strongest candidate for level 1/2/3 (`PickByLevel`). It rolls success **once** up
front (`chancePerLevel[level-1] * aliveCount`, clamped to 1) and bakes the result into a single
`CharmShot`, so the animated and instant paths always agree on success/failure.

`CharmShot.Apply()` (`Spells/CharmShots.cs`) does the actual re-parenting: clears the creature's old
slot, reparents its transform under the destination charm slot, updates `Creature.Slot` /
`UnitSlot.Unit` both ways, and toggles `StatusesManager.IsCharmed` (charming a normal creature marks
it; charming an already-charmed one clears the mark — a creature only ever changes sides via Charm,
and once it leaves its native slot it never returns to one). If `Success` is false, `Apply()` is a
no-op — a failed charm attempt still consumes the roll but changes nothing.

**Restart coverage**: a charmed creature just lives in a charm slot on some `CreaturesManager`,
which `CreaturesManager.ResetAll()` already destroys unconditionally (native + charm slots alike) on
`OnBattleRestart`. `StatusesManager.IsCharmed` dies with the GameObject. No gap found.

### 3d. BattleCry (Spell) — self-buff + enemy-debuff, no persistent object

`BattleCryResolver.Resolve(...)` (`Spells/BattleCryResolver.cs`) emits one `BattleCryBuffShot` per
creature on the caster's own board and one `BattleCryDebuffShot` per enemy creature, both applying a
flat `AttackDamageMultiplier` via `StatusesManager` (read back later by
`CreaturesManager.GetActiveBattleCryBuffMultiplier()` so a creature spawned *after* BattleCry was
cast the same turn still joins the active buff). Like Charm, this state lives entirely on
`StatusesManager` components that die with their creature GameObjects — restart via
`CreaturesManager.ResetAll()` already covers it.

## 4. `ActionState` orchestration (`Global/GameManager/ActionState.cs`)

- `BuildContext()` — builds an `ActionContext` from `GameManager.Instance.ActiveSide` each call
  (not cached), via `Caster`/`CasterView`/`EnemyHero`/`EnemyShield`/`EnemyCreatures` helpers that
  flip player/enemy based on `IsPlayerCasting`.
- `PlayEntries(entries, pauseBetween, stateName)` — entry point called from `NukeState`/
  `SpellState.OnEnter()`. Empty list → `CompleteWithCleanup()` immediately. Otherwise starts the
  `PlayEntry` chain at index 0.
- `PlayEntry(entries, index, ...)` — **first line is `if (IsStale) return;`** (see below), then: if
  past the end → `CompleteWithCleanup()`; if the entry has no `Source` or no
  `Source.AnimationPrefabBase` → logs a warning and skips to the next entry via `ScheduleNext`
  (placeholder-safe, per design — SO assets with balance numbers but no animation wired up yet don't
  crash the loop). Otherwise: resolve (`entry.Source.CreateAndResolve(BuildContext(), entry.Level)`),
  `Object.Instantiate` the animation prefab, call its `Execute(...)`, and on its `onComplete` destroy
  the animation instance and advance via `ScheduleNext`.
- `ScheduleNext(...)` — `Utils.DoAfterDelay.Execute(() => PlayEntry(..., index+1, ...), pauseBetween)`.
- `CompleteWithCleanup()` — after `PostBattleStateManager.Instance.CleanUpDelay`, guards `IsStale`
  again, then `CleanUpDeadCreatures()` (both sides' `CleanUpDead()`) and `CompleteState()`.
- `ResolveInstant<TEntry>(entries)` (static) — the rule-7 instant path: builds one `ActionContext`,
  calls `CreateAndResolve(ctx, e.Level).ApplyInstant()` per entry (no animation, no waits), then
  `CleanUpDeadCreatures()`. `NukeState.ResolveNukesInstant()`/`SpellState.ResolveSpellsInstant()`
  are the public one-liners that call this.

### The `IsStale` guard (added alongside battle-restart, 2026-07)

`ActionState` derives from `GameState`, which exposes `protected bool IsStale` backed by a
`GameManager.Generation` counter incremented once per `GameManager.RestartBattle()` call (see
GameLoop.md for the full mechanism). `PlayEntry` checks `IsStale` before touching anything, and
`CompleteWithCleanup`'s delayed callback checks it again before running `CleanUpDeadCreatures()`/
`CompleteState()`. This matters here specifically because `ScheduleNext`/`PlayEntry` chain through
`Utils.DoAfterDelay` (real-seconds-scaled `WaitForSeconds`, see GameLoop.md), so a battle restart
mid-animation (e.g. clicking "Restart Battle" while a FireMagic laser is still firing) would
otherwise resume the old entry chain against already-destroyed creatures once the coroutine's delay
elapsed. With the guard, that resumed call is a silent no-op instead.

## 5. How to add a new Nuke or Spell

1. Add balance numbers to a new `[CreateAssetMenu]` SO subclassing `NukeSO`/`SpellSO` (e.g.
   `ScriptableObjects/MyNukeSO.cs`), following the existing `float[] xPerLevel` + `GetXForLevel(level)`
   clamped-index pattern. Balance numbers belong here, never on the animation MonoBehaviour (project
   rule 13 — data/view separation).
2. Write a resolver (`Nukes/MyNukeResolver.cs` or `Spells/MyNukeResolver.cs`) subclassing
   `NukeResolver`/`SpellResolver`, implementing `Resolve(...)` to fill `Shots` with concrete
   `NukeShot`/`SpellShot` subclasses. Put ALL data mutation in each shot's `Apply()` — never in the
   animation — so `ApplyInstant()` (which just calls every shot's `Apply()`) reaches the identical
   end state as the animated path (project rule 7).
3. Override `CreateResolver()` on your SO to `return new MyNukeResolver();`.
4. Write an animation prefab: a MonoBehaviour subclassing `NukeActionAnimation`/`SpellActionAnimation`,
   implementing the typed `Execute(MyNukeSO, Hero caster, MyNukeResolver resolver, Action onComplete)`
   overload. Pure presentation — read `resolver.Shots` to know what to visualize and when to call
   each shot's `Apply()` (e.g. on projectile impact), then call `onComplete`.
5. Assign the animation prefab to the SO's `animationPrefab` field in the Editor, and add the new SO
   asset to whichever roll pool feeds the slot machine (`DefaultNukesSO`/`DefaultSpellsSO`).
6. If your action spawns a persistent object (like Shield) or introduces new manager-level state,
   subscribe that new state's owner to `GameManager.OnBattleRestart` with a reset method (project
   rule — see GameLoop.md). If it only mutates existing creatures/heroes via shots (like Charm/
   BattleCry), no new restart wiring is needed — it's covered transitively by the existing
   `CreaturesManager.ResetAll()`/`HeroView.ClearShield()`/`Hero.InitHealth()` subscribers.
7. Optional: give the enemy AI a reason to pick it. Write a scorer in `AI/Scoring/` subclassing
   `ActionAIScorer` and override `CreateAIScorer()` on your SO to return it — one line, same shape as
   step 3's `CreateResolver()`. Without this, the SO falls back to `NoOpActionAIScorer` (score 0
   always), so the AI simply never picks it. See `docs/AI.md`.

## Related docs
- [GameLoop.md](GameLoop.md) — round/state sequence, `Generation`/`IsStale` mechanism, `OnBattleRestart` event and its subscribers, pause menu.
- [Battle.md](Battle.md) — `Health`/`Targetable`/`Unit`/`Creature`/`Hero`/`Shield`, `StatusesManager`.
- [SlotMachine.md](SlotMachine.md) — how a roll produces the `ActionSO` list that `RollStateManager.AnalyzeRoll` turns into entries.
- [Experience.md](Experience.md) — `Experience.Level` (unrelated to the "level" used in this doc), XP granting on kill.
