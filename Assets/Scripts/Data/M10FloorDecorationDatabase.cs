using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class M10FloorDecorationDefinition
{
    public string SetId { get; set; }
    public int FloorDensityPerThousand { get; set; }
    public int WallDensityPerThousand { get; set; }
    public string[] FloorKeys { get; set; }
    public string[] WallKeys { get; set; }
}

public static class M10FloorDecorationDatabase
{
    private static readonly Dictionary<string, M10FloorDecorationDefinition> Entries = Load();

    public static M10FloorDecorationDefinition Find(string setId)
    {
        return !string.IsNullOrWhiteSpace(setId) && Entries.TryGetValue(setId, out var value) ? value : null;
    }

    private static Dictionary<string, M10FloorDecorationDefinition> Load()
    {
        var result = new Dictionary<string, M10FloorDecorationDefinition>(StringComparer.OrdinalIgnoreCase);
        TextAsset csv = Resources.Load<TextAsset>("Data/m10_floor_decorations");
        if (csv == null) return result;
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            if (f.Length < 5 || string.IsNullOrWhiteSpace(f[0])) continue;
            int.TryParse(f[1], out int floorDensity);
            int.TryParse(f[2], out int wallDensity);
            result[f[0].Trim()] = new M10FloorDecorationDefinition
            {
                SetId = f[0].Trim(),
                FloorDensityPerThousand = Mathf.Clamp(floorDensity, 0, 1000),
                WallDensityPerThousand = Mathf.Clamp(wallDensity, 0, 1000),
                FloorKeys = SplitKeys(f[3]),
                WallKeys = SplitKeys(f[4])
            };
        }
        return result;
    }

    private static string[] SplitKeys(string raw) => string.IsNullOrWhiteSpace(raw)
        ? Array.Empty<string>()
        : raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
}
