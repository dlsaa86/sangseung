namespace Ascend.Prototype
{
    /// <summary>
    /// Identifies the combination type matched by CombinationResolver.
    /// Priority order (highest → lowest): ContainsLegendary → ThreeOfAKind → SpecificOrder
    /// → CommonAdvancedRare → ThreeSameGrade → ThreeDifferentCommon → None.
    /// </summary>
    public enum CombinationType
    {
        ThreeOfAKind,
        ThreeSameGrade,
        ThreeDifferentCommon,
        CommonAdvancedRare,
        SpecificOrder,
        ContainsLegendary,
        None
    }
}
