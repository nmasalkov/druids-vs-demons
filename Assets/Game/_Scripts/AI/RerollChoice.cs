public readonly struct RerollChoice
{
    public readonly bool ShouldReroll;
    public readonly int SlotIndex;

    public RerollChoice(bool shouldReroll, int slotIndex)
    {
        ShouldReroll = shouldReroll;
        SlotIndex = slotIndex;
    }
}
