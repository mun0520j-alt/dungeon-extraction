using UnityEngine;

// 엘리트 본체 SpriteRenderer의 알파 모양만 단색으로 그린 복제 4장을 뒤에 배치한다.
// LateUpdate에서 본체 애니메이션과 flip을 복사하므로 별도 애니메이션 상태를 소유하지 않는다.
public sealed class EliteSpriteOutline : MonoBehaviour
{
    private static readonly Vector2[] Directions =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right
    };

    private static Material sharedSilhouetteMaterial;
    private readonly SpriteRenderer[] outlines = new SpriteRenderer[4];
    private SpriteRenderer body;
    private Color outlineColor;
    private int thicknessPixels;
    private bool externallyVisible = true;

    public static void Attach(SpriteRenderer target, Color color, int pixels)
    {
        if (target == null) return;
        EliteSpriteOutline outline = target.GetComponent<EliteSpriteOutline>();
        if (outline == null) outline = target.gameObject.AddComponent<EliteSpriteOutline>();
        outline.Configure(target, color, pixels);
    }

    private void Configure(SpriteRenderer target, Color color, int pixels)
    {
        body = target;
        outlineColor = color;
        thicknessPixels = Mathf.Clamp(pixels, 1, 4);
        EnsureMaterial();
        EnsureRenderers();
        Synchronize();
    }

    private static void EnsureMaterial()
    {
        if (sharedSilhouetteMaterial != null) return;
        Shader shader = Resources.Load<Shader>("Shaders/M10EliteSilhouette");
        if (shader == null)
        {
            Debug.LogError("[EliteOutline] Resources/Shaders/M10EliteSilhouette 셰이더를 찾지 못했습니다.");
            return;
        }
        sharedSilhouetteMaterial = new Material(shader) { name = "M10EliteSilhouette_Runtime" };
    }

    private void EnsureRenderers()
    {
        for (int i = 0; i < outlines.Length; i++)
        {
            if (outlines[i] != null) continue;
            var child = new GameObject($"EliteOutline_{i}");
            child.transform.SetParent(transform, false);
            outlines[i] = child.AddComponent<SpriteRenderer>();
        }
    }

    private void LateUpdate() => Synchronize();

    public void SetVisible(bool value)
    {
        externallyVisible = value;
        Synchronize();
    }

    private void Synchronize()
    {
        if (body == null) return;
        float pixelsPerUnit = body.sprite != null ? Mathf.Max(1f, body.sprite.pixelsPerUnit) : 16f;
        float offset = thicknessPixels / pixelsPerUnit;
        for (int i = 0; i < outlines.Length; i++)
        {
            SpriteRenderer outline = outlines[i];
            if (outline == null) continue;
            outline.transform.localPosition = new Vector3(Directions[i].x * offset, Directions[i].y * offset, 0f);
            outline.sprite = body.sprite;
            outline.flipX = body.flipX;
            outline.flipY = body.flipY;
            outline.color = outlineColor;
            outline.sharedMaterial = sharedSilhouetteMaterial;
            outline.sortingLayerID = body.sortingLayerID;
            outline.sortingOrder = body.sortingOrder - 1;
            outline.maskInteraction = body.maskInteraction;
            outline.enabled = externallyVisible && body.enabled && body.sprite != null && sharedSilhouetteMaterial != null;
        }
    }
}
