using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// M11 시간대·심야·보호 주머니 수치의 단일 진입점.
// 런타임 규칙은 이 클래스를 통해서만 CSV 값을 읽는다.
public static class M11TimeConfig
{
    private const string ResourcePath = "Data/m11_time";
    private static Dictionary<string, string> values;

    public static int NightStartTicks => Int("night_start_ticks", 1800);
    public static int NightStatPercent => Int("night_stat_pct", 120);
    public static int NightXpPercent => Int("night_xp_pct", 150);
    public static int NightPotionDropPercent => Int("night_potion_drop_pct", 150);
    public static int NightSpawnCount => Int("night_spawn_count", 2);
    public static int NightRevealRange => Int("night_reveal_range", 4);
    public static int NightLegendaryPercent => Int("night_legendary_pct", 3);
    public static int NightDropOnePercent => Int("night_drop_one_pct", 50);
    public static int SevenFloorExitCloseTicks => Int("seven_floor_exit_close_ticks", 2220);
    public static int ChestDayOnePercent => Int("chest_day_one_pct", 60);
    public static int ChestNightTwoPercent => Int("chest_night_two_pct", 70);
    public static int CarryLimit => Int("carry_limit", 14);
    public static int OrcCrossDamage => Int("orc_cross_damage", 50);
    public static int OrcCrossRadius => Int("orc_cross_radius", 3);
    public static int PocketStartWidth => Int("pocket_start_width", 2);
    public static int PocketStartHeight => Int("pocket_start_height", 2);
    public static int PocketSixQuestCount => Int("pocket_six_quest_count", 10);
    public static int PocketNineQuestCount => Int("pocket_nine_quest_count", 30);
    public static int StartingSmallPotions => Int("starting_small_potions", 2);
    public static Color NightTint => Color("night_tint", "8C9CFF", new Color(0.55f, 0.61f, 1f));

    public static int RollNightDropCount(System.Random rng) => rng.Next(100) < NightDropOnePercent ? 1 : 2;
    public static int RollChestItemCount(System.Random rng, bool night)
    {
        if (night) return rng.Next(100) < ChestNightTwoPercent ? 2 : 3;
        return rng.Next(100) < ChestDayOnePercent ? 1 : 2;
    }

    private static int Int(string key, int fallback)
    {
        EnsureLoaded();
        return values.TryGetValue(key, out string raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value : fallback;
    }

    private static Color Color(string key, string fallbackHex, Color fallback)
    {
        EnsureLoaded();
        string raw = values.TryGetValue(key, out string value) ? value : fallbackHex;
        return ColorUtility.TryParseHtmlString("#" + raw.Trim().TrimStart('#'), out Color parsed) ? parsed : fallback;
    }

    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null)
        {
            Debug.LogError($"[M11TimeConfig] Resources/{ResourcePath}.csv를 찾지 못했습니다.");
            return;
        }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 2) values[fields[0].Trim()] = fields[1].Trim();
        }
    }
}
