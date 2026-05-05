# Project Coding Rules

1. **No null-checks on serialized fields.** If a `[SerializeField]` reference is not assigned, let it throw so the error is immediately visible. Do not guard with `if (x != null)`.
2. **Use `Utils.DoAfterDelay` for simple delayed actions.** Instead of writing custom coroutines for one-off delayed calls, use `Utils.DoAfterDelay.Execute(action, delay)`.
3. Use events for communication between components. Avoid direct references when possible to promote loose coupling.
4. **No logic that depends on other scripts in `Awake()`.** `Awake()` is only for self-initialization (caching own components, setting instance references). Any logic that relies on other MonoBehaviours, singletons, or cross-script data must go in `Start()` or later.
