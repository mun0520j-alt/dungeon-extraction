#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class M10DecorationVisualCatalogBuilder
{
    private const string Frames = "Assets/Art_Asset/DungeonTiles_0x72/DungeonTiles/0x72_DungeonTilesetII_v1.7/frames";
    private const string AssetPath = "Assets/Resources/Data/M10DecorationVisualCatalog.asset";

    [InitializeOnLoadMethod]
    private static void BuildWhenMissing()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<M10DecorationVisualCatalog>(AssetPath);
        if (catalog == null || catalog.EntryCount == 0) Build();
    }

    [MenuItem("Tools/Dungeon/M10/Rebuild Decoration Visual Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<M10DecorationVisualCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<M10DecorationVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        var entries = new List<M10DecorationVisualCatalog.Entry>();
        Add(entries, "floor_spikes", "floor_spikes_anim_f0");
        Add(entries, "wall_goo", "wall_goo");
        Add(entries, "wall_goo_base", "wall_goo_base");
        Add(entries, "wall_banner_green", "wall_banner_green");
        Add(entries, "wall_banner_blue", "wall_banner_blue");
        Add(entries, "wall_banner_red", "wall_banner_red");
        Add(entries, "wall_fountain_blue", "wall_fountain_mid_blue_anim_f0");
        Add(entries, "wall_fountain_red", "wall_fountain_mid_red_anim_f0");
        catalog.EditorSetEntries(entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[M10] Decoration visual catalog rebuilt: {entries.Count} entries.");
    }

    private static void Add(List<M10DecorationVisualCatalog.Entry> entries, string key, string assetName)
    {
        string path = AssetDatabase.FindAssets(assetName, new[] { Frames }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(value => System.IO.Path.GetFileNameWithoutExtension(value) == assetName);
        Sprite sprite = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite != null) entries.Add(new M10DecorationVisualCatalog.Entry { key = key, sprite = sprite });
    }
}
#endif
