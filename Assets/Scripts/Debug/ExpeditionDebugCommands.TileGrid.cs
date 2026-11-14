#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class ExpeditionDebugCommands
{
    private void DrawTileGridOverlay()
    {
        var player = PlayerTurnActor.Instance;
        var camera = Camera.main;
        if (player == null || camera == null || player.FloorTilemap == null || player.WallsTilemap == null) return;

        Tilemap floor = player.FloorTilemap;
        Tilemap walls = player.WallsTilemap;
        Vector3Int playerCell = player.CurrentCell;

        Vector3 bottomLeftWorld = camera.ViewportToWorldPoint(new Vector3(0f, 0f, -camera.transform.position.z));
        Vector3 topRightWorld = camera.ViewportToWorldPoint(new Vector3(1f, 1f, -camera.transform.position.z));
        Vector3Int minCell = floor.WorldToCell(bottomLeftWorld) - Vector3Int.one;
        Vector3Int maxCell = floor.WorldToCell(topRightWorld) + Vector3Int.one;

        EnsureGridLabelStyle();
        for (int y = minCell.y; y <= maxCell.y; y++)
        {
            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                var cell = new Vector3Int(x, y, 0);
                Rect rect = CellToGuiRect(floor, camera, cell);
                if (rect.width < 2f || rect.height < 2f) continue;

                bool hasFloor = floor.HasTile(cell);
                bool hasWall = walls.HasTile(cell);
                int roomId = DungeonRoomRegistry.GetRoomId(floor, cell);
                bool hasDoor = DungeonDoor.TryGetDebugState(floor, cell, out bool doorOpen);
                bool hasChest = DungeonChest.IsCellBlocked(cell);
                bool hasStairs = DungeonStairs.TryGetDebugLabel(cell, out string stairsLabel);
                ITurnActor occupyingActor = TickManager.Instance != null
                    ? TickManager.Instance.GetActorAt(cell, player)
                    : null;
                Color fill = hasWall
                    ? new Color(1f, 0.1f, 0.1f, 0.28f)
                    : hasFloor ? new Color(0.1f, 0.9f, 0.35f, 0.12f) : new Color(0.25f, 0.25f, 0.25f, 0.08f);
                FillRect(rect, fill);
                DrawRectOutline(rect, hasWall ? Color.red : hasFloor ? Color.green : Color.gray, 1f);

                if (hasDoor)
                {
                    FillRect(rect, doorOpen ? new Color(0.1f, 0.75f, 1f, 0.16f) : new Color(1f, 0.45f, 0.05f, 0.35f));
                    DrawRectOutline(rect, doorOpen ? Color.cyan : new Color(1f, 0.45f, 0.05f), 3f);
                }
                else if (hasChest)
                {
                    DrawRectOutline(rect, new Color(0.65f, 0.35f, 0.1f), 3f);
                }
                else if (hasStairs)
                {
                    DrawRectOutline(rect, new Color(0.2f, 0.8f, 1f), 3f);
                }

                string direction = GetDirectionLabel(playerCell, cell);
                if (cell == playerCell)
                {
                    DrawRectOutline(rect, Color.yellow, 3f);
                    direction = "PLAYER";
                }
                else if (!string.IsNullOrEmpty(direction))
                {
                    DrawRectOutline(rect, new Color(1f, 0.55f, 0f), 3f);
                }
                if (occupyingActor != null)
                {
                    DrawRectOutline(rect, new Color(0.85f, 0.2f, 1f), 4f);
                    direction = $"ACTOR:{occupyingActor.GetType().Name}";
                }

                if (rect.width >= 34f)
                {
                    string state = hasWall ? "W" : hasFloor ? "F" : "-";
                    string room = roomId >= 0 ? $"R{roomId}" : "R-";
                    string obj = hasDoor ? (doorOpen ? "D:OPEN" : "D:CLOSED")
                        : hasChest ? "CHEST" : hasStairs ? stairsLabel : string.Empty;
                    GUI.Label(rect, $"{x},{y} {room}\n{state} {obj} {direction}", gridLabelStyle);
                }
            }
        }

        GUI.Box(new Rect(12f, 12f, 460f, 108f),
            HotkeyHelpLine() + "\n초록=Floor / 빨강=Wall / 노랑=Player / 보라=Actor\n" +
            "주황=D:CLOSED / 하늘=D:OPEN / 갈색=CHEST / 파랑=STAIRS·EXIT\n" +
            "R번호=Room ID / 주황 테두리=Player 인접 검색 칸\nF4: 왼쪽 상자 / 위 Enemy / 오른쪽 탈출구");
    }

    private void EnsureGridLabelStyle()
    {
        if (gridLabelStyle != null) return;
        gridLabelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            normal = { textColor = Color.white }
        };
    }

    private static Rect CellToGuiRect(Tilemap tilemap, Camera camera, Vector3Int cell)
    {
        Vector3 worldMin = tilemap.CellToWorld(cell);
        Vector3 worldMax = tilemap.CellToWorld(cell + Vector3Int.one);
        Vector3 screenMin = camera.WorldToScreenPoint(worldMin);
        Vector3 screenMax = camera.WorldToScreenPoint(worldMax);
        float x = Mathf.Min(screenMin.x, screenMax.x);
        float y = Screen.height - Mathf.Max(screenMin.y, screenMax.y);
        return new Rect(x, y, Mathf.Abs(screenMax.x - screenMin.x), Mathf.Abs(screenMax.y - screenMin.y));
    }

    private static string GetDirectionLabel(Vector3Int origin, Vector3Int cell)
    {
        Vector3Int delta = cell - origin;
        if (delta == Vector3Int.up) return "+Y";
        if (delta == Vector3Int.down) return "-Y";
        if (delta == Vector3Int.right) return "+X";
        if (delta == Vector3Int.left) return "-X";
        return string.Empty;
    }

    private static void FillRect(Rect rect, Color color)
    {
        Color previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    private static void DrawRectOutline(Rect rect, Color color, float thickness)
    {
        FillRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
        FillRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
        FillRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
        FillRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
    }
}
#endif
