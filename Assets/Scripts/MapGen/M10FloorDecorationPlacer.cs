using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class M10FloorDecorationPlacer
{
    public static int Place(int floor, int seed, Tilemap floorMap, Tilemap wallMap, Transform parent)
    {
        M10FloorThemeDatabase.Theme theme = M10FloorThemeDatabase.Find(floor);
        M10FloorDecorationDefinition definition = M10FloorDecorationDatabase.Find(theme?.DecorationSet);
        if (definition == null || string.Equals(definition.SetId, "classic", StringComparison.OrdinalIgnoreCase)) return 0;

        M10DecorationVisualCatalog catalog = Resources.Load<M10DecorationVisualCatalog>("Data/M10DecorationVisualCatalog");
        if (catalog == null) return 0;

        var rng = new System.Random(seed ^ 0x4D313044);
        var root = new GameObject($"Decorations_{definition.SetId}");
        root.transform.SetParent(parent, false);
        int count = 0;
        count += PlaceOnMap(floorMap, wallMap, false, definition.FloorDensityPerThousand, definition.FloorKeys, catalog, rng, root.transform);
        count += PlaceOnMap(wallMap, floorMap, true, definition.WallDensityPerThousand, definition.WallKeys, catalog, rng, root.transform);
        if (count == 0) UnityEngine.Object.Destroy(root);
        return count;
    }

    private static int PlaceOnMap(Tilemap target, Tilemap other, bool wall, int density, string[] keys,
        M10DecorationVisualCatalog catalog, System.Random rng, Transform parent)
    {
        if (density <= 0 || keys == null || keys.Length == 0) return 0;
        var cells = new List<Vector3Int>();
        foreach (Vector3Int cell in target.cellBounds.allPositionsWithin)
        {
            if (!target.HasTile(cell)) continue;
            if (!wall && other.HasTile(cell)) continue;
            cells.Add(cell);
        }
        cells.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        int placed = 0;
        foreach (Vector3Int cell in cells)
        {
            if (rng.Next(1000) >= density) continue;
            string key = keys[rng.Next(keys.Length)].Trim();
            Sprite sprite = catalog.Find(key);
            if (sprite == null) continue;
            var go = new GameObject($"Decor_{key}_{cell.x}_{cell.y}", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.position = target.GetCellCenterWorld(cell);
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = wall ? 2 : 1;
            placed++;
        }
        return placed;
    }
}
