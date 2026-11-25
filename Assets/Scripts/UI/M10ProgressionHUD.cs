using System.Collections.Generic;
using UnityEngine;

// 새 Canvas를 만들지 않는 표시 전용 HUD.
public sealed class M10ProgressionHUD : MonoBehaviour
{
    private const float PanelX = 18f;
    // 시계(상단 24 + 지름 96 + 남은 시간 28) 아래로 분리해 Tick HUD와 겹치지 않는다.
    private const float PanelY = 164f;
    private const float PanelWidth = 286f;

    private int observedLevel = 1;
    private float levelUpUntil;
    private GUIStyle titleStyle;
    private GUIStyle barTextStyle;
    private GUIStyle combatLogStyle;

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
        int levelStartXP = M10ProgressionDatabase.Level(level).CumulativeXP;
        int nextXP = M10RunProgression.NextLevelXP;
        bool isMaxLevel = level >= M10RunProgression.MaxLevel;
        int earnedThisLevel = isMaxLevel ? 0 : Mathf.Max(0, M10RunProgression.Experience - levelStartXP);
        int requiredThisLevel = isMaxLevel ? 0 : Mathf.Max(1, nextXP - levelStartXP);
        int remainingXP = isMaxLevel ? 0 : Mathf.Max(0, nextXP - M10RunProgression.Experience);
        float progress = isMaxLevel ? 1f : Mathf.Clamp01((float)earnedThisLevel / requiredThisLevel);

        EnsureStyles();
        Rect panel = new Rect(PanelX, PanelY, PanelWidth, M10RunProgression.IsHealing ? 82f : 60f);
        GUI.Box(panel, GUIContent.none);

        string title = isMaxLevel
            ? $"Lv.{level}  MAX"
            : $"Lv.{level}   다음 레벨까지 {remainingXP} XP";
        GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 22f), title, titleStyle);

        Rect bar = new Rect(panel.x + 10f, panel.y + 31f, panel.width - 20f, 18f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.08f, 0.11f, 0.17f, 0.95f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = new Color(0.2f, 0.75f, 1f, 0.95f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * progress, bar.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        GUI.Label(bar, isMaxLevel ? "MAX LEVEL" : $"{earnedThisLevel} / {requiredThisLevel} XP", barTextStyle);

        if (M10RunProgression.IsHealing)
            GUI.Label(new Rect(panel.x + 10f, panel.y + 56f, panel.width - 20f, 20f),
                $"회복 중 · {M10RunProgression.HealingTicksRemaining:0.#} Tick");
        if (Time.unscaledTime < levelUpUntil)
            GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.3f, 200f, 40f), $"LEVEL UP!  Lv.{level}");

        IReadOnlyList<string> lines = CombatFeedback.RecentLines;
        if (lines.Count > 0)
        {
            float lineHeight = 22f;
            float logHeight = lines.Count * lineHeight + 12f;
            Rect logPanel = new Rect(18f, Screen.height - logHeight - 18f, 360f, logHeight);
            GUI.Box(logPanel, GUIContent.none);
            for (int i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(logPanel.x + 8f, logPanel.y + 6f + i * lineHeight,
                    logPanel.width - 16f, lineHeight), lines[i], combatLogStyle);
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }

        if (barTextStyle == null)
        {
            barTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            barTextStyle.normal.textColor = Color.white;
        }

        if (combatLogStyle == null)
        {
            combatLogStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            combatLogStyle.normal.textColor = Color.white;
        }
    }
}
