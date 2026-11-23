using Gwent.Engine;
using Xunit;

namespace Gwent.Engine.Tests;

public sealed class CardDefinitionTests
{
    [Fact]
    public void DefinitionCreatesIndependentInstancesWithSharedMetadata()
    {
        var definition = new CardDefinition(
            "vanguard", "Vanguard", 5, "Northern", "Unit", "Silver",
            [Row.Melee, Row.Ranged], "Front line unit");

        var first = definition.CreateInstance(Row.Melee);
        var second = definition.CreateInstance(Row.Ranged);

        Assert.NotEqual(first.InstanceId, second.InstanceId);
        Assert.Equal(first.DefinitionId, second.DefinitionId);
        Assert.Equal("Northern", first.Faction);
        Assert.Equal("Silver", second.Rank);
        Assert.Equal(Row.Melee, first.Row);
        Assert.Equal(Row.Ranged, second.Row);
        Assert.Equal(5, first.BasePower);
    }

    [Fact]
    public void DefinitionRejectsUnsupportedPlacementRow()
    {
        var definition = new CardDefinition(
            "vanguard", "Vanguard", 5, "Northern", "Unit", "Silver",
            [Row.Melee], "Front line unit");

        Assert.Throws<ArgumentException>(() => definition.CreateInstance(Row.Siege));
    }
}
