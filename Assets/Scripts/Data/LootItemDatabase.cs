using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

public static class LootItemDatabase
{
    private const string ResourcePath = "Data/items";
    private const int MinimumFieldCount = 14;
    private static List<LootItemData> items;

    public static IReadOnlyList<LootItemData> Items
    {
        get
        {
            EnsureLoaded();
            return items;
        }
    }

    public static LootItemData Roll(int floor, System.Random rng)
    {
        EnsureLoaded();
        LootRarity rarity = RollRarity(floor, rng);
        var candidates = CandidatesAtOrBelow(rarity);
        if (candidates.Count == 0) return null;

        int totalWeight = candidates.Sum(item => Mathf.Max(1, item.DropWeight));
        int roll = rng.Next(totalWeight);
        foreach (var item in candidates)
        {
            roll -= Mathf.Max(1, item.DropWeight);
            if (roll < 0) return item;
        }
        return candidates[candidates.Count - 1];
    }

    public static LootItemData FindById(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return null;
        EnsureLoaded();
        return items.FirstOrDefault(item => string.Equals(item.Id, itemId, StringComparison.Ordinal));
    }

    public static LootItemData Roll(LootRarity rarity, System.Random rng)
    {
        EnsureLoaded();
        var candidates = CandidatesAtOrBelow(rarity);
        if (candidates.Count == 0) return null;
        int totalWeight = candidates.Sum(item => Mathf.Max(1, item.DropWeight));
        int roll = rng.Next(totalWeight);
        foreach (var item in candidates)
        {
            roll -= Mathf.Max(1, item.DropWeight);
            if (roll < 0) return item;
        }
        return candidates[candidates.Count - 1];
    }

    // 뽑힌 등급에 아이템이 없으면 Normal이 아니라 한 단계 아래 등급
    // (2026-11-23, Legendary 0개 문제 재발 방지).
    private static List<LootItemData> CandidatesAtOrBelow(LootRarity rarity)
    {
        for (int value = (int)rarity; value >= (int)LootRarity.Normal; value--)
        {
            LootRarity fallback = (LootRarity)value;
            var candidates = items.Where(item => item.Rarity == fallback && item.DropWeight > 0).ToList();
            if (candidates.Count > 0) return candidates;
        }
        return new List<LootItemData>();
    }

    public static LootRarity RollRarity(int floor, System.Random rng)
    {
        return M10LootRarityTable.Rarity(floor, rng);
    }

    public static int RollBossDropCount(System.Random rng, int floor = 5)
    {
        return M10LootRarityTable.BossCount(floor, rng);
    }

    public static LootRarity RollBossDropRarity(System.Random rng, int floor = 5)
    {
        return M10LootRarityTable.BossRarity(floor, rng);
    }

    private static void EnsureLoaded()
    {
        if (items != null) return;
        items = new List<LootItemData>();
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null)
        {
            Debug.LogError($"[LootItemDatabase] Resources/{ResourcePath}.csv를 찾지 못했습니다.");
            return;
        }

        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length < MinimumFieldCount)
            {
                Debug.LogError($"[LootItemDatabase] {i + 1}행의 컬럼이 부족합니다. 필요 {MinimumFieldCount}, 실제 {fields.Length}.");
                continue;
            }
            if (!Enum.TryParse(fields[2], true, out LootItemType type)) continue;
            if (!Enum.TryParse(fields[3], true, out LootRarity rarity)) continue;

            items.Add(new LootItemData
            {
                Id = fields[0].Trim(),
                Name = fields[1].Trim(),
                Type = type,
                Rarity = rarity,
                AttackBonus = ParseInt(fields[4]),
                DefenseBonus = ParseInt(fields[5]),
                CapacityBonus = ParseInt(fields[6]),
                IconKey = fields[7].Trim(),
                DropWeight = ParseInt(fields[8]),
                SellPrice = ParseInt(fields[9]),
                GridWidth = ParseInt(fields[10]),
                GridHeight = ParseInt(fields[11]),
                BagGridWidth = ParseInt(fields[12]),
                BagGridHeight = ParseInt(fields[13])
            });
        }
        Debug.Log($"[LootItemDatabase] CSV 아이템 {items.Count}개 로드 완료.");
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : 0;
    }

}
