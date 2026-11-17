using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class M9StashConfig
{
    private const string ResourcePath = "Data/m9_stash";
    private static Dictionary<string, int> values;
    public static int BaseWidth => Mathf.Max(1, Get("base_width", 10));
    public static int BaseHeight => Mathf.Max(1, Get("base_height", 8));
    public static int RowsAfterQ03 => Mathf.Max(0, Get("rows_after_q03", 2));
    public static int RowsAfterQ06 => Mathf.Max(0, Get("rows_after_q06", 2));

    private static int Get(string key, int fallback)
    {
        EnsureLoaded();
        return values.TryGetValue(key, out int value) ? value : fallback;
    }

    private static void EnsureLoaded()
    {
        if (values != null) return;
        values = new Dictionary<string, int>(StringComparer.Ordinal);
        TextAsset csv = Resources.Load<TextAsset>(ResourcePath);
        if (csv == null) { Debug.LogError($"[M9StashConfig] Resources/{ResourcePath}.csv를 찾지 못했습니다."); return; }
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length >= 2 && int.TryParse(fields[1].Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value)) values[fields[0].Trim()] = value;
        }
    }
}
