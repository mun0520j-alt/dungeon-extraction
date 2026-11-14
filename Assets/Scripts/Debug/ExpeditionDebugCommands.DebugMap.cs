#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Tilemaps;

public sealed partial class ExpeditionDebugCommands
{
    private static bool BuildNearPlayer()
    {
        var player = PlayerTurnActor.Instance;
        if (player == null || player.FloorTilemap == null || player.WallsTilemap == null)
        {
            Debug.LogWarning("[QA] 플레이어 또는 Floor/Walls Tilemap을 찾지 못해 디버그 맵을 만들 수 없습니다.");
            return false;
        }

        Tilemap floor = player.FloorTilemap;
        Tilemap walls = player.WallsTilemap;
        Vector3Int center = player.CurrentCell;
        TileBase floorTile = floor.GetTile(center);
        if (floorTile == null)
        {
            Debug.LogWarning("[QA] 플레이어 칸의 바닥 타일을 찾지 못했습니다.");
            return false;
        }

        for (int y = -4; y <= 4; y++)
        {
            for (int x = -4; x <= 4; x++)
            {
                var cell = center + new Vector3Int(x, y, 0);
                floor.SetTile(cell, floorTile);
                walls.SetTile(cell, null);
            }
        }

        ExpeditionSession.DebugSetCurrentFloor(ExpeditionSession.FinalFloor);
        ReplaceExit(floor, center + new Vector3Int(2, 0, 0));
        PlaceEnemy(center + new Vector3Int(0, 2, 0));
        CreateDebugChest(floor, center + new Vector3Int(-2, 0, 0));
        FogOfWarController.Instance?.UpdateVisibility(center, 8);

        Debug.Log("[QA] M2 디버그 맵 구성 완료: 왼쪽 실제 상자 / 위 Enemy / 오른쪽 3층 탈출구. F5=가방 채우기.");
        return true;
    }

    private static void ReplaceExit(Tilemap floor, Vector3Int cell)
    {
        foreach (var stairs in Object.FindObjectsByType<DungeonStairs>(FindObjectsSortMode.None)) Object.Destroy(stairs.gameObject);
        new GameObject("EscapeExit(QA)").AddComponent<DungeonStairs>().Initialize(floor, cell, true);
    }

    private static void PlaceEnemy(Vector3Int cell)
    {
        var enemies = Object.FindObjectsByType<EnemyTurnActor>(FindObjectsSortMode.InstanceID);
        if (enemies.Length == 0)
        {
            Debug.LogWarning("[QA] 배치할 Enemy가 없습니다. DungeonRunManager의 Enemy 생성을 확인하세요.");
            return;
        }

        enemies[0].DebugTeleportTo(cell);
        enemies[0].gameObject.SetActive(true);
        for (int i = 1; i < enemies.Length; i++) enemies[i].gameObject.SetActive(false);
    }

    private static void CreateDebugChest(Tilemap floor, Vector3Int cell)
    {
        var previous = GameObject.Find("DebugChest(M2)");
        if (previous != null) return;

        var chest = new GameObject("DebugChest(M2)");
        chest.AddComponent<DungeonChest>().Initialize(
            floor, cell, ExpeditionSession.CurrentFloor, DebugChestSeed);
    }
}
#endif
