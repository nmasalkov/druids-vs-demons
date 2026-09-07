public readonly struct RerollChoice
{
    public readonly bool ShouldReroll;
    public readonly int SlotIndex;
    public readonly ShouldRerollDecision.RerollMode NextMode;
    public readonly bool GrantBonusReroll;

    private RerollChoice(bool shouldReroll, int slotIndex, ShouldRerollDecision.RerollMode nextMode,
        bool grantBonusReroll)
    {
        ShouldReroll = shouldReroll;
        SlotIndex = slotIndex;
        NextMode = nextMode;
        GrantBonusReroll = grantBonusReroll;
    }

    public static RerollChoice Reroll(int slotIndex, ShouldRerollDecision.RerollMode nextMode,
        bool grantBonusReroll = false) => new RerollChoice(true, slotIndex, nextMode, grantBonusReroll);

    public static RerollChoice Finish(ShouldRerollDecision.RerollMode nextMode,
        bool grantBonusReroll = false) => new RerollChoice(false, -1, nextMode, grantBonusReroll);
}
