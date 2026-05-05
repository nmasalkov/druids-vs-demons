public enum AIRollDecision { PostRollSlot1, PostRollSlot2, PostRollSlot3, FinishRoll }

public class AIRollController
{
    public AIRollDecision Decide()
    {
        return AIRollDecision.FinishRoll;
    }
}

