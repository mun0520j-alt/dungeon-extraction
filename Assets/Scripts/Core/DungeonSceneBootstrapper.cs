using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Dungeon Tools 9) 실제 던전 씬(DungeonScene)의 진입점 — TickManager/DungeonClock을 준비해두고
// 스케줄을 시작한다. 이름에 일부러 "Test"를 안 붙였다 — Assets/Scripts/Debug/TickQueueTest/
// TickQueueTestBootstrapper(검증용 더미 액터 3개로 스케줄러 자체를 테스트하는 용도)와 헷갈리지
// 않도록, 이쪽은 "실제 게임 로직이 사용하는 진짜 진입점"이라는 걸 이름으로 구분한다
// (2026-08-27 설계 확정: "debug용이랑 실제랑 이름만 다르게").
//
// 이 컴포넌트 자체는 액터를 하나도 만들지 않는다 — 씬에 배치된 PlayerTurnActor가 자기 자신을
// OnEnable에서 TickManager에 등록하므로(자기관리형, 플레이어는 항상 씬에 존재한다는 전제),
// 여기서는 그 등록이 끝난 뒤 스케줄만 시작해주면 된다.
public class DungeonSceneBootstrapper : MonoBehaviour
{
    private static int oppositeExitFallbackCount;

    private void Start()
    {
        // Unity 초기화 순서상 모든 오브젝트의 Awake/OnEnable이 어떤 Start()보다도 먼저 끝나기
        // 때문에, 이 Start()가 실행되는 시점엔 PlayerTurnActor.OnEnable이 이미 TickManager를
        // 만들어서 등록까지 마친 상태다 — 여기서는 그 인스턴스를 그대로 재사용한다.
        var tickManager = TickManager.Instance;
        if (tickManager == null)
        {
            // 혹시 씬에 액터가 하나도 없어서 TickManager가 아직 안 만들어진 경우를 대비한 방어 코드.
            Debug.LogWarning("[DungeonSceneBootstrapper] TickManager.Instance가 없어서 새로 만듭니다 — " +
                              "씬에 PlayerTurnActor 같은 액터가 하나도 없는 상태일 수 있습니다.");
            tickManager = gameObject.AddComponent<TickManager>();
        }

        var dungeonClock = tickManager.GetComponent<DungeonClock>();
        if (dungeonClock == null)
        {
            dungeonClock = tickManager.gameObject.AddComponent<DungeonClock>();
        }
        if (!ExpeditionSession.IsActive) ExpeditionSession.BeginNewRun();
        PlayerTurnActor.Instance?.RestoreHealthForActiveExpedition();

        tickManager.SynchronizeTimeline(ExpeditionSession.ElapsedTicks);
        dungeonClock.RestoreForActiveRun(ExpeditionSession.ElapsedTicks);

        var expeditionManager = FindFirstObjectByType<ExpeditionManager>();
        if (expeditionManager == null) expeditionManager = gameObject.AddComponent<ExpeditionManager>();
        expeditionManager.Initialize(dungeonClock);

        tickManager.BeginSchedule();

        // [2026-09-04 신규] HUD(시계/인벤토리/미니맵) 부착 — 전부 "완전 자체 생성" 컴포넌트라
        // (ScreenSpaceCanvasProvider.cs 참고) 여기서 AddComponent만 해주면 Canvas부터 텍스트/
        // 카메라까지 알아서 만들어진다. Player/Enemy에 HP바를 붙이는 것과 동일한 이유로, 씬에
        // 미리 배치해둘 필요 없이 이 부트스트래퍼가 자동으로 붙여준다 — 매 씬마다 수동으로
        // GameObject를 만들어 컴포넌트를 드래그할 필요가 없다.
        //
        // [2026-09-04 하이라키 배치 지원] 이제 BuildHudInScene.cs(에디터 메뉴)로 이 셋을 씬에
        // 미리 배치해둘 수도 있게 됐다 — 그렇게 미리 배치된 경우 여기서 또 AddComponent를 하면
        // 같은 오브젝트에 컴포넌트가 중복으로 붙어서 Awake가 두 번 도는 꼴이 된다(각 컴포넌트의
        // Build()가 자체적으로 멱등하게 짜여있어도,애초에 중복 컴포넌트 자체가 지저분하다).
        // FindObjectOfType으로 이미 있는지 먼저 확인해서 없을 때만 새로 붙인다.
        if (FindFirstObjectByType<DungeonClockDisplay>() == null) gameObject.AddComponent<DungeonClockDisplay>();
        if (FindFirstObjectByType<InventoryPanel>() == null) gameObject.AddComponent<InventoryPanel>();
        if (FindFirstObjectByType<LootingPanel>() == null) gameObject.AddComponent<LootingPanel>();
        if (FindFirstObjectByType<DungeonBagWindows>() == null) gameObject.AddComponent<DungeonBagWindows>();
        var legacyBagPanel = FindFirstObjectByType<M2BagPanel>();
        if (legacyBagPanel != null) Destroy(legacyBagPanel);
        if (FindFirstObjectByType<MinimapController>() == null) gameObject.AddComponent<MinimapController>();
        if (FindFirstObjectByType<DungeonInteractionMenu>() == null) gameObject.AddComponent<DungeonInteractionMenu>();
        if (FindAnyObjectByType<M10ProgressionHUD>() == null) gameObject.AddComponent<M10ProgressionHUD>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (FindFirstObjectByType<ExpeditionDebugCommands>() == null) gameObject.AddComponent<ExpeditionDebugCommands>();
#endif

        CreateFloorExit();

        Debug.Log($"[DungeonSceneBootstrapper] {ExpeditionSession.CurrentFloor}층 시작 — 누적 {ExpeditionSession.ElapsedTicks:0} Tick.");
    }

    private static void CreateFloorExit()
    {
        var player = PlayerTurnActor.Instance;
        Tilemap floor = player != null ? player.FloorTilemap : null;
        Tilemap walls = player != null ? player.WallsTilemap : null;
        if (floor == null)
        {
            Debug.LogError("[DungeonSceneBootstrapper] 출구를 배치할 Floor Tilemap을 찾지 못했습니다.");
            return;
        }

        Vector3Int playerCell = player.CurrentCell;
        Dictionary<Vector3Int, int> distanceFromSpawn = TilePathfinder.FindReachableDistances(playerCell, walls, floor);
        Vector3Int exitCell = FindFarthestAvailable(distanceFromSpawn, playerCell, default, false, floor);

        bool finalExit = ExpeditionSession.CurrentFloor >= ExpeditionSession.FinalFloor;
        var exitObject = new GameObject(finalExit ? "EscapeExit(AutoCreated)" : "DownStairs(AutoCreated)");
        exitObject.AddComponent<DungeonStairs>().Initialize(floor, exitCell, finalExit);

        if (M10DungeonConfig.HasMidExit(ExpeditionSession.CurrentFloor))
        {
            bool fallbackUsed = false;
            Vector3Int escapeCell;
            if (M10DungeonConfig.IsBossFloor(ExpeditionSession.CurrentFloor))
            {
                // 보스 아레나는 기존 규칙을 유지한다: 계단에서 가장 먼 도달 가능 칸.
                Dictionary<Vector3Int, int> distanceFromStairs = TilePathfinder.FindReachableDistances(exitCell, walls, floor);
                escapeCell = FindFarthestAvailable(distanceFromStairs, playerCell, exitCell, true, floor);
            }
            else
            {
                Vector3Int stairsDirection = exitCell - playerCell;
                escapeCell = FindFarthestOpposite(distanceFromSpawn, playerCell, exitCell, stairsDirection, floor, out bool foundOpposite);
                if (!foundOpposite)
                {
                    fallbackUsed = true;
                    oppositeExitFallbackCount++;
                    Dictionary<Vector3Int, int> distanceFromStairs = TilePathfinder.FindReachableDistances(exitCell, walls, floor);
                    escapeCell = FindFarthestAvailable(distanceFromStairs, playerCell, exitCell, true, floor);
                    Debug.LogWarning($"[M10Exit] Seed {M3MeasurementAutomation.CurrentRunSeed} F{ExpeditionSession.CurrentFloor} 반대쪽 후보 없음 — 계단에서 가장 먼 칸으로 대체 (누적 {oppositeExitFallbackCount}회).");
                }
            }

            new GameObject($"EscapeExit_F{ExpeditionSession.CurrentFloor}(AutoCreated)")
                .AddComponent<DungeonStairs>().Initialize(floor, escapeCell, true);

            int spawnRoom = DungeonRoomRegistry.GetRoomId(floor, playerCell);
            int stairsRoom = DungeonRoomRegistry.GetRoomId(floor, exitCell);
            int escapeRoom = DungeonRoomRegistry.GetRoomId(floor, escapeCell);
            Debug.Log($"[M10Exit] Seed {M3MeasurementAutomation.CurrentRunSeed} F{ExpeditionSession.CurrentFloor} " +
                      $"Spawn {playerCell}/R{spawnRoom}, Stairs {exitCell}/R{stairsRoom}, " +
                      $"Exit {escapeCell}/R{escapeRoom}, fallback={fallbackUsed}.");
        }

        int exitRoomId = DungeonRoomRegistry.GetRoomId(floor, exitCell);
        int enemiesInExitRoom = 0;
        foreach (var enemy in FindObjectsByType<EnemyTurnActor>())
        {
            if (enemy != null && enemy.FloorTilemap == floor
                && DungeonRoomRegistry.GetRoomId(floor, enemy.CurrentCell) == exitRoomId)
                enemiesInExitRoom++;
        }
        int shortestPathTiles = TilePathfinder.FindPath(playerCell, exitCell, walls, floor).Count;
        M3MeasurementAutomation.RecordFloorExitMetrics(shortestPathTiles, enemiesInExitRoom);
    }

    private static Vector3Int FindFarthestOpposite(Dictionary<Vector3Int, int> distances, Vector3Int spawn,
        Vector3Int stairs, Vector3Int stairsDirection, Tilemap floor, out bool found)
    {
        found = false;
        Vector3Int best = spawn;
        int bestDistance = -1;
        foreach (var pair in distances)
        {
            Vector3Int candidate = pair.Key;
            if (candidate == spawn || candidate == stairs || IsPlacementBlocked(floor, candidate)) continue;
            Vector3Int direction = candidate - spawn;
            if (direction.x * stairsDirection.x + direction.y * stairsDirection.y >= 0) continue;
            if (!IsBetter(pair.Value, candidate, bestDistance, best)) continue;
            found = true;
            best = candidate;
            bestDistance = pair.Value;
        }
        return best;
    }

    private static Vector3Int FindFarthestAvailable(Dictionary<Vector3Int, int> distances, Vector3Int spawn,
        Vector3Int excluded, bool hasExcluded, Tilemap floor)
    {
        Vector3Int best = spawn;
        int bestDistance = -1;
        foreach (var pair in distances)
        {
            Vector3Int candidate = pair.Key;
            if (candidate == spawn || (hasExcluded && candidate == excluded) || IsPlacementBlocked(floor, candidate)) continue;
            if (!IsBetter(pair.Value, candidate, bestDistance, best)) continue;
            best = candidate;
            bestDistance = pair.Value;
        }
        return best;
    }

    private static bool IsBetter(int distance, Vector3Int candidate, int bestDistance, Vector3Int best)
    {
        return distance > bestDistance
            || (distance == bestDistance && (candidate.y < best.y || (candidate.y == best.y && candidate.x < best.x)));
    }

    private static bool IsPlacementBlocked(Tilemap floor, Vector3Int cell)
    {
        return (TickManager.Instance != null && TickManager.Instance.IsCellOccupied(cell))
            || DungeonChest.IsCellBlocked(cell)
            || DungeonDoor.IsDoorCell(floor, cell)
            || GroundLootPile.IsCellBlocked(floor, cell);
    }
}
