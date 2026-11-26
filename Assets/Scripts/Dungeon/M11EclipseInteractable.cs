using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class M11EclipseInteractable : MonoBehaviour
{
    private Tilemap floor;
    private Vector3Int cell;
    private bool fragment;

    public static void Spawn(Tilemap targetFloor, Vector3Int targetCell, bool fragment)
    {
        var go = new GameObject(fragment ? "EclipseFragment" : "AltarKeeperNPC");
        go.AddComponent<M11EclipseInteractable>().Initialize(targetFloor, targetCell, fragment);
    }

    private void Initialize(Tilemap targetFloor, Vector3Int targetCell, bool isFragment)
    {
        floor = targetFloor;
        cell = targetCell;
        fragment = isFragment;
        transform.SetParent(floor.transform.parent, false);
        transform.position = floor.GetCellCenterWorld(cell);
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ProceduralSprite.GetWhiteSpriteCenterPivot();
        renderer.color = fragment ? new Color(0.72f, 0.35f, 1f, 0.95f) : new Color(0.8f, 0.72f, 0.55f, 1f);
        renderer.sortingOrder = 7;
        transform.localScale = fragment ? new Vector3(0.45f, 0.45f, 1f) : new Vector3(0.75f, 0.9f, 1f);
    }

    private void Update()
    {
        if (PauseMenuController.BlocksWorldInput || InventoryPanel.IsOpen || GroundLoot.IsSelectionOpen) return;
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (player == null || player.FloorTilemap != floor || !Input.GetKeyDown(KeyCode.E)) return;
        int distance = Mathf.Abs(player.CurrentCell.x - cell.x) + Mathf.Abs(player.CurrentCell.y - cell.y);
        if (distance > 1) return;
        if (fragment)
        {
            if (!player.TryPerformTimedAction(2f, "TakeEclipseFragment", () =>
            {
                M11EclipseState.AcquireFragment();
                Destroy(gameObject);
                return true;
            })) return;
        }
        else
        {
            M11EclipseState.AcceptQuest();
            Destroy(gameObject);
        }
    }

    private void OnGUI()
    {
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (player == null || player.FloorTilemap != floor) return;
        int distance = Mathf.Abs(player.CurrentCell.x - cell.x) + Mathf.Abs(player.CurrentCell.y - cell.y);
        if (distance > 1) return;
        GUI.Box(new Rect(Screen.width * 0.5f - 170f, Screen.height - 140f, 340f, 34f),
            fragment ? "E: 월식의 파편 획득 (2 Tick)" : "E: 제단지기 — 멈춰버린 밤 수락");
    }
}
