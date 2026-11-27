using System.Collections;
using UnityEngine;

public partial class EnemyTurnActor
{
    private GameObject archerBow;
    private SpriteRenderer archerBowRenderer;
    private GameObject archerAimLine;
    private Vector3Int archerAimDirection;
    private bool archerAiming;

    private bool IsArcher => string.Equals(m10Definition?.AttackType, "ranged", System.StringComparison.OrdinalIgnoreCase);

    private void ConfigureArcherVisual()
    {
        if (!IsArcher || archerBow != null) return;
        Sprite sprite = FirstArcherSprite(Resources.Load<SpriteFrameSet>("Data/M11ArcherBow"));
        if (sprite == null) return;
        archerBow = new GameObject("ArcherBow", typeof(SpriteRenderer));
        archerBow.transform.SetParent(transform, false);
        archerBow.transform.localPosition = new Vector3(0.22f, 0.08f, -0.03f);
        archerBowRenderer = archerBow.GetComponent<SpriteRenderer>();
        archerBowRenderer.sprite = sprite;
        archerBowRenderer.sortingOrder = (spriteRenderer?.sortingOrder ?? 0) + 1;
    }

    private void SetArcherVisible(bool visible)
    {
        if (archerBowRenderer != null)
        {
            archerBowRenderer.enabled = visible;
            archerBowRenderer.flipX = spriteRenderer != null && spriteRenderer.flipX;
            archerBow.transform.localPosition = new Vector3(archerBowRenderer.flipX ? -0.22f : 0.22f, 0.08f, -0.03f);
        }
        if (archerAimLine != null) archerAimLine.SetActive(visible);
    }

    private bool RunArcherTurn()
    {
        if (!IsArcher) return false;
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (player == null || player.FloorTilemap != floorTilemap) { ClearArcherAim(); return false; }
        Vector3Int delta = player.CurrentCell - currentCell;
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        if (distance == 1)
        {
            ClearArcherAim();
            catalogAnimation?.PlayAttackOnce();
            int damage = Mathf.Max(1, Mathf.RoundToInt(attackPower * m10Definition.MeleeDamagePercent / 100f));
            player.TakeDamage(damage, m10Definition.Id);
            TickManager.Instance.CompleteTurn(this, TickCost.Attack, "ArcherMelee");
            return true;
        }

        if (archerAiming)
        {
            bool stillOnLine = IsOnAimLine(delta) && distance <= m10Definition.Range
                && LineOfSight.HasLineOfSight(currentCell, player.CurrentCell, wallsTilemap);
            ClearArcherAim();
            if (stillOnLine) { FireArcherShot(player); return true; }
            Debug.Log($"[Archer] {name}: Player가 조준선에서 벗어나 발사 취소");
            return false;
        }

        if (distance <= m10Definition.Range && IsStraight(delta)
            && LineOfSight.HasLineOfSight(currentCell, player.CurrentCell, wallsTilemap))
        {
            archerAimDirection = new Vector3Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y), 0);
            archerAiming = true;
            CreateArcherAimLine(player.CurrentCell);
            TickManager.Instance.CompleteTurn(this, TickCost.Attack, "ArcherAim");
            Debug.Log($"[Archer] {name}: {player.CurrentCell} 조준 — 다음 행동에 발사");
            return true;
        }
        return false;
    }

    private bool IsOnAimLine(Vector3Int delta) => IsStraight(delta)
        && (archerAimDirection.x == 0 ? delta.x == 0 && System.Math.Sign(delta.y) == archerAimDirection.y
            : delta.y == 0 && System.Math.Sign(delta.x) == archerAimDirection.x);
    private static bool IsStraight(Vector3Int delta) => (delta.x == 0) != (delta.y == 0);

    private void FireArcherShot(PlayerTurnActor player)
    {
        catalogAnimation?.PlayAttackOnce();
        int damage = Mathf.Max(1, Mathf.RoundToInt(attackPower * m10Definition.RangedDamagePercent / 100f));
        StartCoroutine(AnimateArcherArrow(floorTilemap.GetCellCenterWorld(player.CurrentCell)));
        player.TakeDamage(damage, m10Definition.Id);
        TickManager.Instance.CompleteTurn(this, TickCost.Attack, "ArcherShot");
    }

    private IEnumerator AnimateArcherArrow(Vector3 target)
    {
        Sprite sprite = FirstArcherSprite(Resources.Load<SpriteFrameSet>("Data/M11ArcherArrow"));
        if (sprite == null) yield break;
        var arrow = new GameObject("ArcherArrow", typeof(SpriteRenderer));
        SpriteRenderer renderer = arrow.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = (spriteRenderer?.sortingOrder ?? 0) + 2;
        Vector3 start = transform.position;
        Vector3 direction = target - start;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        const float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            arrow.transform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        Destroy(arrow);
    }

    private void CreateArcherAimLine(Vector3Int targetCell)
    {
        ClearAimVisual();
        archerAimLine = new GameObject("ArcherAimLine", typeof(LineRenderer));
        LineRenderer line = archerAimLine.GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = line.endColor = new Color(1f, 0.15f, 0.15f, 0.85f);
        line.sortingOrder = 20;
        line.SetPosition(0, floorTilemap.GetCellCenterWorld(currentCell));
        line.SetPosition(1, floorTilemap.GetCellCenterWorld(targetCell));
    }

    private void ClearArcherAim()
    {
        archerAiming = false;
        ClearAimVisual();
    }

    private void ClearAimVisual()
    {
        if (archerAimLine != null) Destroy(archerAimLine);
        archerAimLine = null;
    }

    private static Sprite FirstArcherSprite(SpriteFrameSet set)
    {
        if (set?.states == null) return null;
        foreach (SpriteFrameSet.StateFrames state in set.states)
            if (state?.frames != null && state.frames.Count > 0) return state.frames[0];
        return null;
    }
}
