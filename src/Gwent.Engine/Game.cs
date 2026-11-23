using System.Collections.ObjectModel;

namespace Gwent.Engine;

public enum Row { Melee, Ranged, Siege }
public enum CardZone { Unassigned, Deck, Hand, Field, Graveyard }
public enum EffectDuration { Instant, WhileSourceOnField, UntilRoundEnd }
public enum EffectOperation { Add, Set, Multiply }
public enum EffectTargetScope { SourceCard, OwnRow, BothPlayersInRow }

public sealed record CardEffect
{
    public CardEffect(
        string id,
        int powerIncrease,
        EffectDuration duration = EffectDuration.Instant,
        EffectOperation operation = EffectOperation.Add,
        EffectTargetScope targetScope = EffectTargetScope.SourceCard)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Effect id is required.", nameof(id));
        if (powerIncrease < 0) throw new ArgumentOutOfRangeException(nameof(powerIncrease));
        if (!Enum.IsDefined(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (!Enum.IsDefined(targetScope)) throw new ArgumentOutOfRangeException(nameof(targetScope));
        Id = id;
        PowerIncrease = powerIncrease;
        Duration = duration;
        Operation = operation;
        TargetScope = targetScope;
    }

    public string Id { get; }
    public int PowerIncrease { get; }
    public EffectDuration Duration { get; }
    public EffectOperation Operation { get; }
    public EffectTargetScope TargetScope { get; }
}

public sealed class Card
{
    private Guid? _gameId;
    private string? _ownerId;
    private readonly HashSet<string> _appliedEffects = new(StringComparer.Ordinal);
    private readonly List<PowerModifier> _modifiers = [];

    private sealed record PowerModifier(
        Guid? SourceInstanceId, string EffectId, EffectOperation Operation, int Amount,
        bool UntilRoundEnd = false);

    public Card(string definitionId, string name, int power, Row row, string? effectId = null)
    {
        if (string.IsNullOrWhiteSpace(definitionId)) throw new ArgumentException("Card definition id is required.", nameof(definitionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Card name is required.", nameof(name));
        if (power < 0) throw new ArgumentOutOfRangeException(nameof(power));
        if (!Enum.IsDefined(row)) throw new ArgumentOutOfRangeException(nameof(row));
        InstanceId = Guid.NewGuid();
        DefinitionId = definitionId;
        Name = name;
        BasePower = power;
        Row = row;
        EffectId = effectId;
    }

    public Guid InstanceId { get; }
    public string DefinitionId { get; }
    public string Name { get; }
    public int BasePower { get; }
    public int Power
    {
        get
        {
            var power = BasePower;
            foreach (var modifier in _modifiers)
            {
                power = modifier.Operation switch
                {
                    EffectOperation.Add => checked(power + modifier.Amount),
                    EffectOperation.Set => modifier.Amount,
                    EffectOperation.Multiply => checked(power * modifier.Amount),
                    _ => throw new InvalidOperationException("Unsupported power modifier operation.")
                };
            }
            return power;
        }
    }
    public Row Row { get; }
    public string? EffectId { get; }
    public CardZone Zone { get; private set; }
    public string? OwnerId => _ownerId;

    internal void Assign(Player owner, Guid gameId)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_gameId is not null) throw new InvalidOperationException("A card instance already belongs to a game.");
        _gameId = gameId;
        _ownerId = owner.Id;
        Zone = CardZone.Deck;
    }

    internal void MoveTo(CardZone zone)
    {
        if (_gameId is null || _ownerId is null) throw new InvalidOperationException("An unassigned card cannot change zones.");
        Zone = zone;
    }

    internal bool TryApplyEffect(CardEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        if (!StringComparer.Ordinal.Equals(EffectId, effect.Id))
            throw new InvalidOperationException($"Effect '{effect.Id}' is not assigned to card '{DefinitionId}'.");
        if (!_appliedEffects.Add(effect.Id))
            throw new InvalidOperationException($"Effect '{effect.Id}' has already been applied to this card instance.");

        if (effect.Duration == EffectDuration.Instant)
            _modifiers.Add(new PowerModifier(null, effect.Id, effect.Operation, effect.PowerIncrease));
        else if (effect.Duration == EffectDuration.UntilRoundEnd)
            _modifiers.Add(new PowerModifier(null, effect.Id, effect.Operation, effect.PowerIncrease, UntilRoundEnd: true));
        return true;
    }

    internal void SetPersistentModifier(Card source, CardEffect effect)
    {
        if (_modifiers.Any(modifier => modifier.SourceInstanceId == source.InstanceId &&
                                       StringComparer.Ordinal.Equals(modifier.EffectId, effect.Id)))
            return;
        _modifiers.Add(new PowerModifier(source.InstanceId, effect.Id, effect.Operation, effect.PowerIncrease));
    }

    internal void ApplyRuntimeModifier(
        string effectId, EffectOperation operation, int amount, EffectDuration duration)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            throw new ArgumentException("Effect id is required.", nameof(effectId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(operation)) throw new ArgumentOutOfRangeException(nameof(operation));
        if (duration == EffectDuration.WhileSourceOnField)
            throw new ArgumentException("Source-bound modifiers must be registered through ApplyEffect.", nameof(duration));
        if (!Enum.IsDefined(duration)) throw new ArgumentOutOfRangeException(nameof(duration));

        _modifiers.Add(new PowerModifier(null, effectId, operation, amount,
            UntilRoundEnd: duration == EffectDuration.UntilRoundEnd));
    }

    internal void RemovePersistentModifiers()
    {
        _modifiers.RemoveAll(modifier => modifier.SourceInstanceId is not null);
    }

    internal void RemoveRoundModifiers()
    {
        _modifiers.RemoveAll(modifier => modifier.UntilRoundEnd);
    }

    internal void RemovePersistentModifier(Guid sourceInstanceId)
    {
        _modifiers.RemoveAll(modifier => modifier.SourceInstanceId == sourceInstanceId);
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

    public int ScoreForRow(Row row)
    {
        ValidateRow(row);
        return _rows[row].Sum(card => card.Power);
    }

    internal void Add(Card card) => _rows[card.Row].Add(card);

    internal void Remove(Card card)
    {
        var row = _rows[card.Row];
        var index = row.FindIndex(existing => existing.InstanceId == card.InstanceId);
        if (index < 0) throw new InvalidOperationException("Card instance is not on this board.");
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
        if (card.Zone == CardZone.Field) Board.Remove(card);
        else if (card.Zone == CardZone.Hand) Remove(_hand, card, "hand");
        else if (card.Zone == CardZone.Deck) Remove(_deck, card, "deck");
        else throw new InvalidOperationException("Card is not in a movable zone.");
        card.MoveTo(CardZone.Graveyard);
        _graveyard.Add(card);
    }

    internal void ClearFieldToGraveyard()
    {
        foreach (var card in Board.Cards) MoveToGraveyard(card);
    }

    private static void Remove(List<Card> cards, Card card, string zoneName)
    {
        var index = cards.FindIndex(existing => existing.InstanceId == card.InstanceId);
        if (index < 0) throw new InvalidOperationException($"Card instance '{card.InstanceId}' is not in the player's {zoneName}.");
        cards.RemoveAt(index);
    }
}

public sealed class Game
{
    private sealed record ActiveEffect(Card Source, CardEffect Effect);

    private readonly Guid _id = Guid.NewGuid();
    private readonly HashSet<Player> _passedPlayers = [];
    private readonly List<ActiveEffect> _activeEffects = [];
    private Player _roundStarter;
    private bool _hasPlayedThisTurn;

    public Game(Player first, Player second)
    {
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));
        if (ReferenceEquals(first, second)) throw new ArgumentException("A game requires two distinct players.");
        if (StringComparer.Ordinal.Equals(first.Id, second.Id))
            throw new ArgumentException("Player IDs must be unique within a game.");
        _roundStarter = First;
        CurrentPlayer = First;
    }

    public Player First { get; }
    public Player Second { get; }
    public Player CurrentPlayer { get; private set; }
    public int RoundNumber { get; private set; } = 1;
    public int FirstRoundsWon { get; private set; }
    public int SecondRoundsWon { get; private set; }
    public Player? LastRoundWinner { get; private set; }
    public bool IsFinished { get; private set; }
    public Player? Winner => IsFinished
        ? FirstRoundsWon == SecondRoundsWon ? null : FirstRoundsWon > SecondRoundsWon ? First : Second
        : null;

    public int RoundsWon(Player player)
    {
        ValidatePlayer(player);
        return ReferenceEquals(player, First) ? FirstRoundsWon : SecondRoundsWon;
    }

    public void AddToDeck(Player player, Card card)
    {
        ValidatePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.Zone != CardZone.Unassigned) throw new InvalidOperationException("Only an unassigned card instance can be added to a deck.");
        card.Assign(player, _id);
        player.AddToDeck(card);
    }

    public Card? Draw(Player player)
    {
        ValidateActivePlayer(player);
        return player.DrawTopCard();
    }

    public void Play(Player player, Card card)
    {
        ValidateActivePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.OwnerId != player.Id || card.Zone != CardZone.Hand ||
            !player.Hand.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card in the active player's hand can be played.");
        if (_hasPlayedThisTurn) throw new InvalidOperationException("A player can play only one card per turn.");
        player.PlayFromHand(card);
        _hasPlayedThisTurn = true;
        RefreshPersistentEffects();
    }

    public void Destroy(Player player, Card card)
    {
        ValidateActivePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        if (card.OwnerId != player.Id || card.Zone != CardZone.Field ||
            !player.Field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Only a card on the active player's field can be destroyed.");
        DeactivateEffectsFrom(card);
        card.RemovePersistentModifiers();
        card.RemoveRoundModifiers();
        player.MoveToGraveyard(card);
        RefreshPersistentEffects();
    }

    public void ApplyEffect(Player player, Card card, CardEffect effect)
    {
        ValidateActivePlayer(player);
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(effect);
        if (card.OwnerId != player.Id || card.Zone != CardZone.Field ||
            !player.Field.Any(existing => existing.InstanceId == card.InstanceId))
            throw new InvalidOperationException("Effects can only be applied by a card on the active player's field.");

        card.TryApplyEffect(effect);
        if (effect.Duration == EffectDuration.WhileSourceOnField)
        {
            _activeEffects.Add(new ActiveEffect(card, effect));
            RefreshPersistentEffects();
        }
    }

    public void ApplyPowerModifier(
        Player player,
        Card target,
        string effectId,
        EffectOperation operation,
        int amount,
        EffectDuration duration = EffectDuration.UntilRoundEnd)
    {
        ValidateActivePlayer(player);
        ArgumentNullException.ThrowIfNull(target);
        if (!First.Field.Concat(Second.Field).Any(card => card.InstanceId == target.InstanceId))
            throw new InvalidOperationException("Power modifiers can only target a card currently on the field.");
        target.ApplyRuntimeModifier(effectId, operation, amount, duration);
    }

    public void EndTurn()
    {
        EnsureNotFinished();
        var other = Other(CurrentPlayer);
        if (!_passedPlayers.Contains(other)) CurrentPlayer = other;
        _hasPlayedThisTurn = false;
    }

    public void Pass(Player player)
    {
        ValidateActivePlayer(player);
        _passedPlayers.Add(player);
        var other = Other(player);
        if (_passedPlayers.Contains(other))
        {
            ResolveRound();
            return;
        }
        CurrentPlayer = other;
        _hasPlayedThisTurn = false;
    }

    public static int Score(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Board.Score;
    }

    public static int Score(Player player, Row row)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Board.ScoreForRow(row);
    }

    private void DeactivateEffectsFrom(Card source)
    {
        _activeEffects.RemoveAll(active => active.Source.InstanceId == source.InstanceId);
        foreach (var card in First.Field.Concat(Second.Field))
            card.RemovePersistentModifier(source.InstanceId);
    }

    private void RefreshPersistentEffects()
    {
        var field = First.Field.Concat(Second.Field).ToArray();
        foreach (var card in field) card.RemovePersistentModifiers();

        foreach (var active in _activeEffects)
        {
            if (active.Source.Zone != CardZone.Field) continue;
            foreach (var target in SelectTargets(active.Source, active.Effect))
                target.SetPersistentModifier(active.Source, active.Effect);
        }
    }

    private IEnumerable<Card> SelectTargets(Card source, CardEffect effect)
    {
        if (effect.TargetScope == EffectTargetScope.SourceCard)
            return [source];

        var owner = StringComparer.Ordinal.Equals(source.OwnerId, First.Id) ? First : Second;
        var candidates = effect.TargetScope == EffectTargetScope.OwnRow
            ? owner.Field
            : First.Field.Concat(Second.Field).ToArray();

        return candidates.Where(card => card.InstanceId != source.InstanceId && card.Row == source.Row);
    }

    private void ResolveRound()
    {
        var firstScore = Score(First);
        var secondScore = Score(Second);
        if (firstScore > secondScore)
        {
            FirstRoundsWon++;
            LastRoundWinner = First;
            _roundStarter = Second;
        }
        else if (secondScore > firstScore)
        {
            SecondRoundsWon++;
            LastRoundWinner = Second;
            _roundStarter = First;
        }
        else
        {
            FirstRoundsWon++;
            SecondRoundsWon++;
            LastRoundWinner = null;
            _roundStarter = ReferenceEquals(_roundStarter, First) ? Second : First;
        }

        foreach (var card in First.Field.Concat(Second.Field))
        {
            card.RemovePersistentModifiers();
            card.RemoveRoundModifiers();
        }
        _activeEffects.Clear();
        First.ClearFieldToGraveyard();
        Second.ClearFieldToGraveyard();
        _passedPlayers.Clear();
        _hasPlayedThisTurn = false;

        IsFinished = FirstRoundsWon >= 2 || SecondRoundsWon >= 2 || RoundNumber >= 3;
        if (!IsFinished)
        {
            RoundNumber++;
            CurrentPlayer = _roundStarter;
        }
    }

    private Player Other(Player player) => ReferenceEquals(player, First) ? Second : First;

    private void ValidateActivePlayer(Player player)
    {
        ValidatePlayer(player);
        EnsureNotFinished();
        if (!ReferenceEquals(player, CurrentPlayer)) throw new InvalidOperationException("It is not this player's turn.");
        if (_passedPlayers.Contains(player)) throw new InvalidOperationException("A player who passed cannot act again this round.");
    }

    private void ValidatePlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ReferenceEquals(player, First) && !ReferenceEquals(player, Second))
            throw new InvalidOperationException("Player does not belong to this game.");
    }

    private void EnsureNotFinished()
    {
        if (IsFinished) throw new InvalidOperationException("The game has already finished.");
    }
}
