# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

"Druids vs Demons" is a Unity 6 (6000.1.6f1, URP 17.1) 2D auto-battler. Two `HeroView`s (player and
enemy) take turns spinning a slot machine to roll creatures, nukes, or spells, then an automated
battle phase resolves combat between the two sides' summoned creatures.

Game code lives entirely under `Assets/Game/_Scripts` (and `Assets/Game/_ScriptableObjects` for data
assets). Everything else under `Assets/` (Epic Toon FX, PixPlays, Feel/MoreMountains, Spine, All In 1
Sprite Shader, etc.) is vendored third-party/asset-store content — treat it as read-only unless a task
specifically requires changing how it's wired into `Assets/Game`.

There is no custom `.asmdef` for `Assets/Game` — it compiles into the default `Assembly-CSharp`
assembly.

### Robotek terminology mapping

The user often describes mechanics in terms of the game Robotek. Translate as follows:

| Robotek term | This project |
| ------------ | ------------ |
| hack         | Charm (the `CharmSO` spell) |
| robots       | creatures |
| mainframe    | hero |
| droid        | mage |
| drone        | archer |
| tank         | tank |

## Working with this repo

This is a Unity project, not a CLI-buildable one — there are no npm/make/CLI build, lint, or test
commands. Development happens in the Unity Editor (6000.1.6f1):

- Open the project in Unity Editor, enter Play mode on the main scene under `Assets/Game/_Scenes` to
  run the game.
- `com.unity.test-framework` is installed as a package dependency, but no EditMode/PlayMode test
  assemblies currently exist in the repo — there is no automated test suite to run.
- After changing any MonoBehaviour/ScriptableObject serialized fields, the change must be verified/
  wired up in the Editor (see rule 6 below) since there's no way to check this outside Unity.

## Core architecture

### Game loop: `GameManager` + `GameState`

`GameManager` (`Global/GameManager/GameManager.cs`) is a singleton that drives the entire match as a
single coroutine, `RunGameLoop()`. That method is the **single source of truth for round order** —
read it top-to-bottom rather than searching for where states get chained; no state ever dynamically
inserts another state into the sequence.

Flow per round: `SwitchSideState` → `RollState` → (`SpawningState` | `NukeState` | `SpellState`,
chosen polymorphically by `GameManager.CreateActionState()` based on the roll's `SlotMachine.RollType`)
→ optionally re-roll on a triple → `BattleState` → `PostBattleState`, then repeat for the other side,
then `EndOfRoundState`. Round 1 skips the post-player-turn battle (player just summoned, shouldn't
immediately be attacked).

All states derive from the abstract `GameState` (`Global/GameManager/GameState.cs`): `OnEnter()` /
`OnExit()` lifecycle, and `CompleteState()` signals `GameManager.Run()` to advance. `ActionState`
(`Global/GameManager/ActionState.cs`) is the shared base for Nuke/Spell states — it resolves
caster/target sides from `GameManager.ActiveSide`, and owns the generic entry-playback loop
(`PlayEntries`) and the animation-skipping instant-resolve path (`ResolveInstant`).

Hero death is handled out-of-band via the `Hero.OnHeroDied` event, which can interrupt the main
coroutine at any point (`HandleHeroDied` → `GameOverState`).

### `G` — global service locator

`G.cs` (`Global/G.cs`) is a singleton exposing default data SOs (`DefaultCreatures`, `DefaultNukes`,
`DefaultSpells`) and both sides' `HeroView`/`CreaturesManager`/`Hero` as static properties. Most
gameplay code reaches other systems through `G.*` rather than holding direct references.

### Action pattern: SO (data) → Resolver (logic) → Animation (view)

Nukes and spells share one pattern, split strictly along the data/logic/view boundary (this mirrors
coding rule 13 below):

1. **`ActionSO`** (abstract) — the "card": name, sprite, and `AnimationPrefabBase`. `NukeSO`/`SpellSO`
   subclasses (e.g. `FireMagicSO`, `ShieldSO`, `StarfallSO`) hold balance numbers (damage per level,
   targeting rules) and implement `CreateAndResolve(ActionContext, level)`.
2. **`ActionResolver`** (abstract, `ApplyInstant()`) — pure data layer. A concrete resolver (e.g.
   `FireMagicResolver`, `ShieldResolver`) computes targets/damage/shots up front against an
   `ActionContext` (caster, enemy creatures/hero/shield) with no animation or timing involved.
3. **`ActionAnimation`** (abstract MonoBehaviour, `Execute(source, caster, resolver, onComplete)`) —
   view layer only. Instantiated per entry, downcasts the resolver to its typed shots and plays
   visuals, then calls `onComplete`.

`ActionState.PlayEntries` drives this per rolled entry: build `ActionContext` → `CreateAndResolve` →
instantiate the animation prefab → wait for `onComplete` → pause → next entry → `CompleteWithCleanup()`
(removes dead creatures on both sides). `ActionState.ResolveInstant` is the non-animated equivalent
used for instant resolution (rule 7) — same resolve step, straight to `ApplyInstant()`, no view layer.

Placeholder SOs without an assigned `AnimationPrefabBase` are skipped with a warning rather than
throwing, since balance-only WIP SO assets are expected to exist mid-development.

### Slot machine

`SlotMachine.cs` drives a multi-column reel (`SlotColumn`) with three roll type tabs
(`RollType.Creature/Nuke/Spell`, selectable only in `MachineState.FirstRoll`). Each `SlotColumn`
independently spins/stops and reports its landed `ActionSO` as `WinningAction`. After all columns
stop, the machine enters `PostRolls` (allowing per-column reroll) unless all three columns already
match (`IsTriple()`), in which case it auto-finishes. `FinishRoll()` fires
`OnFinishRollCompleted(List<ActionSO>, RollType)`, which `RollStateManager` consumes to build the
entries (`IActionEntry`) that `ActionState` plays.

### Units

`Targetable` → `Unit` (abstract, `[RequireComponent(StatusesManager)]`, exposes `Animator`/
`StatusesManager`, requires subclasses to implement `GetMaxHealth()`) → `Creature` (has `CreatureSO`
data + `Experience`) / `Hero`. `Shield` is a `Targetable` directly (not a `Unit`) — a summoned
defensive construct in `HeroView.ShieldSlot`, highest-priority target for most nukes/melee, no XP on
kill.

`Health` is a standalone component (`TakeDamage`/`Heal`/`IsDead`) with a `PostponeDeath` flag so
attackers (e.g. Tank) can finish their attack animation before the death animation plays
(`ExecutePostponedDeath()`).

Stats/balance for creatures live on `CreatureSO` (indexed by `Experience.Level`, e.g.
`Data.Stats(Experience.Level).health`), not on the `Creature` MonoBehaviour itself, per the
data/view separation rule.

## Project coding rules

These are load-bearing conventions for this codebase (from `.github/copilot-instructions.md`) — follow
them for any new/modified game code under `Assets/Game`:

1. **No null-checks on serialized `[SerializeField]` fields.** Let missing references throw so the bug
   is immediately visible.
2. **Use `Utils.DoAfterDelay.Execute(action, delay)`** for one-off delayed calls instead of writing a
   custom coroutine.
3. **Prefer events over direct references** between components for loose coupling.
4. **No cross-script logic in `Awake()`.** `Awake()` is self-initialization only (caching own
   components, singleton `Instance` assignment). Anything depending on other MonoBehaviours or
   singletons goes in `Start()` or later.
5. **No null-checks on mandatory references** (e.g. `Creature.Slot`) — let them throw. Only guard
   values that are genuinely optional.
6. **After complex changes / new serialized fields**, always give a checklist of what needs manual
   Editor setup (components to add, fields to assign, SO assets to update).
7. **Every animated/delayed game mechanic needs an instant-resolve counterpart** that skips animation,
   so tests/automation can run without waiting (see `NukeState.ResolveNukesInstant()`,
   `ActionState.ResolveInstant`, `BattleState.ResolveBattleInstant()`,
   `ExperienceManager.ResolveGemsInstant()`). The end goal: the **whole game cycle** must be able to
   switch into instant mode and run hundreds of automated test fights headlessly (no visual playback)
   to gather balance data — so an outcome must never depend on its animation. Keep all data mutation
   in resolvers/shots (`Apply()`), never inside animation callbacks, so both paths reach the exact
   same end state.
8. **Use the new Input System** (`UnityEngine.InputSystem`, e.g. `Keyboard.current.jKey...`), never the
   legacy `Input` class.
9. **Cache `[RequireComponent]` sibling references in `Awake()`** and reuse the cached field — never
   repeat `GetComponent` calls at runtime.
10. **Use `[RequireComponent]`** for components a GameObject will essentially always need (e.g. `Health`
    on a unit).
11. **Avoid nested `if`s, especially in loops** — extract methods, use guard clauses/early returns.
12. **Nest small, single-owner enums inside their owning class** (e.g. `SlotMachine.RollType`,
    `SlotMachine.MachineState`); only promote to top-level if multiple unrelated classes need it.
13. **Data/view separation.** Balance numbers (damage, cooldowns, costs, durations, prefab lookup
    tables) belong on `ScriptableObject`s, never on view/animation MonoBehaviours, which hold only
    presentation data (timings, VFX prefabs, audio, transform offsets). Prefer an SO asset over a
    scene MonoBehaviour for any data not tied to a live scene object.
14. **Base-class-owned helper components**, cached once in the base's `Awake()`:
    - Sibling on the same GameObject → `[RequireComponent]` + `GetComponent<T>()`.
    - Dedicated child GameObject by convention → no `[RequireComponent]`; `GetComponentInChildren<T>()`
      and let it throw if missing (rule 5). Example: `HitFeedback` lives on a child GO named
      `HitFeedback`, cached via `Unit.Awake()`.
15. **Play particles through Feel feedbacks.** Gameplay code never calls `ParticleSystem.Play()`
    directly — every particle effect is wired to an `MMF_Player` (with an `MMF_Particles` feedback
    bound to the system) and triggered via `PlayFeedbacks()` / `StopFeedbacks()` (plus the
    `Stop(true, StopEmittingAndClear)` residue fix when force-stopping looping effects). See
    `StatusesManager` for the pattern — it owns the serialized `MMF_Player` refs for every unit
    status/attempt effect. Also: particle systems on units must
    use main-module **Scaling Mode = Local**, not Hierarchy — the enemy side is mirrored via
    `localScale.x = -1`, and Hierarchy-scaled Billboard/Mesh particles inherit the negative scale
    and render invisible (verified live: identical simulation, nothing drawn).

## Editor / IDE MCP integrations

Setup status in this environment (last confirmed 2026-07-07): **Coplay MCP is connected and verified
live** — registered at user scope (`claude mcp get coplay-mcp` → Connected), `com.coplaydev.coplay` in
`Packages/manifest.json`, and `mcp__coplay-mcp__get_unity_editor_state` returns real live editor state.
**JetBrains MCP is still not set up** — installed Rider is 2024.3.10 (build 243.28141.39); a Rider
update was applied but did not cross the 2025.2 line the built-in MCP Server plugin requires, and no
MCP-related plugin is present under `%APPDATA%\JetBrains\Rider2024.3\plugins`. Re-check the installed
version (`product-info.json` under the Rider install dir, e.g. `C:\Program Files\JetBrains\JetBrains
Rider 2024.3\product-info.json`) after any future Rider update before assuming `mcp__jetbrains__*` is
usable.

### Coplay MCP (Unity Editor Access)

The Coplay MCP server (`mcp__coplay-mcp__*`) gives direct access to the running Unity Editor. Use it
whenever a task would otherwise require the user to manually run something in Unity. Prefer it over
guessing at editor state.

**RULE: MCP tools are mandatory. `execute_script` is the absolute last resort.**
Before every editor action, scan the tool list below. If a dedicated tool exists, you MUST use it — no
exceptions, no "the dedicated tool might be slower / more verbose / has a known quirk".
`execute_script` is only allowed when no dedicated tool covers the case (e.g. reading editor-only
state not exposed elsewhere, batch ops across many objects with no per-object tool, calling APIs with
no MCP wrapper). A known quirk in a dedicated tool does not promote `execute_script` to default — use
the dedicated tool first and only fall back if it actually fails for that call. Don't announce tool
choice ("using execute_script per the gotcha…") — just use the right tool.

- Compile / errors / logs: `check_compile_errors`, `get_unity_logs`, `get_unity_editor_state`
- Scene + hierarchy: `list_game_objects_in_hierarchy`, `get_game_object_info`, `open_scene`,
  `save_scene` (⚠️ pass full asset path as `scene_name` — see Gotchas), `create_scene`
- GameObjects: `create_game_object`, `duplicate_game_object`, `delete_game_object`,
  `parent_game_object`, `rename_game_object`, `set_transform`, `set_layer`, `set_tag`, `set_property`
- Components: `add_component`, `remove_component`, `set_property` (use for `SerializedField` values)
- Prefabs / assets: `create_prefab`, `create_prefab_variant`, `add_nested_object_to_prefab`,
  `place_asset_in_scene`, `duplicate_asset`, `rename_asset`, `list_all_prefabs_with_bounding_boxes`
- UI Toolkit / UGUI: `create_ui_element`, `set_ui_layout`, `set_ui_text`, `set_rect_transform`,
  `create_panel_settings_asset`, `capture_ui_canvas`
- Materials / shaders / sprites: `create_material`, `assign_material`, `assign_material_to_fbx`,
  `assign_shader_to_material`
- Animation: `create_animation_clip`, `create_animator_controller`, `modify_animator_controller`,
  `create_blend_tree_state`, `set_animation_curves`, `set_animation_clip_settings`,
  `apply_animation_to_rigged_model`, `auto_rig_3d_model`, `list_model_animation_clips`,
  `search_animation_library`
- Input System: `create_input_action_asset`, `add_action_map`, `add_action`, `add_bindings`,
  `add_composite_binding`, `add_control_scheme`, `generate_input_action_wrapper_code`, plus matching
  `remove_*` / `rename_*`
- Generation (AI assets): `generate_3d_model_from_text`, `generate_3d_model_from_image`,
  `generate_3d_model_texture`, `generate_or_edit_images`, `generate_music`, `generate_sfx`,
  `generate_tts`
- Files / search inside project: `read_file`, `list_files`, `search_files`,
  `list_code_definition_names`
- Packages: `list_packages`, `search_installed_packages`, `search_all_packages`,
  `install_unity_package`, `install_git_package`, `remove_unity_package`
- Scene view / capture: `capture_scene_object`, `scene_view_functions`
- Profiling: `get_worst_cpu_frames`, `get_worst_gc_frames`, `list_objects_with_high_polygon_count`
- Play mode: `play_game`, `stop_game`
- Scripts (escape hatch): `execute_script` runs arbitrary C# in the editor — use for anything not
  covered by a dedicated tool
- Project root: `list_unity_project_roots`, `set_unity_project_root` (call once if Coplay points at a
  different project)

**When to use:**
- Verifying compile state after edits → `check_compile_errors` (avoid asking the user to "check the
  console")
- Inspecting hierarchy / component values → `list_game_objects_in_hierarchy` +
  `get_game_object_info`
- Wiring up scenes, prefabs, components → use the dedicated tools instead of writing setup
  instructions for the user
- Saving the active scene → `save_scene` with full asset path (see Gotchas)
- `execute_script` → only when no dedicated tool fits (e.g., reading editor-only state, batch
  operations across many objects, calling APIs not exposed as MCP tools)

**Gotchas:**
- `save_scene` quirk: `scene_name` requires a full asset path, not a bare name. Call `save_scene`
  with the full path first. Only fall back to `execute_script` + `EditorSceneManager.SaveScene` if
  that specific call fails.
- Tools mutate the live editor. Treat them with the same care as editing files: confirm destructive
  operations (deleting GameObjects, removing components, overwriting assets) when intent is unclear.

### JetBrains MCP (Rider / IDE Access)

The JetBrains MCP server (`mcp__jetbrains__*`) connects to the running Rider/IntelliJ IDE. Use it for
code reading, search, and refactoring instead of raw grep/Read/Edit whenever a JetBrains tool fits —
it's indexed, language-aware, and updates references correctly.

**RULE: Prefer JetBrains MCP tools for code work over generic file tools.**
- Renaming a symbol → `rename_refactoring` (updates all references project-wide). Never do a manual
  find-replace on a class/method/field name.
- Searching code → `search_in_files_by_text` / `search_in_files_by_regex` (indexed, much faster than
  shell grep).
- Finding files → `find_files_by_name_keyword` (indexed, very fast) or `find_files_by_glob` for
  patterns.
- Reading a file you already know → `get_file_text_by_path` is fine, but `Read` is equivalent — pick
  either.
- Understanding a symbol at a position → `get_symbol_info` (Quick Documentation: type, signature,
  declaration).
- Editing a known string → `replace_text_in_file` (auto-saves the file; good for surgical edits).
  Plain `Edit` also works.
- Checking errors on a file → `get_file_problems` (IntelliJ inspections, errors + warnings).
- Building / validating after edits → `build_project` (for non-Unity-managed code; for Unity scripts
  use Coplay's `check_compile_errors`).
- Project shape → `list_directory_tree`, `get_project_modules`, `get_all_open_file_paths`.

**When NOT to use JetBrains MCP:**
- Anything touching the live Unity Editor state (scenes, GameObjects, components, assets) → use
  Coplay MCP instead. JetBrains only sees the file system / solution.
- Compile-checking Unity scripts → Coplay's `check_compile_errors` reflects the actual Unity domain
  reload; JetBrains' `build_project` doesn't run Unity's compile pipeline.

**Quick decision:** editing/searching code on disk → JetBrains MCP. Anything inside the running Unity
Editor → Coplay MCP. Shell / non-project files → Bash + Read/Edit.
