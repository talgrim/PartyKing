namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>
/// The behavior of every plot card type. A new card needs an entry here (a test fails for a type without one) and
/// the action of its own in <see cref="ResistanceGame"/>.
/// </summary>
internal static class ResistancePlotCardBehaviors
{
    private static readonly IReadOnlyDictionary<ResistancePlotCardType, IResistancePlotCardBehavior> Registered =
        new IResistancePlotCardBehavior[]
        {
            new OverheardConversationBehavior(),
            new OpinionMakerBehavior(),
            new EstablishConfidenceBehavior(),
            new OpenUpBehavior(),
            new ResistancePlainPlotCardBehavior(ResistancePlotCardType.NoConfidence),
            new ResistancePlainPlotCardBehavior(ResistancePlotCardType.KeepingCloseEye),
            new ResistancePlainPlotCardBehavior(ResistancePlotCardType.StrongLeader),
            new InTheSpotlightBehavior(),
            new ResistancePlainPlotCardBehavior(ResistancePlotCardType.TakeResponsibility),
        }.ToDictionary(x => x.Type);

    public static IReadOnlyCollection<IResistancePlotCardBehavior> All => Registered.Values.ToList();

    public static IResistancePlotCardBehavior For(ResistancePlotCardType type)
    {
        return Registered[type];
    }
}
