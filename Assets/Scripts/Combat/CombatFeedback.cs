using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 전투 중 실제 적용된 피해·회복량만 표시한다. 별도 Canvas/Singleton 없이 기존 HUD가 로그를 읽고,
// 월드 팝업은 독립 오브젝트가 수명을 관리한다 (결정 52, 2026-11-25).
public static class CombatFeedback
{
    private static readonly List<string> recentLines = new List<string>();
    private static int popupFrame = -1;
    private static int popupCountThisFrame;

    public static IReadOnlyList<string> RecentLines => recentLines;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubsystem() => Clear();

    public static void Clear()
    {
        recentLines.Clear();
        popupFrame = -1;
        popupCountThisFrame = 0;
    }

    public static void AddLine(string message)
    {
        if (!ExpeditionSession.IsActive || string.IsNullOrWhiteSpace(message)) return;
        recentLines.Add(message);
        int limit = M10DungeonConfig.CombatLogLines;
        while (recentLines.Count > limit) recentLines.RemoveAt(0);
    }

    public static void ShowDamage(Vector3 worldPosition, int amount, bool playerTarget, bool bossAoe = false)
    {
        if (amount <= 0) return;
        Color color = bossAoe ? new Color(1f, 0.55f, 0.1f) : playerTarget ? new Color(1f, 0.2f, 0.2f) : Color.white;
        SpawnPopup(worldPosition, $"-{amount}", color);
    }

    public static void ShowHealing(Vector3 worldPosition, int amount)
    {
        if (amount > 0) SpawnPopup(worldPosition, $"+{amount}", new Color(0.25f, 1f, 0.35f));
    }

    private static void SpawnPopup(Vector3 worldPosition, string text, Color color)
    {
        if (Time.frameCount != popupFrame)
        {
            popupFrame = Time.frameCount;
            popupCountThisFrame = 0;
        }

        float stackOffset = popupCountThisFrame++ * 0.22f;
        var popup = new GameObject("CombatNumberPopup");
        popup.transform.position = worldPosition + Vector3.up * (1.05f + stackOffset);
        var label = popup.AddComponent<TextMesh>();
        label.text = text;
        label.color = color;
        label.fontSize = 48;
        label.characterSize = 0.045f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontStyle = FontStyle.Bold;
        label.GetComponent<MeshRenderer>().sortingOrder = 260;
        popup.AddComponent<PopupRunner>().Play(color);
    }

    private sealed class PopupRunner : MonoBehaviour
    {
        private Color baseColor;

        public void Play(Color color)
        {
            baseColor = color;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float duration = M10DungeonConfig.DamagePopupSeconds;
            float rise = M10DungeonConfig.DamagePopupRiseWorld;
            Vector3 start = transform.position;
            TextMesh label = GetComponent<TextMesh>();
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                transform.position = start + Vector3.up * (rise * progress);
                Color color = baseColor;
                color.a = 1f - progress;
                if (label != null) label.color = color;
                yield return null;
            }
            Object.Destroy(gameObject);
        }
    }
}
