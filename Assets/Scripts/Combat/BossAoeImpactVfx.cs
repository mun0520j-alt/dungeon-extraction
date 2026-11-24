using System.Collections;
using UnityEngine;

// 보스 광역 발동 범위를 한 번 번쩍이고 기존 CameraFollow에 짧은 흔들림만 요청한다.
// 임시 GameObject가 자기 수명을 관리하므로 별도 Canvas나 전역 상태를 만들지 않는다.
public static class BossAoeImpactVfx
{
    public static void Spawn(Vector3 worldPosition)
    {
        var effect = new GameObject("BossAoeImpactVfx");
        effect.transform.position = worldPosition;
        effect.transform.localScale = new Vector3(5f, 5f, 1f);
        var renderer = effect.AddComponent<SpriteRenderer>();
        renderer.sprite = ProceduralSprite.GetWhiteSpriteCenterPivot();
        renderer.color = M10DungeonConfig.BossAoeFlashColor;
        renderer.sortingOrder = 205;
        effect.AddComponent<Runner>().Play(M10DungeonConfig.BossAoeVfxSeconds);

        Camera camera = Camera.main;
        CameraFollow follow = camera != null ? camera.GetComponent<CameraFollow>() : null;
        follow?.PlayShake(M10DungeonConfig.BossAoeShakeStrength, M10DungeonConfig.BossAoeShakeSeconds);
    }

    private sealed class Runner : MonoBehaviour
    {
        public void Play(float duration) => StartCoroutine(Fade(duration));

        private IEnumerator Fade(float duration)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            Color start = renderer.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = start;
                color.a = Mathf.Lerp(start.a, 0f, Mathf.Clamp01(elapsed / duration));
                renderer.color = color;
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
