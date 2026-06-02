/// <summary>
/// Marker state that signals the end of a round.
/// GameManager uses this to decide whether to loop or trigger game over.
/// </summary>
public class EndOfRoundState : GameState
{
    public override void OnStateStart()
    {
        CompleteState();
    }
}

