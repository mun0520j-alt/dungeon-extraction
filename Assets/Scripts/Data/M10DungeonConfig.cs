using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class M10DungeonConfig
{
    private const string ResourcePath = "Data/m10_dungeon";
    private static Dictionary<string, int> values;
    private static Dictionary<string, string> rawValues;
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
    public static int NightFloorMultiplierPercent => Mathf.Max(100, Get("night_floor_multiplier_pct", 100));
    // 결정 34: 본체 원색은 유지하고, 알파 실루엣 외곽선으로 어두운 맵에서도 엘리트를 구분한다.
    public static Color EliteTint => new Color(Get("elite_tint_r", 100) / 100f, Get("elite_tint_g", 100) / 100f, Get("elite_tint_b", 100) / 100f, 1f);
    public static int EliteOutlinePixels => Mathf.Clamp(Get("elite_outline_px", 1), 1, 4);
    public static Color EliteOutlineColor
    {
        get
        {
            EnsureLoaded();
            string hex = rawValues != null && rawValues.TryGetValue("elite_outline_hex", out string value) ? value : "FF3030";
            return ColorUtility.TryParseHtmlString("#" + hex.Trim().TrimStart('#'), out Color color)
                ? color : new Color(1f, 48f / 255f, 48f / 255f, 1f);
        }
    }
    public static bool IsBossFloor(int floor) => floor == FirstBossFloor || floor == FinalBossFloor;
    private static int Get(string key, int fallback) { EnsureLoaded(); return values.TryGetValue(key, out int value) ? value : fallback; }
    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        rawValues = new Dictionary<string, string>(StringComparer.Ordinal);
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
    }
}
