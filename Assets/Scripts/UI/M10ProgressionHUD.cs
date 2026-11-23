using UnityEngine;

// 새 Canvas를 만들지 않는 표시 전용 HUD.
public sealed class M10ProgressionHUD : MonoBehaviour
{
    private int observedLevel = 1;
    private float levelUpUntil;

    private void Awake() => observedLevel = M10RunProgression.Level;

    private void Update()
    {
        if (M10RunProgression.Level <= observedLevel) return;
        observedLevel = M10RunProgression.Level;
        levelUpUntil = Time.unscaledTime + 2f;
    }

    private void OnGUI()
    {
        if (!ExpeditionSession.IsActive) return;
        int level = M10RunProgression.Level;
        int currentFloorXP = M10ProgressionDatabase.Level(level).CumulativeXP;
        int nextXP = M10RunProgression.NextLevelXP;
        float progress = level >= M10RunProgression.MaxLevel ? 1f
            : Mathf.InverseLerp(currentFloorXP, nextXP, M10RunProgression.Experience);
        Rect panel = new Rect(18f, 132f, 250f, M10RunProgression.IsHealing ? 70f : 50f);
        GUI.Box(panel, GUIContent.none);
        GUI.Label(new Rect(panel.x + 8f, panel.y + 4f, 230f, 20f),
            level >= M10RunProgression.MaxLevel ? $"Lv.{level} MAX" : $"Lv.{level}  XP {M10RunProgression.Experience}/{nextXP}");
        GUI.Box(new Rect(panel.x + 8f, panel.y + 28f, 230f, 12f), GUIContent.none);
        GUI.Box(new Rect(panel.x + 8f, panel.y + 28f, 230f * progress, 12f), GUIContent.none);
        if (M10RunProgression.IsHealing)
            GUI.Label(new Rect(panel.x + 8f, panel.y + 44f, 230f, 20f), $"회복 중 · {M10RunProgression.HealingTicksRemaining:0.#} Tick");
        if (Time.unscaledTime < levelUpUntil)
            GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.3f, 200f, 40f), $"LEVEL UP!  Lv.{level}");
    }
}
