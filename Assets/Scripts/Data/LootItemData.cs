using System;

public enum LootRarity
{
    Normal,
    Rare,
    Epic,
    Unique,
    Legendary
}

public enum LootItemType
{
    Weapon,
    Shield,
    Armor,
    Bag,
    Junk,
    Consumable
}

[Serializable]
public sealed class LootItemData
{
    public string Id;
    public string Name;
    public LootItemType Type;
    public LootRarity Rarity;
    public int AttackBonus;
    public int DefenseBonus;
    public int CapacityBonus;
    public string IconKey;
    public int DropWeight;
    public int SellPrice;
    public int GridWidth;
    public int GridHeight;
    public int BagGridWidth;
    public int BagGridHeight;
}

[Serializable]
public sealed class LootItemInstance
{
    public string InstanceId;
    public LootItemData Definition;
    public bool IsFoundInRaid;
    [NonSerialized] private LootGridModel bagGrid;

    public LootGridModel BagGrid
    {
        get
        {
            if (Definition?.Type != LootItemType.Bag) return null;
            int width = Math.Max(1, Definition.BagGridWidth);
            int height = Math.Max(1, Definition.BagGridHeight);
            if (bagGrid == null || bagGrid.Width != width || bagGrid.Height != height)
                bagGrid = new LootGridModel(width, height);
            return bagGrid;
        }
    }

    internal void SetBagGrid(LootGridModel value)
    {
        if (Definition?.Type != LootItemType.Bag) return;
        bagGrid = value;
    }

    public LootItemInstance(LootItemData definition, bool foundInRaid = false)
    {
        InstanceId = Guid.NewGuid().ToString("N");
        Definition = definition;
        IsFoundInRaid = foundInRaid;
    }

    public LootItemInstance(LootItemData definition, string instanceId, bool foundInRaid = false)
    {
        InstanceId = string.IsNullOrWhiteSpace(instanceId) ? Guid.NewGuid().ToString("N") : instanceId;
        Definition = definition;
        IsFoundInRaid = foundInRaid;
    }
}
