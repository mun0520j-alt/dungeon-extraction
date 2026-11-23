using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Dungeon/M10 Enemy Visual Catalog")]
public sealed class M10EnemyVisualCatalog : ScriptableObject
{
    [Serializable] public sealed class Entry { public string key; public Sprite[] idleFrames; public Sprite[] runFrames; }
    [SerializeField] private List<Entry> entries = new List<Entry>();
    public int EntryCount => entries?.Count ?? 0;
    public Entry Find(string key) => entries.Find(value => value != null && value.key == key);
#if UNITY_EDITOR
    public void EditorSetEntries(List<Entry> values) => entries = values ?? new List<Entry>();
#endif
}

public sealed class M10SpriteAnimation : MonoBehaviour
{
    private SpriteRenderer target; private Sprite[] idleFrames; private Sprite[] runFrames; private Sprite[] frames; private float elapsed; private int index;
    public void Configure(SpriteRenderer renderer, Sprite[] idle, Sprite[] run) { target=renderer; idleFrames=idle; runFrames=run; PlayIdle(); }
    public void PlayIdle() => Play(idleFrames);
    public void PlayRun() => Play(runFrames != null && runFrames.Length > 0 ? runFrames : idleFrames);
    private void Play(Sprite[] next) { frames=next; elapsed=0f; index=0; if(target!=null&&frames?.Length>0) target.sprite=frames[0]; }
    private void Update() { if(target==null||frames==null||frames.Length<2)return; elapsed+=Time.deltaTime; if(elapsed<0.18f)return; elapsed=0f; index=(index+1)%frames.Length; target.sprite=frames[index]; }
}
