---
name: simple-debug
description: Cheap, log-driven bug reproduction for numeric/state bugs (wrong value persisted, wrong value displayed, value reverts unexpectedly) in Unity gameplay code — when the user reports a concrete, reproducible bug but an isolated execute_script check or code read-through couldn't reproduce or explain it. Adds temporary Debug.Log calls at every write site of the value in question, reproduces the exact repro live in Play mode, and reads the log timeline back to find the one write that's wrong — instead of re-reading code and re-guessing.
---

# Simple Debug

For bugs that are **just numbers or state flags** (an int/bool/string ends up wrong, or reverts,
or doesn't persist) — not visual/animation/timing bugs, which need screenshots or `capture_scene_object`
instead. This skill trades a few minutes of temporary logging for certainty, instead of re-reading
source files and re-guessing at a mechanism. Reach for it specifically when: the user has given a
concrete repro with concrete before/after numbers, and a prior attempt to reproduce it (via
`execute_script` state checks or an automated click-through) either failed to reproduce it or the
root cause still isn't nailed down to one specific line.

## Why this beats re-reading code

Re-reading source and reasoning about call order is fine for finding *candidate* causes, but for a
value that's wrong at runtime, only two things are authoritative: what value a line actually wrote,
and in what order. Guessing which of N call sites is the culprit from static reading alone is slow
and error-prone once more than 2-3 sites touch the same field — a log timeline settles it in one
Play-mode pass.

## Steps

1. **Identify every write site of the suspect value.** `Grep` for the field name across
   `Assets/Game/_Scripts` (e.g. `currentEnergy`) — every line that assigns to it (`run.currentEnergy =
   ...`, not reads) is a candidate. Don't filter by "looks unrelated" — restart/reset paths and debug
   tools are exactly where these bugs hide (see CLAUDE.md rule 22's `EnergyController` example, and
   rule 26's `CampaignDebugTool` example — both were found this way).
2. **Add one `Debug.Log` per write site**, logging the old value, the new value, and enough context to
   identify *when* in the flow it fired (calling method name is usually enough — Unity's stack trace in
   the log already shows the call chain). Keep it to one line per site; don't restructure the code to
   add logging.
3. **Reproduce the user's exact steps in Play mode** — same starting state, same actions, in the same
   order they described (which scene they started from matters — see rule 26). Use `execute_script` to
   drive state/UI directly (set a save via `SaveStorage.Backend.Write`, invoke button `onClick`s,
   call the method under test) rather than trying to simulate UI input pixel-by-pixel — same approach
   `verify`'s battle checklist uses.
4. **Read the full log timeline** — `get_unity_logs` with no filter or a broad one, ordered
   chronologically — and find the exact write that doesn't match what the user's repro says should
   happen. The timestamp ordering tells you the call sequence for free.
5. **Fix the root cause, then remove the temporary logs** (or fold anything genuinely worth keeping
   into a real diagnostic, like CLAUDE.md rule 19's Inspector-visibility pattern, rather than leaving
   scattered `Debug.Log` calls in shipped gameplay code).
6. **Re-run the same repro once more** to confirm the fixed timeline now matches expectations, per the
   `verify` skill.

## Example

`EnergyController.ResetForRestart()` and `CampaignDebugTool.Awake()`'s `overrideCurrentEnergy` branch
both write `RunState.currentEnergy`, from two different files, only one of which is obviously related
to "reward doesn't persist." A grep-and-log pass across every write site is what actually found the
real cause (rule 26) after code-reading alone missed it.
