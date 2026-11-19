using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public static class M9StashConfig
{
    private const string ResourcePath = "Data/m9_stash";
    private static Dictionary<string, int> values;
    public static int BaseWidth => Mathf.Max(1, Get("base_width", 10));
    public static int BaseHeight => Mathf.Max(1, Get("base_height", 20));
    public static int RowsAfterQ03 => Mathf.Max(0, Get("rows_after_q03", 2));
    public static int RowsAfterQ06 => Mathf.Max(0, Get("rows_after_q06", 2));
    public static int SaleBasketWidth => Mathf.Max(1, Get("sale_basket_width", 5));
    public static int SaleBasketHeight => Mathf.Max(1, Get("sale_basket_height", 20));
    // 원정 중 출격 가방 안 하위 가방들에 든 아이템 합계 상한 (결정 41). 하위 가방 공간 꼼수 방지.
    // 동시에 열 수 있는 가방 창 수 (영지·던전 공용, 결정 44)
    public static int BagWindowMax => Mathf.Max(1, Get("bag_window_max", 5));
    public static int RunNestedItemLimit => Mathf.Max(0, Get("run_nested_item_limit", 10));

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
