using System.Collections.ObjectModel;

namespace Gwent.Engine;

public enum Row
{
    Melee,
    Ranged,
    Siege
}

public sealed class Card
{
    public Card(string definitionId, string name, int power, Row row, string? effectId = null)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            throw new ArgumentException("Card definition id is required.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card name is required.", nameof(name));
        if (power < 0) throw new ArgumentOutOfRangeException(nameof(power));

        InstanceId = Guid.NewGuid();
        DefinitionId = definitionId;
        Name = name;
        BasePower = power;
        Power = power;
        Row = row;
        EffectId = effectId;
    }

    // InstanceId identifies this physical copy; DefinitionId identifies its shared template.
    public Guid InstanceId { get; }
    public string DefinitionId { get; }
    public string Name { get; }
    public int BasePower { get; }
    public int Power { get; private set; }
    public Row Row { get; }
    public string? EffectId { get; }

    public void IncreasePower(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Power = checked(Power + amount);
    }
}

public sealed class Player
{
    private readonly List<Card> _field = [];
    private readonly ReadOnlyCollection<Card> _fieldView;

    public Player(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Player id is required.", nameof(id));
        Id = id;
        _fieldView = _field.AsReadOnly();
    }

    public string Id { get; }
    public IReadOnlyList<Card> Field => _fieldView;

    internal void Add(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException($"Card instance '{card.InstanceId}' is already on this player's field.");
        _field.Add(card);
    }
}

public sealed class Game
{
    public Game(Player first, Player second)
    {
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));
        if (ReferenceEquals(first, second)) throw new ArgumentException("A game requires two distinct players.");
    }

    public Player First { get; }
    public Player Second { get; }

    public Player? Winner
    {
        get
        {
            var first = Score(First);
            var second = Score(Second);
            return first == second ? null : first > second ? First : Second;
        }
    }

    public void Play(Player player, Card card)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(card);
        if (!ReferenceEquals(player, First) && !ReferenceEquals(player, Second))
            throw new InvalidOperationException("Player does not belong to this game.");

        // A physical copy may occupy only one place in a match.
        if (First.Field.Any(existing => existing.InstanceId == card.InstanceId) ||
            Second.Field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException($"Card instance '{card.InstanceId}' is already in this game.");

        player.Add(card);
    }

    public static int Score(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Field.Sum(card => card.Power);
    }
}
