namespace PartyKing.Domain.Entities.Games.Resistance.PlotCards;

/// <summary>
/// The behavior of the plot card types that set themselves apart. Every other type is a plain card, so a type missing
/// here never throws: it is dealt by the leader into a hand and has an action of its own.
/// </summary>
internal static class ResistancePlotCardBehaviors
{
    private static readonly IReadOnlyDictionary<ResistancePlotCardType, IResistancePlotCardBehavior> Behaviors =
        new IResistancePlotCardBehavior[]
        {
            new OverheardConversationBehavior(),
            new OpinionMakerBehavior(),
            new EstablishConfidenceBehavior(),
            new OpenUpBehavior(),
            new InTheSpotlightBehavior(),
        }.ToDictionary(x => x.Type);

    /// <summary>The registered behaviors, one per type.</summary>
    public static IReadOnlyCollection<IResistancePlotCardBehavior> Registered { get; } = Behaviors.Values.ToList();

    public static IResistancePlotCardBehavior For(ResistancePlotCardType type)
    {
        return Behaviors.TryGetValue(type, out var behavior) ? behavior : new ResistancePlainPlotCardBehavior(type);
    }
}
