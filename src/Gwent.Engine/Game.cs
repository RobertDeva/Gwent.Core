using System.Collections.ObjectModel;

namespace Gwent.Engine;

public enum Row
{
    Melee,
    Ranged,
    Siege
}

public enum CardZone
{
    Unassigned,
    Deck,
    Hand,
    Field,
    Graveyard
}

public sealed class Card
{
    private Guid? _gameId;
    private string? _ownerId;

    public Card(string definitionId, string name, int power, Row row, string? effectId = null)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
            throw new ArgumentException("Card definition id is required.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card name is required.", nameof(name));
        if (power < 0) throw new ArgumentOutOfRangeException(nameof(power));
        if (!Enum.IsDefined(row)) throw new ArgumentOutOfRangeException(nameof(row));

        InstanceId = Guid.NewGuid();
        DefinitionId = definitionId;
        Name = name;
        BasePower = power;
        Power = power;
        Row = row;
        EffectId = effectId;
    }

    public Guid InstanceId { get; }
    public string DefinitionId { get; }
    public string Name { get; }
    public int BasePower { get; }
    public int Power { get; private set; }
    public Row Row { get; }
    public string? EffectId { get; }
    public CardZone Zone { get; private set; }
    public string? OwnerId => _ownerId;

    internal void Assign(Player owner, Guid gameId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_gameId is not null)
            throw new InvalidOperationException("A card instance already belongs to a game.");
        _gameId = gameId;
        _ownerId = owner.Id;
        Zone = CardZone.Deck;
    }

    internal void MoveTo(CardZone zone)
    {
        if (_gameId is null || _ownerId is null)
            throw new InvalidOperationException("An unassigned card cannot change zones.");
        Zone = zone;
    }

    public void IncreasePower(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Power = checked(Power + amount);
    }
}

public sealed class Board
{
    private readonly Dictionary<Row, List<Card>> _rows;
    private readonly Dictionary<Row, ReadOnlyCollection<Card>> _rowViews;

    internal Board()
    {
        _rows = Enum.GetValues<Row>().ToDictionary(row => row, _ => new List<Card>());
        _rowViews = _rows.ToDictionary(pair => pair.Key, pair => pair.Value.AsReadOnly());
    }

    public IReadOnlyList<Card> GetRow(Row row)
    {
        ValidateRow(row);
        return _rowViews[row];
    }

    public IReadOnlyList<Card> Cards => _rows.Values.SelectMany(cards => cards).ToArray();
    public int Score => _rows.Values.SelectMany(cards => cards).Sum(card => card.Power);

    public int Score(Row row)
    {
        ValidateRow(row);
        return _rows[row].Sum(card => card.Power);
    }

    internal void Add(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        _rows[card.Row].Add(card);
    }

    internal void Remove(Card card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var row = _rows[card.Row];
        var index = row.FindIndex(existing => existing.InstanceId == card.InstanceId);
        if (index < 0)
            throw new InvalidOperationException("Card instance is not on this board.");
        row.RemoveAt(index);
    }

    private static void ValidateRow(Row row)
    {
        if (!Enum.IsDefined(row)) throw new ArgumentOutOfRangeException(nameof(row));
    }
}

public sealed class Player
{
    private readonly List<Card> _deck = [];
    private readonly List<Card> _hand = [];
    private readonly List<Card> _graveyard = [];
    private readonly ReadOnlyCollection<Card> _deckView;
    private readonly ReadOnlyCollection<Card> _handView;
    private readonly ReadOnlyCollection<Card> _graveyardView;

    public Player(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Player id is required.", nameof(id));
        Id = id;
        Board = new Board();
        _deckView = _deck.AsReadOnly();
        _handView = _hand.AsReadOnly();
        _graveyardView = _graveyard.AsReadOnly();
    }

    public string Id { get; }
    public Board Board { get; }
    public IReadOnlyList<Card> Deck => _deckView;
    public IReadOnlyList<Card> Hand => _handView;
    public IReadOnlyList<Card> Field => Board.Cards;
    public IReadOnlyList<Card> Graveyard => _graveyardView;

    internal bool Contains(Card card) =>
        ContainsInstance(_deck, card) || ContainsInstance(_hand, card) ||
        ContainsInstance(Board.Cards, card) || ContainsInstance(_graveyard, card);

    internal Card? DrawTopCard()
    {
        if (_deck.Count == 0) return null;
        var index = _deck.Count - 1;
        var card = _deck[index];
        _deck.RemoveAt(index);
        card.MoveTo(CardZone.Hand);
        _hand.Add(card);
        return card;
    }

    internal void AddToDeck(Card card) => _deck.Add(card);

    internal void PlayFromHand(Card card)
    {
        Remove(_hand, card, "hand");
        card.MoveTo(CardZone.Field);
        Board.Add(card);
    }

    internal void MoveToGraveyard(Card card)
    {
        Board.Remove(card);
        card.MoveTo(CardZone.Graveyard);
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
        if (card.Zone != CardZone.Unassigned)
            throw new InvalidOperationException("Only an unassigned card instance can be added to a deck.");
        card.Assign(player, _id);
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
        if (card.OwnerId != player.Id || card.Zone != CardZone.Hand ||
            !player.Hand.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card in the player's hand can be played.");
        player.PlayFromHand(card);
    }

    public void Destroy(Player player, Card card)
    {
        ValidatePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.OwnerId != player.Id || card.Zone != CardZone.Field ||
            !player.Field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card on the player's field can be moved to the graveyard.");
        player.MoveToGraveyard(card);
    }

    public static int Score(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Board.Score;
    }

    public static int Score(Player player, Row row)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Board.Score(row);
    }

    private void ValidatePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ReferenceEquals(player, First) && !ReferenceEquals(player, Second))
            throw new InvalidOperationException("Player does not belong to this game.");
    }
}
