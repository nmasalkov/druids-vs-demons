---
name: verify
description: Compile the project, enter Play mode, read Unity logs, inspect relevant GameObjects, and report any errors or unexpected runtime values before confirming a Unity change works. Use after any change to gameplay code, scenes, or prefabs — never claim something works based only on a clean compile or an isolated execute_script check.
---

# Verify

Minimal, mandatory verification pass for changes to Unity gameplay code, scenes, or prefabs. A clean
compile and a correct isolated `execute_script` check are necessary but not sufficient — they can
both pass while the game is still unplayable (e.g. a gate returning early so a whole subsystem never
activates). This skill exists because exactly that happened: a slot-machine activation gate silently
blocked the game from starting, and it shipped because only isolated function-level checks were run
instead of an actual playthrough.

## Steps

1. **Compile check** — `mcp__coplay-mcp__check_compile_errors`. Fix and repeat until clean.
2. **Enter Play mode** — `mcp__coplay-mcp__play_game`.
3. **Run the relevant checklist.** If the change touches battle/campaign/roll flow, run
   `battle_scene_verify.md` (same directory as this file) end-to-end — don't skip steps; each one
   exists because a real bug was missed there before. If no checklist matches the change, still do a
   short manual equivalent: exercise the actual user-facing flow the change affects, not just the
   function you edited.
4. **Read Unity logs throughout, not just at the end** — `mcp__coplay-mcp__get_unity_logs`. Grep for
   `Exception`, `NullReference`, `"level":"Error"`. An error mid-sequence can get silently absorbed
   by whatever runs next; check after each meaningful step, not only once at the finish.
5. **Inspect relevant GameObjects** — `get_game_object_info` / `list_game_objects_in_hierarchy` for
   anything the change touches: is it active? are serialized fields actually wired? did the expected
   GameObject/component even get created? Don't infer state from code alone when you can read it
   directly from the running Editor.
6. **Stop Play mode and clean up** any test-only state (test saves via `SaveStorage.Backend.Delete()`,
   spawned test objects) before reporting — Play mode itself reverts scene changes automatically, but
   real save data written during the test does not.
7. **Report what you actually observed** — specific log lines, values read back, which steps passed —
   not just "it should work now." If a path wasn't verified, say so explicitly instead of implying
   it's covered.

## When to run this

After any change to: `GameManager`/`GameState` subclasses, `SlotMachine`/`RollStateManager`,
`CampaignManager`/`CampaignStateManager`/encounter flow, or scene/prefab wiring that anything
gameplay-critical depends on. Not required for pure doc/comment-only edits.

## Skipping for small fixes

User-confirmed OK: for a small, narrowly-scoped fix (a single field's default, a one-line logic swap,
something whose whole mechanism fits in one isolated `execute_script` check) it's fine to skip the
full Play-mode checklist above — an isolated check that directly exercises the exact thing the bug was
about is enough. Say so explicitly in the report ("skipped full verify — confirmed via an isolated
check because X") rather than silently omitting steps or implying the full pass ran. Still run the
full checklist for anything touching a multi-step flow (turn order, encounter transitions, restart,
anything a single function-level check can't see the whole picture of).
