using System;

public abstract class GameState
{
    /// <summary>Fired right before any state's OnEnter runs. Useful for debug HUDs.</summary>
    public static event Action<GameState> OnAnyStateStarted;
    /// <summary>Fired right after any state's OnExit runs.</summary>
    public static event Action<GameState> OnAnyStateEnded;

    public event Action OnStateCompleted;

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
        OnStateCompleted?.Invoke();
    }
}
