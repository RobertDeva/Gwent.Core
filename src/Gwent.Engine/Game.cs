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
    private Guid? _gameId;

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

    // InstanceId identifies this physical copy; DefinitionId identifies its reusable template.
    public Guid InstanceId { get; }
    public string DefinitionId { get; }
    public string Name { get; }
    public int BasePower { get; }
    public int Power { get; private set; }
    public Row Row { get; }
    public string? EffectId { get; }

    internal void AssignToGame(Guid gameId)
    {
        if (_gameId is not null && _gameId != gameId)
            throw new InvalidOperationException("A card instance cannot belong to more than one game.");
        _gameId = gameId;
    }

    public void IncreasePower(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Power = checked(Power + amount);
    }
}

public sealed class Player
{
    private readonly List<Card> _deck = [];
    private readonly List<Card> _hand = [];
    private readonly List<Card> _field = [];
    private readonly List<Card> _graveyard = [];
    private readonly ReadOnlyCollection<Card> _deckView;
    private readonly ReadOnlyCollection<Card> _handView;
    private readonly ReadOnlyCollection<Card> _fieldView;
    private readonly ReadOnlyCollection<Card> _graveyardView;

    public Player(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Player id is required.", nameof(id));
        Id = id;
        _deckView = _deck.AsReadOnly();
        _handView = _hand.AsReadOnly();
        _fieldView = _field.AsReadOnly();
        _graveyardView = _graveyard.AsReadOnly();
    }

    public string Id { get; }
    public IReadOnlyList<Card> Deck => _deckView;
    public IReadOnlyList<Card> Hand => _handView;
    public IReadOnlyList<Card> Field => _fieldView;
    public IReadOnlyList<Card> Graveyard => _graveyardView;

    internal bool Contains(Card card) =>
        ContainsInstance(_deck, card) || ContainsInstance(_hand, card) ||
        ContainsInstance(_field, card) || ContainsInstance(_graveyard, card);

    internal Card? DrawTopCard()
    {
        if (_deck.Count == 0) return null;
        var index = _deck.Count - 1;
        var card = _deck[index];
        _deck.RemoveAt(index);
        _hand.Add(card);
        return card;
    }

    internal void AddToDeck(Card card) => _deck.Add(card);

    internal void PlayFromHand(Card card)
    {
        Remove(_hand, card, "hand");
        _field.Add(card);
    }

    internal void MoveToGraveyard(Card card)
    {
        Remove(_field, card, "field");
        _graveyard.Add(card);
    }

    private static bool ContainsInstance(IEnumerable<Card> cards, Card card) =>
        cards.Any(existing => existing.InstanceId == card.InstanceId);

    private static void Remove(List<Card> cards, Card card, string zoneName)
    {
        var index = cards.FindIndex(existing => existing.InstanceId == card.InstanceId);
        if (index < 0)
            throw new InvalidOperationException($"Card instance '{card.InstanceId}' is not in the player's {zoneName}.");
        cards.RemoveAt(index);
    }
}

public sealed class Game
{
    private readonly Guid _id = Guid.NewGuid();

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

    public void AddToDeck(Player player, Card card)
    {
        ValidatePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (ContainsCard(card))
            throw new InvalidOperationException($"Card instance '{card.InstanceId}' is already in this game.");

        // Claim the instance only after all validations and immediately before insertion.
        card.AssignToGame(_id);
        player.AddToDeck(card);
    }

    public Card? Draw(Player player)
    {
        ValidatePlayer(player);
        return player.DrawTopCard();
    }

    public void Play(Player player, Card card)
    {
        ValidatePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (!player.Hand.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card in the player's hand can be played.");

        player.PlayFromHand(card);
    }

    public void Destroy(Player player, Card card)
    {
        ValidatePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (!player.Field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card on the player's field can be moved to the graveyard.");

        player.MoveToGraveyard(card);
    }

    public static int Score(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Field.Sum(card => card.Power);
    }

    private void ValidatePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ReferenceEquals(player, First) && !ReferenceEquals(player, Second))
            throw new InvalidOperationException("Player does not belong to this game.");
    }

    private bool ContainsCard(Card card) =>
        First.Contains(card) || Second.Contains(card);
}
