using System;
using UnityEngine;

public static class M10FloorThemeDatabase
{
    public static void Apply(int floor, UnityEngine.Tilemaps.Tilemap floorMap, UnityEngine.Tilemaps.Tilemap wallMap)
    {
        TextAsset csv = Resources.Load<TextAsset>("Data/m10_floor_themes");
        if (csv == null) return;
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] f = lines[i].Split(',');
            if (f.Length < 5 || !int.TryParse(f[0], out int min) || !int.TryParse(f[1], out int max) || floor < min || floor > max) continue;
            if (ColorUtility.TryParseHtmlString("#" + f[2], out Color floorColor)) floorMap.color = floorColor;
            if (ColorUtility.TryParseHtmlString("#" + f[3], out Color wallColor)) wallMap.color = wallColor;
            floorMap.gameObject.name = $"Floor_{f[4]}";
            wallMap.gameObject.name = $"Walls_{f[4]}";
            return;
        }
    }
}
