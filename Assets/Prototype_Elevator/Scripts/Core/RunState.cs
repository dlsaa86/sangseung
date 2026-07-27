namespace Ascend.Prototype
{
    /// <summary>
    /// All phases of a single floor cycle in the Ascend state machine.
    /// </summary>
    public enum RunState
    {
        FloorArrival,
        PassengerSelection,
        GenerationTurn,
        PowerResolution,
        OverchargeAllocation,
        Ascending
    }
}
