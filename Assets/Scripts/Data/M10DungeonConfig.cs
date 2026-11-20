using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class M10DungeonConfig
{
    private const string ResourcePath = "Data/m10_dungeon";
    private static Dictionary<string, int> values;
    public static int FinalFloor => Get("final_floor", 10);
    public static int FirstBossFloor => Get("boss_floor_1", 5);
    public static int FinalBossFloor => Get("boss_floor_2", 10);
    public static int DeepMinRooms => Get("deep_min_rooms", 9);
    public static int DeepMaxRooms => Mathf.Max(DeepMinRooms, Get("deep_max_rooms", 12));
    // 결정 17: 엘리트는 "아 쟤구나" 정도로만 구분 — 크기 배율(m10_enemies.csv visual_scale) + 옅은 색조(%).
    public static Color EliteTint => new Color(Get("elite_tint_r", 100) / 100f, Get("elite_tint_g", 72) / 100f, Get("elite_tint_b", 72) / 100f, 1f);
    public static bool IsBossFloor(int floor) => floor == FirstBossFloor || floor == FinalBossFloor;
    private static int Get(string key, int fallback) { EnsureLoaded(); return values.TryGetValue(key, out int value) ? value : fallback; }
    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null) { Debug.LogError($"[M10DungeonConfig] Resources/{ResourcePath}.csv를 찾지 못했습니다."); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 2 && int.TryParse(fields[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                values[fields[0].Trim()] = value;
        }
    }
}
