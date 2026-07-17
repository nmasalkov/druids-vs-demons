using System;
using Game._Scripts.Global;

public abstract class GameState
{
    /// <summary>Fired right before any state's OnEnter runs. Useful for debug HUDs.</summary>
    public static event Action<GameState> OnAnyStateStarted;
    /// <summary>Fired right after any state's OnExit runs.</summary>
    public static event Action<GameState> OnAnyStateEnded;

    public event Action OnStateCompleted;

    /// <summary>Restart generation captured when this state was created. Lets a state started
    /// before a battle restart recognize that it's stale and no-op instead of corrupting the
    /// fresh run.</summary>
    protected readonly int Generation = GameManager.Instance.Generation;
    protected bool IsStale => GameManager.IsStale(Generation);

    public void OnStateStart()
    {
        OnAnyStateStarted?.Invoke(this);
        OnEnter();
    }

    public void OnStateEnd()
    {
        OnExit();
        OnAnyStateEnded?.Invoke(this);
    }

    protected virtual void OnEnter() { }
    protected virtual void OnExit() { }

    protected void CompleteState()
    {
        if (IsStale) return;
        OnStateCompleted?.Invoke();
    }
}
