using Gwent.Engine;

using System.Collections.ObjectModel;

namespace Gwent.Compiler;

/// <summary>
/// Runtime collection value. It is a local snapshot unless created from a player zone,
/// in which case mutations are routed through validated Game zone transitions.
/// </summary>
public sealed class RuntimeCardCollection
{
    private readonly List<Card> _cards;
    private readonly ReadOnlyCollection<Card> _view;
    private readonly RuntimeExecutionContext? _context;
    private readonly Player? _owner;
    private readonly CardZone? _zone;
    private readonly bool _readOnly;

    public RuntimeCardCollection(IEnumerable<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);
        _cards = cards.ToList();
        _view = _cards.AsReadOnly();
    }

    private RuntimeCardCollection(RuntimeExecutionContext context, Player owner, CardZone zone)
    {
        _context = context;
        _owner = owner;
        _zone = zone;
        _cards = ReadZone().ToList();
        _view = _cards.AsReadOnly();
    }

    private RuntimeCardCollection(IEnumerable<Card> cards, bool readOnly)
    {
        _cards = cards.ToList();
        _view = _cards.AsReadOnly();
        _readOnly = readOnly;
    }

    public IReadOnlyList<Card> Cards => IsZoneBound ? ReadZone().ToArray() : _view;
    public int Count => IsZoneBound ? ReadZone().Count() : _cards.Count;

    public static RuntimeCardCollection ForZone(
        RuntimeExecutionContext context, Player owner, CardZone zone)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(owner);
        if (context.Game is null) return ReadOnly(ReadZoneWithoutGame(owner, zone));
        return new RuntimeCardCollection(context, owner, zone);
    }

    public static RuntimeCardCollection ReadOnly(IEnumerable<Card> cards) =>
        new(cards, readOnly: true);

    public void Add(Card card)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(card);
        if (IsZoneBound)
        {
            if (card.Zone != _zone || !StringComparer.Ordinal.Equals(card.OwnerId, _owner!.Id))
                _context!.Game!.MoveCard(card, _owner!, _zone!.Value, DeckPosition.Top);
            else if (!_context!.Game!.ContainsCard(card))
                throw new InvalidOperationException("Card instance does not belong to this game.");
        }
        else
            _cards.Add(card);
        Refresh();
    }

    public void Push(Card card)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(card);
        if (IsZoneBound)
            _context!.Game!.MoveCard(card, _owner!, _zone!.Value, DeckPosition.Top);
        else
        {
            _cards.RemoveAll(existing => existing.InstanceId == card.InstanceId);
            _cards.Add(card);
        }
        Refresh();
    }

    public void SendBottom(Card card)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(card);
        if (IsZoneBound && _zone == CardZone.Deck)
            _context!.Game!.MoveCard(card, _owner!, CardZone.Deck, DeckPosition.Bottom);
        else if (IsZoneBound)
            _context!.Game!.MoveCard(card, _owner!, _zone!.Value, DeckPosition.Bottom);
        else
        {
            _cards.RemoveAll(existing => existing.InstanceId == card.InstanceId);
            _cards.Insert(0, card);
        }
        Refresh();
    }

    public void Remove(Card card)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(card);
        if (IsZoneBound)
        {
            if (card.Zone != _zone ||
                !StringComparer.Ordinal.Equals(card.OwnerId, _owner!.Id))
                return;

            var destination = _zone == CardZone.Graveyard
                ? CardZone.Unassigned
                : CardZone.Graveyard;
            _context!.Game!.MoveCard(card, _owner!, destination);
        }
        else
            _cards.RemoveAll(existing => existing.InstanceId == card.InstanceId);
        Refresh();
    }

    public Card? Pop()
    {
        EnsureMutable();
        var currentCards = Cards;
        if (currentCards.Count == 0) return null;
        var card = currentCards[^1];
        if (IsZoneBound)
            _context!.Game!.MoveCard(card, _owner!, CardZone.Unassigned);
        else
            _cards.RemoveAt(_cards.Count - 1);
        Refresh();
        return card;
    }

    public void Shuffle(Random random)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(random);
        if (IsZoneBound)
            _context!.Game!.ShuffleZone(_owner!, _zone!.Value, random);
        else
        {
            for (var i = _cards.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
        Refresh();
    }

    private bool IsZoneBound => _context is not null && _owner is not null && _zone is not null;

    private IEnumerable<Card> ReadZone() => _zone switch
    {
        CardZone.Deck => _owner!.Deck,
        CardZone.Hand => _owner!.Hand,
        CardZone.Field => _owner!.Field,
        CardZone.Graveyard => _owner!.Graveyard,
        _ => throw new ArgumentOutOfRangeException(nameof(_zone))
    };

    private void Refresh()
    {
        if (!IsZoneBound) return;
        _cards.Clear();
        _cards.AddRange(ReadZone());
    }

    private void EnsureMutable()
    {
        if (_readOnly)
            throw new InvalidOperationException("This card collection is read-only.");
    }

    private static IEnumerable<Card> ReadZoneWithoutGame(Player owner, CardZone zone) => zone switch
    {
        CardZone.Deck => owner.Deck,
        CardZone.Hand => owner.Hand,
        CardZone.Field => owner.Field,
        CardZone.Graveyard => owner.Graveyard,
        _ => throw new ArgumentOutOfRangeException(nameof(zone))
    };
}
