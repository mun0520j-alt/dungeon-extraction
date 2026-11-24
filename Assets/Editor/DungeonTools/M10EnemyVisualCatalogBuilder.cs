#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class M10EnemyVisualCatalogBuilder
{
    private const string Frames = "Assets/Art_Asset/DungeonTiles_0x72/DungeonTiles/0x72_DungeonTilesetII_v1.7/frames";
    private const string AssetPath = "Assets/Resources/Data/M10EnemyVisualCatalog.asset";

    [InitializeOnLoadMethod]
    private static void BuildWhenMissing()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<M10EnemyVisualCatalog>(AssetPath);
        if (CatalogNeedsRebuild(catalog)) Build();
    }

    private static bool CatalogNeedsRebuild(M10EnemyVisualCatalog catalog)
    {
        if (catalog == null || catalog.EntryCount == 0) return true;
        foreach (string key in EnemyKeys())
        {
            M10EnemyVisualCatalog.Entry entry = catalog.Find(key);
            if (entry?.idleFrames == null || entry.idleFrames.Length == 0
                || entry.runFrames == null || entry.runFrames.Length == 0) return true;
            if (entry.idleFrames.Any(frame => frame == null || !frame.name.StartsWith(key + "_", System.StringComparison.Ordinal))
                || entry.runFrames.Any(frame => frame == null || !frame.name.StartsWith(key + "_", System.StringComparison.Ordinal))) return true;
        }
        return false;
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

    private static Sprite[] LoadFrames(string key, string action)
    {
        if (key == "chest_mimic")
        {
            Sprite[] open = LoadExact(@"^chest_mimic_open_anim_f(\d+)$");
            return action == "idle" ? open.Take(1).ToArray() : open;
        }

        Sprite[] exact = LoadExact($@"^{Regex.Escape(key)}_{Regex.Escape(action)}_anim_f(\d+)$");
        if (exact.Length > 0) return exact;
        // 액션 분리 파일이 없는 몬스터는 같은 프레임을 idle/run 양쪽에서 사용한다.
        return LoadExact($@"^{Regex.Escape(key)}_anim_f(\d+)$");
    }

    private static Sprite[] LoadExact(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.CultureInvariant);
        return Directory.GetFiles(Frames, "*.png", SearchOption.TopDirectoryOnly)
            .Select(path => path.Replace('\\', '/'))
            .Select(path => new { path, match = regex.Match(Path.GetFileNameWithoutExtension(path)) })
            .Where(value => value.match.Success)
            .OrderBy(value => int.Parse(value.match.Groups[1].Value))
            .SelectMany(value => AssetDatabase.LoadAllAssetsAtPath(value.path).OfType<Sprite>())
            .ToArray();
    }

    private static IEnumerable<string> EnemyKeys()
    {
        TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/m10_enemies.csv");
        if (csv == null) yield break;
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        string[] lines = csv.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');
            if (fields.Length > 1 && !string.IsNullOrWhiteSpace(fields[1]) && seen.Add(fields[1])) yield return fields[1];
        }
    }
}
#endif
