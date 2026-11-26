using System;
using System.Collections.Generic;
using UnityEngine;

public static class M11PouchConfig
{
    private static Dictionary<string, int> values;
    public static int SixSlotQuestCount => Get("pocket_six_quest_count", 10);
    public static int NineSlotQuestCount => Get("pocket_nine_quest_count", 30);
    public static int StartingSmallPotions => Get("starting_small_potions", 2);
    public static int CarryLimit => Get("carry_limit", 14);
    public static int Width(int tier) => Get($"pocket_{Mathf.Clamp(tier, 1, 3)}_width", tier == 3 ? 3 : 2);
    public static int Height(int tier) => Get($"pocket_{Mathf.Clamp(tier, 1, 3)}_height", tier);

    private static int Get(string key, int fallback)
    {
        EnsureLoaded();
        return values.TryGetValue(key, out int value) ? value : fallback;
    }

    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        TextAsset csv = Resources.Load<TextAsset>("Data/m11_pouch");
        if (csv == null) { Debug.LogError("[M11PouchConfig] m11_pouch.csv를 찾지 못했습니다."); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 2 && int.TryParse(fields[1].Trim(), out int value)) values[fields[0].Trim()] = value;
        }
    }
}
