# AnimationAPI (Animator-driven visuals) + BossAnimator

## What this system does

Every unit and hero avatar in the project was originally Spine-based: `UnitAnimator` (and every
subclass, including `HeroAnimator`) plays animations straight on a `SkeletonAnimation`. The final
boss (`FinalBoss.prefab`) uses the **Skeleton Warlock** sprite rig instead, which runs on a plain
Unity `Animator` + `AnimatorController`.

`AnimationAPI` is a small, generic playback layer for **any** GameObject with an `Animator`. It lets
view code say "play Hurt once" or "rest in Dead" by name, without knowing the controller's parameter
names or transition graph. A one-click editor tool generates that graph from the controller's states.
`BossAnimator` is the first consumer: a drop-in `HeroAnimator` that routes the usual avatar calls
through `AnimationAPI` instead of Spine.

## Key files

- `Assets/Game/_Scripts/_Utils/AnimationAPI.cs` — runtime component (`[RequireComponent(Animator)]`),
  goes on the GameObject that holds the `Animator`.
- `Assets/Game/_Scripts/_Utils/Editor/AnimationAPIControllerWiring.cs` — static `Wire(AnimatorController)`,
  the generator.
- `Assets/Game/_Scripts/_Utils/Editor/AnimationAPIEditor.cs` — Inspector: wiring status + **Wire
  controller** button (edit mode); live state + per-animation **Trigger**/**Enable** buttons (Play mode).
- `Assets/Game/_Scripts/_Units/_Animators/BossAnimator.cs` (+ `Editor/BossAnimatorEditor.cs`) —
  `HeroAnimator` subclass driven by `AnimationAPI`.
- `Assets/Game/_Animations/_Bosses/SkeletonWarlockBoss.controller` — wired **copy** of the vendor
  `Assets/Skeleton Warlock/Controllers/Skeleton Warlock.controller`.
- `Assets/Game/_Prefabs/_Characters/_Avatars/_Enemies/FinalBoss.prefab` — the only user so far.

## Runtime API contract

Convention, per animation (= state) `N`: **trigger `N`** + **bool `IsN`**. The bool can't also be called
`N` — Unity forbids two parameters with the same name, whatever their types.

| Call | Effect |
| ---- | ------ |
| `TriggerAnimation(N)` | Plays `N` **now, once** (Any State transition, interrupts anything), then falls back to the enabled animation. Re-triggering while `N` plays restarts it (repeated hits restart Hurt). |
| `EnableAnimation(N)` | `N` becomes the animation the Animator **rests in**: sets `IsN`, clears every other `Is*` bool, and fires trigger `N` so the switch is immediate. No-op if `N` is already enabled (so a one-shot playing on top of it isn't cut off). |
| `EnableAnimation(N, playImmediately: false)` | Same, but doesn't fire the trigger — whatever is playing finishes first, then the Animator settles into `N`. |
| `HasAnimation(N)`, `GetClipLength(N)`, `AnimationNames` | Lookups. `GetClipLength` matches by **clip name** (== state name for the Warlock), 0 if absent. |
| `CurrentStateName`, `EnabledAnimation`, `LastTriggeredAnimation` | Inspector/debug state. The first two are read live off the Animator (rule 22); only the last trigger is stored, since consumed triggers leave no trace. |

- Names are discovered in `Awake()` from `Animator.parameters` (triggers → names; bools prefixed `Is`
  → names). Nothing is code-generated, so there's nothing to regenerate when a controller changes —
  only re-run the wiring.
- An **unknown name throws** `ArgumentException` listing the available names (rules 1/5: a typo'd
  animation name is a setup bug and should be loud).
- No per-call allocations: hashes are cached once; `EnableAnimation` loops over the cached bool hashes.

## Controller wiring (`AnimationAPIControllerWiring.Wire`)

Works on **layer 0's root state machine only** (extra layers / sub-state machines are left untouched,
with a warning). For every root state `N`:

1. Validates first: if `N` or `IsN` already exists with the wrong type, logs an error and changes nothing.
2. Adds trigger `N` and bool `IsN` if missing (other parameters are left alone).
3. **Replaces** all Any State, Entry, and outgoing state transitions with:
   - **Any State → N** on trigger `N` — no exit time, duration 0, `canTransitionToSelf = true`.
   - **Entry → N** when `IsN` is true.
   - **N → Exit** when `IsN` is false, **with exit time** `clamp(1 − 0.1/clipLength, 0, 0.99)` and a
     fixed 0.1 s blend (so the blend finishes as the clip ends).

Resulting behavior: a triggered one-shot finishes → leaves through Exit → re-enters through Entry
into whichever `Is*` is true (or the controller's default state if none is). An enabled animation
never satisfies its own exit condition, so it simply loops natively. At the root state machine,
Exit → Entry works exactly like this (verified live: Hurt → Idle, Die → Dead).

It's destructive on purpose and idempotent — adding a state and re-running is the whole workflow. The
Inspector button asks for confirmation first; the change is saved to the asset.

## BossAnimator

`BossAnimator : HeroAnimator` — a subclass so it satisfies `Hero`'s
`[RequireComponent(typeof(HeroAnimator))]` and is what `Unit.Animator` (`GetComponent<UnitAnimator>`)
and `Health.animator` resolve to. `UnitAnimator` only gained virtual seams for it (`PlayIdle`,
`PlayDead`, `GetAnimationDuration`); nothing Spine-side changed.

| Method | AnimationAPI calls |
| ------ | ------------------ |
| `Start()` | no Spine init; `PlayIdle()` |
| `PlayIdle()` | `EnableAnimation(Idle)` (ignored once dead) |
| `PlayDead()` | `isDead = true`; `EnableAnimation(Dead, false)` then `TriggerAnimation(Die)` → fall, then lie still |
| `PlayAttack()` / `GetAttackDuration()` | `TriggerAnimation(Cast01)` / its clip length (heroes don't attack today) |
| `PlayHurt()` | `TriggerAnimation(Hurt)` — target of `HitFeedback`'s Unity Events feedback (see `docs/Battle.md` "Hit feedbacks") |
| `PlayRise()` | `isDead = false`; `EnableAnimation(Idle, false)` then `TriggerAnimation(Rise)` |
| `GetRiseDuration()` | `GetAnimationDuration(riseAnimation)` — lets a caller schedule against the end of `PlayRise` without knowing this boss's animation names |
| `PlayWalk()` | `EnableAnimation(Walk)` — `PlayIdle()` to stop |
| `PlayCast1/2/3()` | `TriggerAnimation(Cast01/02/03)` — parameterless so they're pickable in UnityEvent dropdowns |

Every one-shot (Hurt, casts, attack) is ignored while dead; only `PlayRise` leaves Dead — and that is
now a real gameplay call, not just an Inspector button: `BossPhaseTransitionState` plays it when a boss
whose fight is followed by a `FightSO.continuesPreviousFight` phase dies, then schedules the rest
of the transition off `GetRiseDuration()`
(`AnimationAPI` has no "animation finished" event — clip length is the only timing primitive; see
`docs/GameLoop.md`). Animation names
are serialized string fields (defaults match the Warlock controller), so a future boss on a different
controller only needs different names, not a new class. JumpUp/JumpDown have no dedicated method — use
`AnimationApi` directly if ever needed.

`BossAnimator` is view-only (rule 7: no instant counterpart needed) and holds no battle-scoped state —
like `UnitAnimator`, it doesn't subscribe to `OnBattleRestart` (restart only happens mid-fight, while
the hero is alive).

### FinalBoss.prefab wiring

- Root (variant of `EnemyAvatarParent` → `Player`): `BossAnimator` added, the inherited `HeroAnimator`
  **removed** (`m_RemovedComponents`), `Health.animator` → `BossAnimator`.
  `BossAnimator.animationApi`/`creatureVisual` → the nested `Skeleton Warlock`.
- Nested `Skeleton Warlock`: `Animator.m_Controller` overridden to `SkeletonWarlockBoss.controller`;
  `AnimationAPI` added.
- Nested `HitFeedback`: its root `MMF_Player`'s "Hit Events" (`MMF_Events`) `PlayEvents` →
  `BossAnimator.PlayHurt` (a managed-reference override on this prefab only — the base
  `HitFeedback.prefab` stays empty).

### FinalBoss 2.prefab wiring

The second consumer: Fight 5's phase-2 avatar (`fights[5]`, flagged `FightSO.continuesPreviousFight` —
see `docs/Encounters.md`). Identical
wiring to `FinalBoss.prefab` above, with the nested visual being `Skeleton Warlock FIRE.prefab` (the
same rig on fire materials) instead of the vendor `Skeleton Warlock`. Four things are easy to miss when
building a prefab like this by duplicating the visual — all four were in fact missing here:

1. The nested rig's `Animator.m_Controller` must be overridden to `SkeletonWarlockBoss.controller`.
   A copy of the warlock prefab inherits the **vendor** `Skeleton Warlock.controller`, which has zero
   parameters, so `AnimationAPI.Awake()` discovers no animations and the first `TriggerAnimation` throws.
2. `AnimationAPI` has to be added to the nested rig (it isn't on the warlock prefab itself).
3. `BossAnimator.animationApi` → that `AnimationAPI`.
4. `BossAnimator.creatureVisual` → the nested rig's transform.

`BossAnimatorEditor` only shows live state for a boss **in the scene**: it bails out for the prefab
asset and for Prefab Mode, since `Awake()` never ran there and reading `AnimationAPI.CurrentStateName`
would throw `UnassignedReferenceException` on every repaint (Prefab Mode objects live in a preview
scene and are not persistent, so an `EditorUtility.IsPersistent` check alone doesn't catch them).

## Gotchas

- **Never wire a vendor controller in place.** Wiring replaces every transition (the vendor demo's
  automatic Die → Dead → Rise → Idle chain included) and a store update/re-import would silently wipe
  it. Copy it into `Assets/Game/_Animations/` and override the prefab's `Animator` to the copy — the
  Inspector warns when the controller lives outside `Assets/Game`.
- **Exit time must stay below 1.** Unity re-checks exit times < 1 on every loop but evaluates exit
  times ≥ 1 only once, so a looping state with exit time 1 would never leave after its first cycle.
  All the Warlock clips are authored with Loop Time on, so "loop vs one-shot" can't be inferred from
  the clips — the convention doesn't need to.
- **Don't fire two triggers on the same frame.** `PlayDead` enables Dead with `playImmediately: false`
  precisely because `EnableAnimation(Dead)` + `TriggerAnimation(Die)` would leave the Dead trigger
  pending and cut the fall short on the next frame.
- **Subclass field names must differ from `UnitAnimator`'s private serialized `idle`/`dead`/`attack`**
  — Unity refuses a field name serialized twice across a class hierarchy. Hence `idleAnimation` etc.
  `BossAnimatorEditor` hides the inherited, unused Spine fields.
- **Swapping an avatar's animator in a prefab variant**: add `BossAnimator` first (it satisfies
  `Hero`'s `RequireComponent`), then remove the inherited `HeroAnimator` by **exact type** — a
  `GetComponent<HeroAnimator>()` would also match the subclass.
