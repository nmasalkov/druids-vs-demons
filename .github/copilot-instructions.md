# Project Coding Rules

1. **No null-checks on serialized fields.** If a `[SerializeField]` reference is not assigned, let it throw so the error is immediately visible. Do not guard with `if (x != null)`.
2. **Use `Utils.DoAfterDelay` for simple delayed actions.** Instead of writing custom coroutines for one-off delayed calls, use `Utils.DoAfterDelay.Execute(action, delay)`.

