namespace Gwent.Engine;

/// <summary>
/// One physical card entry in a deck list. Repeated definitions are allowed;
/// each entry creates a separate card instance.
/// </summary>
public sealed record DeckCardEntry(CardDefinition Definition, Row Row);

/// <summary>
/// Immutable, validated deck configuration. The leader is metadata and is not
/// shuffled into the player's card pile.
/// </summary>
public sealed class DeckDefinition
{
    public const int MinimumCardCount = 25;

    public DeckDefinition(
        string id,
        CardDefinition leader,
        IEnumerable<DeckCardEntry> cards)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Deck id is required.", nameof(id));
        Leader = leader ?? throw new ArgumentNullException(nameof(leader));
        ArgumentNullException.ThrowIfNull(cards);

        if (!StringComparer.Ordinal.Equals(Leader.Type, "Leader"))
            throw new ArgumentException("Deck leader definition must have Type 'Leader'.", nameof(leader));

        var entries = cards.ToArray();
        if (entries.Length < MinimumCardCount)
            throw new ArgumentException(
                $"A deck must contain at least {MinimumCardCount} cards; received {entries.Length}.",
                nameof(cards));

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index] ??
                throw new ArgumentException($"Deck entry at index {index} cannot be null.", nameof(cards));
            var definition = entry.Definition ??
                throw new ArgumentException($"Card definition at index {index} cannot be null.", nameof(cards));

            if (StringComparer.Ordinal.Equals(definition.Type, "Leader"))
                throw new ArgumentException("Leader cards cannot appear in the main deck.", nameof(cards));
            if (!StringComparer.Ordinal.Equals(definition.Faction, Leader.Faction))
                throw new ArgumentException(
                    $"Card '{definition.Id}' belongs to faction '{definition.Faction}', not leader faction '{Leader.Faction}'.",
                    nameof(cards));
            if (!definition.AllowedRows.Contains(entry.Row))
                throw new ArgumentException(
                    $"Card '{definition.Id}' cannot be assigned to row '{entry.Row}'.",
                    nameof(cards));
        }

        Id = id;
        Cards = Array.AsReadOnly(entries);
    }

    public string Id { get; }
    public CardDefinition Leader { get; }
    public IReadOnlyList<DeckCardEntry> Cards { get; }

    public IReadOnlyList<Card> CreateCardInstances() =>
        Array.AsReadOnly(Cards
            .Select(entry => entry.Definition.CreateInstance(entry.Row))
            .ToArray());
}
