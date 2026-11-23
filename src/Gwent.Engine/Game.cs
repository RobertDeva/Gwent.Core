namespace Gwent.Engine;

public enum Row
{
    Melee,
    Ranged,
    Siege
}

public sealed class Card
{
    public Card(string id, string name, int power, Row row, string? effectId = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Card id is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Card name is required.", nameof(name));
        if (power < 0) throw new ArgumentOutOfRangeException(nameof(power));
        Id = id;
        Name = name;
        BasePower = power;
        Power = power;
        Row = row;
        EffectId = effectId;
    }

    public string Id { get; }
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

    public Player(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Player id is required.", nameof(id));
        Id = id;
    }

    public string Id { get; }
    public IReadOnlyList<Card> Field => _field;

    internal void Add(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_field.Any(existing => existing.Id == card.Id))
            throw new InvalidOperationException($"Card '{card.Id}' is already on this player's field.");
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

        // A card instance represents one physical card and cannot occupy two fields.
        if (First.Field.Contains(card) || Second.Field.Contains(card))
            throw new InvalidOperationException($"Card '{card.Id}' is already in this game.");

        player.Add(card);
    }

    public static int Score(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Field.Sum(card => card.Power);
    }
}
