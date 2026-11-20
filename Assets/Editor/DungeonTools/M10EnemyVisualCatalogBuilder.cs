#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class M10EnemyVisualCatalogBuilder
{
    private const string Frames = "Assets/Art_Asset/DungeonTiles_0x72/DungeonTiles/0x72_DungeonTilesetII_v1.7/frames";
    private const string AssetPath = "Assets/Resources/Data/M10EnemyVisualCatalog.asset";

    [InitializeOnLoadMethod]
    private static void BuildWhenMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<M10EnemyVisualCatalog>(AssetPath) == null) Build();
    }

    [MenuItem("Tools/Dungeon/M10/Rebuild Enemy Visual Catalog")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<M10EnemyVisualCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<M10EnemyVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        var entries = new List<M10EnemyVisualCatalog.Entry>();
        foreach (string key in EnemyKeys())
        {
            Sprite[] idle = LoadFrames(key, "idle");
            Sprite[] run = LoadFrames(key, "run");
            if (idle.Length == 0 && run.Length == 0) continue;
            entries.Add(new M10EnemyVisualCatalog.Entry { key = key, idleFrames = idle, runFrames = run });
        }
        catalog.EditorSetEntries(entries);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[M10] Enemy visual catalog rebuilt: {entries.Count} entries.");
    }

    private static Sprite[] LoadFrames(string key, string action) =>
        AssetDatabase.FindAssets($"{key}_{action}_anim", new[] { Frames })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path)
            .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
            .ToArray();

    private static IEnumerable<string> EnemyKeys()
    {
        TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/m10_enemies.csv");
        if (csv == null) yield break;
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length > 1 && !string.IsNullOrWhiteSpace(fields[1])) yield return fields[1];
        }
    }
}
#endif
