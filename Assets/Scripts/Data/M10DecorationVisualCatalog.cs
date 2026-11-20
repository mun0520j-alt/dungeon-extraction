using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/M10 Decoration Visual Catalog")]
public sealed class M10DecorationVisualCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry { public string key; public Sprite sprite; }
    [SerializeField] private List<Entry> entries = new List<Entry>();
    public int EntryCount => entries?.Count ?? 0;
    public Sprite Find(string key) => entries?.Find(value => value != null && value.key == key)?.sprite;
#if UNITY_EDITOR
    public void EditorSetEntries(List<Entry> values) => entries = values ?? new List<Entry>();
#endif
}
