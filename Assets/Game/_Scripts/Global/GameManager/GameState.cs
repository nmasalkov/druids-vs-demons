using System;

public abstract class GameState
{
    public event Action OnStateCompleted;

    public virtual void OnStateStart() { }
    public virtual void OnStateEnd() { }

    protected void CompleteState()
    {
        OnStateCompleted?.Invoke();
    }
}

