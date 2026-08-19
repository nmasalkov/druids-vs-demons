---
name: playtest-creature-attack
description: Deterministically spawn a specific creature (mage/archer/tank, player or enemy side) and trigger a battle via BalanceTool's Editor-button methods, then verify its attack — damage dealt, no exceptions, and (best-effort) visible projectile/muzzle/hit VFX. Use before confirming any fix/change to a creature's attack behavior, projectile prefab, or Animator, and before claiming a visual bug (missing muzzle, invisible missile, wrong hit effect) is fixed. Prefer this over driving the live slot-machine roll UI, which is RNG-based and easy to get stuck mid-roll in.
---

# Playtest: Creature Attack

For testing one creature's attack (damage, targeting, projectile/melee VFX) without fighting the real
slot-machine roll UI — which is RNG (you can't reliably roll the exact creature you want) and has a
roll/stop lifecycle you can get stuck in if you don't drive it exactly right. `BalanceTool` exists
specifically to skip all of that.

## Use BalanceTool, not the live UI

`Global/BalanceTool` in `BattleScene` (`Assets/Game/_Scripts/Global/Balance/BalanceTool.cs` +
`Editor/BalanceToolEditor.cs`) exposes plain public methods that its custom Inspector draws as buttons:
`SpawnPlayerMage`/`SpawnPlayerArcher`/`SpawnPlayerTank`, the matching `SpawnEnemy*` trio, `PlayBattle`,
`PlayNukeAction`, `PlaySpellAction`. Call them directly via `execute_script` — that's exactly what
clicking the button does, no UI simulation needed:

```csharp
var tool = Object.FindFirstObjectByType<BalanceTool>();
tool.SpawnPlayerMage();
tool.SpawnEnemyTank();   // always spawn something on the opposing side too —
                          // an attacker with no target may not fire at all
tool.PlayBattle();
```

**Don't** try to raycast-simulate these as UI clicks (see gotcha below) — they're Editor Inspector IMGUI
buttons, not in-game Canvas UI, and the click-simulation recipe doesn't apply to them at all.

## Steps

1. `check_compile_errors` — must be clean first.
2. `play_game`, then wait ~2-3 real seconds for `GameStartState`'s intro delay before doing anything else.
3. Confirm `BalanceTool` is present: `get_game_object_info` on `Global/BalanceTool` (it's already in
   `BattleScene` — don't spawn or add one).
4. Spawn what you need via `execute_script` (see snippet above). Calling `SpawnPlayer*`/`SpawnEnemy*`
   again on a slot that already holds a level<4 creature **promotes** it instead of respawning fresh
   (`BalanceTool.HandleExistingCreature`) — call it 4 times to force a despawn+respawn if you need a
   clean level-1 instance, or just accept the existing one if level doesn't matter for the test.
5. Confirm the spawn landed: `get_game_object_info` on the known slot path, e.g.
   `PlayerView/SpawnPlaces/SpawnPlace3Mage/<PrefabName>(Clone)`. The `(Clone)` name is the *visual*
   prefab's name, not necessarily the `CreatureSO`'s own asset name — a reskinned creature's `Data`
   field can legitimately still point at an old/reused balance asset (e.g. Bat's `Data` pointing at
   `Dragon.asset`); that's a reskin, not a bug, don't flag it as one.
6. Trigger the battle: `execute_script` calling `tool.PlayBattle()`. This runs a real `BattleState` →
   `AttacksResolver` pass exactly like the real game, just without the roll/turn UI in front of it.
7. **Verify functionally first — cheap and reliable, always do this:**
   - `get_unity_logs` (no filter) across the whole window, grep for `Exception`/`NullReference`/`Error`.
   - Read HP before/after on both the attacker and its target via an `execute_script` using
     `G.PlayerCreaturesManager.GetAllCreatures()` / `G.EnemyCreaturesManager.GetAllCreatures()`, each
     creature's `GetComponent<Health>().CurrentHealth`/`MaxHealth`. A HP delta on the target proves the
     attack's full path ran end-to-end (spawn → travel/resolve → `onHit` → `AttacksResolver`) —
     independent of whether you personally saw the VFX.
8. **Verify visually, best-effort:** `capture_scene_object` framing the attacker's `ProjectileSpawn`
   child, or `capture_ui_canvas`. See the timing gotcha below before spending many rounds chasing this.
9. `stop_game`, and reset any test-only state you changed (`Time.timeScale = 1` if you slowed it down).

## Rule: a reported VISUAL bug is not confirmed fixed until you have actually seen the render

Do not report a visual bug (missing muzzle, invisible missile, wrong hit effect, ...) as fixed on the
strength of "no exceptions" and "HP changed" alone. Those prove the *logic* path ran, not that anything
was ever drawn to the screen — and for this project specifically, damage can be fully decoupled from the
projectile's visual arrival (see `MageAnimator.StartFreezeSequence`: damage lands on a **fixed timer**
`freezeAfter + freezeDuration`, invoked independently of whether `SimpleProjectile.OnArrived()` ever
runs or whether the projectile is even visible). A real, live-caught case in this project: a fix was
reported "confirmed" on HP-delta + clean-logs evidence alone, but the actual bug (a rendering/rotation
issue making the traveling missile invisible) was still present — the user caught this immediately by
looking at the actual screen, something the report never actually did.

**If you cannot get an actual screenshot/pixels showing the effect, the task is not confirmed done —
say so explicitly** ("mechanically fires and no exceptions, but I could not visually confirm the render
— please treat this as unverified") rather than extrapolating confidence from an indirect signal. This
matters more, not less, when your diagnosis is "clearly correct" from reading code/prefab data — a
well-reasoned hypothesis about *why* something should now work is not the same as having seen that it
does.

## Gotcha: you probably cannot catch a fast one-shot VFX live via MCP polling

Each Coplay tool call is a real network round trip (roughly 1-2 real seconds). A short-flight
projectile and/or a fast attack fire-rate (this project's `BeamAnimator.fireRate` is `0.13s`) completes
and self-destroys well inside that round trip — by the time your next `get_unity_logs`/
`get_game_object_info` call lands, the missile and its hit VFX are already gone.

**Tried and did not reliably work in this project:** polling `GameObject.FindGameObjectsWithTag`
(`"Missile"`/`"Projectile"`) right after `PlayBattle()`, even down to `Time.timeScale = 0.05`–`0.01` —
because each *poll itself* (script call + log read, ~2 round trips) still costs more real time than one
slowed-down attack cycle, so every check straddles "not spawned yet" / "already destroyed". HP deltas
kept confirming the attack fired; the VFX window was never actually caught across ~6 attempts.

What to do instead, in order of preference:

1. **Prove it functionally instead** (step 7) and say so plainly in your report — "confirmed via HP
   delta + zero exceptions; did not independently capture the VFX live" is an honest, sufficient report
   when the bug's *mechanism* is already understood from reading the prefab/script (a mis-pointed
   `impactParticle` reference, a leftover conflicting movement script, an unassigned field), not merely
   inferred from a screenshot.
2. **Ask the user to eyeball it once** in a normal Play session instead of burning many tool-call rounds
   chasing a screenshot — a human watching the Game view in real time catches a 0.1s flash far more
   reliably than repeated MCP polling ever will, and costs them seconds. This is exactly the kind of
   thing worth just asking about rather than escalating guesses (see "When BalanceTool doesn't cover
   what you need" below — the same principle applies to testing techniques, not just missing hooks).
3. If a live capture is genuinely required (e.g. verifying particle scale/position/rotation, not just
   "does something appear"), temporarily slow down the *attack's own* speed/rate fields directly (e.g.
   `BeamAnimator.fireRate`, a projectile's travel speed) rather than `Time.timeScale` — global
   time-scaling slows down the round-trip-bound polling problem right along with the game, so it doesn't
   actually widen your effective capture window the way it seems like it should. Capture, then revert
   the field, same as `simple-debug`'s temporary-instrumentation-then-remove pattern.

## Gotcha: how to actually get a screenshot, and what didn't work

None of Coplay's built-in capture tools reliably show the actual battle in this project:

- `capture_ui_canvas` captures only the named (or first) Canvas's own render output in isolation — it
  does **not** composite the world-space battle camera underneath. Result: a screenshot of just one UI
  element floating on a plain black background, nothing else.
- `capture_scene_object` uses the **Scene view** camera, not the Game view / player camera, and its
  auto-framing is unreliable for small objects — framing a ~0.3-unit projectile or a single creature
  zoomed in so tight the result was a single flat color filling the frame. Framing nothing (no
  `gameObjectPath`) returned solid black (Scene view camera pointed at empty space).

**What actually gets the real player-visible frame:** `execute_script` calling
`ScreenCapture.CaptureScreenshot(absolutePath)`, then a separate `Read` tool call on that file path (Read
supports viewing images directly). This captures the true Game view composite, at full resolution.

**Still unresolved as of this writing:** even with `ScreenCapture.CaptureScreenshot`, a `BalanceTool`-
driven test (`SpawnPlayerMage()` + `PlayBattle()`, no real slot-machine roll) repeatedly captured only
the idle roll-UI panel over a flat, empty-looking background — not the battlefield (terrain, creatures,
HP bars) at all — even though `get_game_object_info` confirmed the creatures were alive and the
projectile was actively moving at the same moment. Likely cause (unconfirmed): the real game flow
transitions the camera/environment into "battle framing" as part of `GameManager`'s normal state
sequence, and `BalanceTool.PlayBattle()` constructs a `BattleState` directly, bypassing whatever
triggers that camera transition. A screenshot taken during a longer-running, less rapidly stop/start-
cycled Play session (spawn → wait → several checks, all within one continuous `play_game` call) *did*
correctly show the full battlefield in this same project — so the effect is inconsistent, not a hard
rule, and not yet root-caused.

**When you hit this: don't keep guessing at camera/capture mechanics on your own — ask the user.** They
can eyeball the real Game view directly in seconds and either confirm/deny what you're trying to verify,
or tell you what's different about your setup. Burning many rounds re-trying capture tools with slightly
different parameters, hoping one eventually works, is exactly the kind of stuck-in-a-loop situation this
skill exists to help you recognize and escape from early.

## Gotcha: the Hierarchy window can silently be scoped to an open prefab, not the Play-mode scene

`list_game_objects_in_hierarchy` reflects whatever the Editor's Hierarchy window is actually showing —
if a prefab happens to be open in Isolation/Prefab-edit mode (yours, or left over from the user's own
session), results silently come back scoped to that prefab instead of the running scene, no error.
Symptom: a call that returns unexpectedly empty results, or a response carrying a
`"This is the hierarchy for a prefab"` message you weren't expecting. Don't retry the same call —
`get_game_object_info` with a fully-qualified scene path (`Parent/Child/Grandchild`) works regardless of
what the Hierarchy window is currently showing, so prefer it once you know the path (from an earlier
successful listing, or from reading the scene/prefab source) over repeated broad
`list_game_objects_in_hierarchy` calls.

## Gotcha: Editor Inspector buttons are not in-game UI — don't raycast-simulate them

`docs/Loadout.md`'s "Testing UI clicks live via Coplay MCP" recipe
(`RectTransformUtility.WorldToScreenPoint` + `EventSystem.RaycastAll`) is for **in-game Canvas UI** the
player actually clicks (slot-machine buttons, reward cards, ...). It does not apply to `BalanceTool`'s
Inspector buttons — those are plain IMGUI drawn by `BalanceToolEditor`, Editor-only, never in a Canvas.
Call the underlying public method directly instead (`tool.PlayBattle()` etc.) — that *is* what the
button does, and it's strictly more reliable.

If you genuinely do need the in-game Canvas UI click recipe (testing the roll flow's UI itself, not
bypassing it): check the canvas's actual `renderMode` first (`get_game_object_info` on the `Canvas`
root's `Canvas` component). `docs/Loadout.md`'s recipe assumes Screen Space - Overlay
(`WorldToScreenPoint(null, ...)`) — `BattleScene`'s root `Canvas` is actually **Screen Space - Camera**
using `Main Camera`, so passing `null` silently produces a wrong screen position and the raycast returns
zero hits with no error anywhere. Pass the actual camera (`Camera.main`, or the canvas's own
`worldCamera`) instead.

## When BalanceTool doesn't cover what you need

If the scenario you need has no equivalent on `BalanceTool` (or `CampaignProgressTool` /
`CampaignDebugTool` for campaign-flow scenarios), don't reach for fragile UI-raycast simulation or many
rounds of trial-and-error against the live roll flow — **ask the user**. They can add a small debug hook
mirroring the existing tools' pattern (a public method + an Editor button, see `BalanceToolEditor.cs`)
faster and more reliably than you can reverse-engineer the live flow blind.

## Related

- `verify` skill — the general compile/Play-mode/log-reading discipline this complements; run both when
  a change touches gameplay code as well as a specific creature's attack.
- `simple-debug` skill — the temporary-instrumentation pattern referenced in the VFX-timing gotcha above.
- `docs/Battle.md`, `docs/SlotMachine.md` — background on `BattleState`/`AttacksResolver` and the real
  roll flow this skill deliberately bypasses.
