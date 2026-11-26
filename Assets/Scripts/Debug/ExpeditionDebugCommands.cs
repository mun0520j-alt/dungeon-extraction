#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// QA 전용 디버그 명령. 에디터 또는 Development Build에만 포함된다(공개 빌드 제외).
// 파일 구성 (partial):
//   ExpeditionDebugCommands.cs          — 단축키 표, 명령(Tick·HP·5층·가방·더미·보험)
//   ExpeditionDebugCommands.Panels.cs   — F6 측정 패널, F9 아이템 지급 패널
//   ExpeditionDebugCommands.TileGrid.cs — F3 타일 격자 오버레이와 GUI 그리기 도우미
//   ExpeditionDebugCommands.DebugMap.cs — F4 빠른 검증 맵
// 새 단축키는 Hotkeys 표에만 추가한다. 도움말 문구는 표에서 자동 생성된다.
// 전체 QA 도구 목록: docs/QA_DEBUG_TOOLS.md
public sealed partial class ExpeditionDebugCommands : MonoBehaviour
{
    private const string DungeonSceneName = "InDungeon";
    private const int DebugChestSeed = 20261105;

    [Min(0f)] public float targetElapsedTicks = 2879f; // M10: MIA 경계 직전 (2880 = 24시간)
    [Min(1)] public int targetPlayerHP = 1;
    [Min(1f)] public float insuranceAdvanceTicks = 3000f;
    [SerializeField] private bool showTileGrid;
    [SerializeField] private bool showM3Panel;
    [SerializeField] private bool showM6ItemPanel;
    private string seedInput;
    private Vector2 itemPanelScroll;
    private Vector2 itemPanelOuterScroll;
    private string qaEnemyIdInput = "masked_orc";
    private string qaShopProgressInput = "0";
    private string qaShopStockInput = "0";
    private string qaElapsedInput = "2879";
    private GUIStyle gridLabelStyle;

    private readonly struct Hotkey
    {
        public readonly KeyCode Key;
        public readonly string Label;
        public readonly System.Action<ExpeditionDebugCommands> Run;
        public Hotkey(KeyCode key, string label, System.Action<ExpeditionDebugCommands> run) { Key = key; Label = label; Run = run; }
    }

    private static readonly Hotkey[] Hotkeys =
    {
        new Hotkey(KeyCode.F3, "Grid", c => c.ToggleTileGrid()),
        new Hotkey(KeyCode.F4, "빠른 배치", c => c.BuildM1DebugMap()),
        new Hotkey(KeyCode.F5, "가방 채우기", c => c.FillM2Bag()),
        new Hotkey(KeyCode.F6, "측정", c => c.showM3Panel = !c.showM3Panel),
        new Hotkey(KeyCode.F7, "5층", c => c.JumpToFloorFive()),
        new Hotkey(KeyCode.F8, "더미", c => c.FillGroundLootPile()),
        new Hotkey(KeyCode.F9, "아이템", c => c.showM6ItemPanel = !c.showM6ItemPanel),
    };

    private static string HotkeyHelpLine()
    {
        var parts = new System.Collections.Generic.List<string>(Hotkeys.Length);
        foreach (Hotkey hotkey in Hotkeys) parts.Add($"{hotkey.Key}: {hotkey.Label}");
        return string.Join(" / ", parts);
    }

    private void Update()
    {
        // 패널이 열린 동안 뒤의 플레이어 이동·상호작용을 막는다(텍스트 입력·버튼 클릭이 월드로 새지 않게).
        // 자기 단축키는 BlocksForPauseMenu만 확인하므로 F6·F9로 패널을 닫을 수 있다.
        if (!PauseMenuController.BlocksForPauseMenu)
        {
            if (seedInput == null) seedInput = M3MeasurementAutomation.ConfiguredSeed.ToString();
            foreach (Hotkey hotkey in Hotkeys)
                if (Input.GetKeyDown(hotkey.Key)) hotkey.Run(this);
        }
        PauseMenuController.DebugPanelOpen = showM3Panel || showM6ItemPanel;
    }

    private void OnDisable()
    {
        PauseMenuController.DebugPanelOpen = false;
    }

    private void OnGUI()
    {
        if (showTileGrid) DrawTileGridOverlay();
        if (showM3Panel) DrawM3Panel();
        if (showM6ItemPanel) DrawM6ItemPanel();
    }

    [ContextMenu("QA/Toggle Tile Grid (F3)")]
    public void ToggleTileGrid()
    {
        showTileGrid = !showTileGrid;
    }

    [ContextMenu("QA/Set Elapsed Tick")]
    public void SetConfiguredTick()
    {
        SetElapsedTicks(targetElapsedTicks);
    }

    [ContextMenu("QA/Build M1 Debug Map")]
    public void BuildM1DebugMap()
    {
        M3MeasurementAutomation.MarkDebugRun();
        BuildNearPlayer();
    }

    [ContextMenu("QA/Fill M2 Bag (F5)")]
    public void FillM2Bag()
    {
        LootItemData definition = null;
        foreach (var candidate in LootItemDatabase.Items)
        {
            if (candidate.DropWeight <= 0) continue;
            definition = candidate;
            break;
        }
        if (definition == null)
        {
            Debug.LogWarning("[QA] 가방을 채울 아이템 정의가 없습니다.");
            return;
        }

        M3MeasurementAutomation.MarkDebugRun();
        while (LootRuntimeState.TryAddToRunBag(new LootItemInstance(definition))) { }

        Debug.Log($"[QA] M6 격자 가방을 채웠습니다. 아이템 {LootRuntimeState.RunBag.Count}개, "
            + $"격자 {LootRuntimeState.RunBagGrid.Width}x{LootRuntimeState.RunBagGrid.Height}.");
    }

    [ContextMenu("QA/Set Player HP")]
    public void SetConfiguredPlayerHP()
    {
        if (PlayerTurnActor.Instance == null) return;
        M3MeasurementAutomation.MarkDebugRun();
        PlayerTurnActor.Instance.DebugSetCurrentHP(targetPlayerHP);
    }

    [ContextMenu("QA/Advance Insurance Ticks")]
    public void AdvanceInsuranceTicks()
    {
        M3MeasurementAutomation.MarkDebugRun();
        LootRuntimeState.AdvanceInsuranceTicks(insuranceAdvanceTicks);
        M5SaveService.SaveNow();
    }

    [ContextMenu("QA/Jump To Floor 5 (F7)")]
    public void JumpToFloorFive()
    {
        JumpToFloor(5);
    }

    public void JumpToFloor(int floor)
    {
        if (!ExpeditionSession.IsActive)
        {
            Debug.LogWarning("[QA] 활성 원정이 없어 층을 이동할 수 없습니다.");
            return;
        }

        M3MeasurementAutomation.MarkDebugRun();
        int target = Mathf.Clamp(floor, ExpeditionSession.FirstFloor, ExpeditionSession.FinalFloor);
        ExpeditionSession.DebugSetCurrentFloor(target);
        Debug.Log($"[QA] 누적 {ExpeditionSession.ElapsedTicks:0.##} Tick과 현재 장비·가방·HP를 유지하고 {target}층으로 이동합니다.");
        SceneManager.LoadScene(DungeonSceneName);
    }

    public void DefeatCurrentFloorBoss()
    {
        int floor = ExpeditionSession.CurrentFloor;
        GameObject boss = GameObject.Find($"Boss_F{floor}");
        EnemyTurnActor actor = boss != null ? boss.GetComponent<EnemyTurnActor>() : null;
        if (actor == null)
        {
            Debug.LogWarning($"[QA] {floor}층 보스를 찾지 못했습니다.");
            return;
        }
        M3MeasurementAutomation.MarkDebugRun();
        actor.TakeDamage(int.MaxValue);
        Debug.Log($"[QA] {floor}층 보스 처치 처리 완료.");
    }

    public void SpawnDebugEnemy(string enemyId)
    {
        DungeonRunManager manager = FindFirstObjectByType<DungeonRunManager>();
        if (manager == null || !manager.DebugSpawnEnemy(enemyId))
        {
            Debug.LogWarning($"[QA] 지정 몹 생성 실패: {enemyId}");
            return;
        }
        M3MeasurementAutomation.MarkDebugRun();
    }

    public void DropEliteEscapeScroll()
    {
        PlayerTurnActor player = PlayerTurnActor.Instance;
        LootItemData definition = LootItemDatabase.FindById("escape_scroll");
        if (player?.FloorTilemap == null || definition == null) return;
        M3MeasurementAutomation.MarkDebugRun();
        var item = new LootItemInstance(definition, foundInRaid: true) { IsExpeditionBound = true };
        GroundLootPile.TryAddOrCreate(player.FloorTilemap, player.CurrentCell, item, out _, "QA 엘리트 전리품");
        Debug.Log("[QA] 원정 한정 엘리트 탈출 스크롤을 플레이어 발밑에 생성했습니다.");
    }

    [ContextMenu("QA/Fill Ground Loot Pile (F8)")]
    public void FillGroundLootPile()
    {
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (player == null || player.FloorTilemap == null)
        {
            Debug.LogWarning("[QA] 플레이어 또는 Floor Tilemap을 찾지 못해 바닥 더미를 채울 수 없습니다.");
            return;
        }

        M3MeasurementAutomation.MarkDebugRun();
        var definition = new LootItemData
        {
            Id = "qa_ground_pile_filler",
            Name = "QA 더미 채움",
            Type = LootItemType.Armor,
            Rarity = LootRarity.Normal,
            GridWidth = 1,
            GridHeight = 1
        };

        GroundLootPile pile = null;
        int added = 0;
        while (added < 25 && GroundLootPile.TryAddOrCreate(
            player.FloorTilemap, player.CurrentCell, new LootItemInstance(definition), out pile, "QA 가득 찬 바닥 더미"))
        {
            added++;
        }

        Debug.Log($"[QA] F8 — 플레이어 칸 바닥 더미 {pile?.Grid.Placements.Count ?? 0}/25. "
            + "인벤토리 아이템 버리기로 공간 부족·0Tick·아이템 유지를 확인하세요.");
    }

    public static bool SetElapsedTicks(float elapsedTicks)
    {
        var tickManager = TickManager.Instance;
        var dungeonClock = tickManager != null ? tickManager.GetComponent<DungeonClock>() : null;
        if (tickManager == null || dungeonClock == null || !ExpeditionSession.IsActive)
        {
            Debug.LogWarning("[QA] 활성 원정의 TickManager/DungeonClock을 찾지 못해 Tick을 설정하지 못했습니다.");
            return false;
        }

        M3MeasurementAutomation.MarkDebugRun();
        float safeTick = Mathf.Max(0f, elapsedTicks);
        tickManager.DebugSetTimeline(safeTick);
        dungeonClock.DebugSetElapsedTicks(safeTick);
        ExpeditionSession.DebugSetElapsedTicks(safeTick);
        Debug.Log($"[QA] 누적 Tick을 {safeTick:0.##}(으)로 설정했습니다.");
        return true;
    }
}
#endif
