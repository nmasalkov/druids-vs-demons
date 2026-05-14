# Project Coding Rules

1. **No null-checks on serialized fields.** If a `[SerializeField]` reference is not assigned, let it throw so the error is immediately visible. Do not guard with `if (x != null)`.
2. **Use `Utils.DoAfterDelay` for simple delayed actions.** Instead of writing custom coroutines for one-off delayed calls, use `Utils.DoAfterDelay.Execute(action, delay)`.
3. Use events for communication between components. Avoid direct references when possible to promote loose coupling.
4. **No logic that depends on other scripts in `Awake()`.** `Awake()` is only for self-initialization (caching own components, setting instance references). Any logic that relies on other MonoBehaviours, singletons, or cross-script data must go in `Start()` or later.
5. **No null-checks on mandatory references.** If a field or property is expected to always be set (e.g., `Creature.Slot`, runtime-assigned dependencies), do not guard with `if (x != null)`. Let it throw a `NullReferenceException` so the bug is immediately visible. Only add null-checks when the value is genuinely optional.
