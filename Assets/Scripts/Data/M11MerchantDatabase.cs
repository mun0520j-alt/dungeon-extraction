using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class M11MerchantDefinition
{
    public string Id;
    public string Name;
    public string[] ItemTags;
    public int MatchedSellPercent;
    public int UnmatchedSellPercent;

    public bool Handles(LootItemData item) => item != null && ItemTags.Any(tag =>
        string.Equals(tag, item.Type.ToString(), StringComparison.OrdinalIgnoreCase));
}

public static class M11MerchantDatabase
{
    public const string Weapon = "weapon";
    public const string Armor = "armor";
    public const string Supply = "supply";
    public const string Collector = "collector";
    private const string ResourcePath = "Data/m11_merchants";
    private static readonly List<M11MerchantDefinition> all = new List<M11MerchantDefinition>();
    private static bool loaded;

    public static IReadOnlyList<M11MerchantDefinition> All { get { EnsureLoaded(); return all; } }
    public static M11MerchantDefinition Find(string id) { EnsureLoaded(); return all.FirstOrDefault(x => x.Id == id); }
    public static string MerchantFor(LootItemData item)
    {
        EnsureLoaded();
        return all.FirstOrDefault(x => x.Handles(item))?.Id;
    }

    public static bool IsPurchaseItemFor(string merchantId, LootItemData item) => Find(merchantId)?.Handles(item) == true;
    public static float SellMultiplier(string merchantId, LootItemData item)
    {
        M11MerchantDefinition merchant = Find(merchantId) ?? Find(Armor);
        int percent = merchant != null && merchant.Handles(item) ? merchant.MatchedSellPercent : merchant?.UnmatchedSellPercent ?? 60;
        return Mathf.Max(0, percent) / 100f;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null) { Debug.LogError($"[M11Merchant] Resources/{ResourcePath}.csv가 없습니다."); return; }
        string[] lines = csv.text.Split(new[] {'\r','\n'}, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            if (f.Length < 5) continue;
            all.Add(new M11MerchantDefinition
            {
                Id=f[0].Trim(), Name=f[1].Trim(), ItemTags=f[2].Split('|'),
                MatchedSellPercent=Int(f[3]), UnmatchedSellPercent=Int(f[4])
            });
        }
    }

    private static int Int(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
}
