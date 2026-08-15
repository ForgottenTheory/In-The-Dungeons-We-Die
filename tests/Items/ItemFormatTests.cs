using Dungeons.Items;
using Xunit;

namespace Dungeons.Tests.Items;

public class ItemFormatTests
{
    [Fact]
    public void InstanceLabel_IncludesNameIdAndProperties()
    {
        var instance = new ItemInstance
        {
            InstanceId = 7,
            BaseDefinitionId = "material.barkbound_iron",
            ItemType = ItemType.Material,
            DisplayName = "Barkbound Iron",
            Properties = new PropertySet(new Dictionary<string, double> { ["hardness"] = 66, ["mass"] = 60 }),
        };

        var label = ItemFormat.InstanceLabel(instance);

        Assert.StartsWith("Barkbound Iron #7 (", label);
        Assert.Contains("hardness 66", label);
        Assert.Contains("mass 60", label);
    }

    [Fact]
    public void InstanceLabel_OmitsParens_WhenNoProperties()
    {
        var instance = new ItemInstance
        {
            InstanceId = 3,
            BaseDefinitionId = "equip.rusty_sword",
            ItemType = ItemType.Weapon,
            DisplayName = "Rusty Sword",
            Properties = PropertySet.Empty,
        };

        Assert.Equal("Rusty Sword #3", ItemFormat.InstanceLabel(instance));
    }
}
