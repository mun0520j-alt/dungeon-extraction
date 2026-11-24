using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class M10DungeonConfig
{
    private const string ResourcePath = "Data/m10_dungeon";
    private static Dictionary<string, int> values;
    private static Dictionary<string, string> rawValues;
    private static HashSet<int> midExitFloors;
    public static int FinalFloor => Get("final_floor", 10);
    public static int FirstBossFloor => Get("boss_floor_1", 5);
    public static int FinalBossFloor => Get("boss_floor_2", 10);
    public static int DeepMinRooms => Get("deep_min_rooms", 9);
    public static int DeepMaxRooms => Mathf.Max(DeepMinRooms, Get("deep_max_rooms", 12));
    public static int EscapeScrollRestockTicks => Mathf.Max(1, Get("escape_scroll_restock_ticks", 10000));
    public static int EscapeScrollRestockAmount => Mathf.Max(1, Get("escape_scroll_restock_amount", 3));
    public static int EscapeScrollStockCap => Mathf.Max(1, Get("escape_scroll_stock_cap", 3));
    public static int FloorArrivalXPShallow => Mathf.Max(0, Get("floor_arrival_xp_shallow", 20));
    public static int FloorArrivalXPDeep => Mathf.Max(0, Get("floor_arrival_xp_deep", 60));
    public static int FastArrivalTicksPerFloor => Mathf.Max(1, Get("fast_arrival_ticks_per_floor", 200));
    public static int FastArrivalBonusPercent => Mathf.Max(0, Get("fast_arrival_bonus_pct", 50));
    public static int BossAoeFinalDamage => Mathf.Max(1, Get("boss_aoe_final_damage", 35));
    public static int BossAoeTelegraphTicks => Mathf.Max(1, Get("boss_aoe_telegraph_ticks", 20));
    public static Color BossAoeFlashColor
    {
        get
        {
            Color color = GetHtmlColor("boss_aoe_flash_hex", "FF3030", new Color(1f, 48f / 255f, 48f / 255f));
            color.a = Mathf.Clamp01(Get("boss_aoe_flash_alpha_pct", 75) / 100f);
            return color;
        }
    }
    public static float BossAoeVfxSeconds => Mathf.Max(0.05f, Get("boss_aoe_vfx_ms", 400) / 1000f);
    public static float BossAoeShakeStrength => Mathf.Max(0f, Get("boss_aoe_shake_strength_pct", 18) / 100f);
    public static float BossAoeShakeSeconds => Mathf.Max(0f, Get("boss_aoe_shake_ms", 250) / 1000f);
    public static int NightFloorMultiplierPercent => Mathf.Max(100, Get("night_floor_multiplier_pct", 100));
    // 결정 34: 본체 원색은 유지하고, 알파 실루엣 외곽선으로 어두운 맵에서도 엘리트를 구분한다.
    public static Color EliteTint => new Color(Get("elite_tint_r", 100) / 100f, Get("elite_tint_g", 100) / 100f, Get("elite_tint_b", 100) / 100f, 1f);
    public static int EliteOutlinePixels => Mathf.Clamp(Get("elite_outline_px", 1), 1, 4);
    public static IEnumerable<int> MidExitFloors { get { EnsureLoaded(); return midExitFloors; } }
    public static bool HasMidExit(int floor) { EnsureLoaded(); return midExitFloors != null && midExitFloors.Contains(floor); }
    public static Color ExitZoneColor
    {
        get
        {
            Color color = GetHtmlColor("exit_zone_hex", "20FFC0", new Color(32f / 255f, 1f, 192f / 255f));
            color.a = Mathf.Clamp01(Get("exit_zone_alpha_pct", 60) / 100f);
            return color;
        }
    }
    public static Color EliteOutlineColor
    {
        get
        {
            return GetHtmlColor("elite_outline_hex", "FF3030", new Color(1f, 48f / 255f, 48f / 255f));
        }
    }
    public static bool IsBossFloor(int floor) => floor == FirstBossFloor || floor == FinalBossFloor;
    private static int Get(string key, int fallback) { EnsureLoaded(); return values.TryGetValue(key, out int value) ? value : fallback; }
    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        rawValues = new Dictionary<string, string>(StringComparer.Ordinal);
        midExitFloors = new HashSet<int> { 3, 5, 7 };
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null) { Debug.LogError($"[M10DungeonConfig] Resources/{ResourcePath}.csv를 찾지 못했습니다."); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length < 2) continue;
            string key = fields[0].Trim();
            string raw = fields[1].Trim();
            rawValues[key] = raw;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) values[key] = value;
        }
        midExitFloors = ParseFloorSet(GetRaw("mid_exit_floors", "3;5;7"));
    }

    private static string GetRaw(string key, string fallback)
    {
        return rawValues != null && rawValues.TryGetValue(key, out string value) ? value : fallback;
    }

    private static HashSet<int> ParseFloorSet(string raw)
    {
        var result = new HashSet<int>();
        foreach (string token in raw.Split(';'))
            if (int.TryParse(token.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int floor)) result.Add(floor);
        return result;
    }

    private static Color GetHtmlColor(string key, string fallbackHex, Color fallback)
    {
        EnsureLoaded();
        string hex = GetRaw(key, fallbackHex).Trim().TrimStart('#');
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color color)) return color;
        fallback.a = 1f;
        return fallback;
    }
}
