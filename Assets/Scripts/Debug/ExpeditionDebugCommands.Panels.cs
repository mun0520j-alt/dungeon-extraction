#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class ExpeditionDebugCommands
{
    private void DrawM6ItemPanel()
    {
        float height = Mathf.Min(620f, Screen.height - 32f);
        GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 210f, 16f, 420f, height),
            "M6 아이템 지급 (F9)", GUI.skin.window);
        bool inDungeon = ExpeditionSession.IsActive && PlayerTurnActor.Instance != null;
        GUILayout.Label(inDungeon
            ? "버튼 클릭: 플레이어 발밑 바닥 더미에 생성 · 측정 원정 제외"
            : "버튼 클릭: Stash에 생성 (공간 부족 시 임시 보관함)");
        GUILayout.Label(inDungeon
            ? $"출격 가방: {LootRuntimeState.RunBagGrid.Width}×{LootRuntimeState.RunBagGrid.Height} / 아이템 {LootRuntimeState.RunBag.Count}개"
            : $"Stash: {LootRuntimeState.StashGrid.Width}×{LootRuntimeState.StashGrid.Height} / 아이템 {LootRuntimeState.Stash.Count}개");
        if (GUILayout.Button($"보험 대기 {insuranceAdvanceTicks:0} Tick 진행", GUILayout.Height(30f)))
        {
            M3MeasurementAutomation.MarkDebugRun();
            LootRuntimeState.AdvanceInsuranceTicks(insuranceAdvanceTicks);
            M5SaveService.SaveNow();
            Debug.Log($"[QA] 보험 대기 시간을 {insuranceAdvanceTicks:0} Tick 진행했습니다.");
        }
        DrawM10QaControls(inDungeon);
        itemPanelScroll = GUILayout.BeginScrollView(itemPanelScroll);
        foreach (LootItemData item in LootItemDatabase.Items)
        {
            if (item == null) continue;
            string size = item.Type == LootItemType.Bag
                ? $"아이템 {item.GridWidth}×{item.GridHeight} → 가방 {item.BagGridWidth}×{item.BagGridHeight}"
                : $"{item.GridWidth}×{item.GridHeight}";
            if (GUILayout.Button($"[{item.Rarity}] {item.Name} ({item.Id}) · {size}", GUILayout.Height(30f)))
                GiveDebugItem(item);
        }
        GUILayout.EndScrollView();
        if (GUILayout.Button("닫기 (F9)", GUILayout.Height(32f))) showM6ItemPanel = false;
        GUILayout.EndArea();
    }

    private void DrawM10QaControls(bool inDungeon)
    {
        GUILayout.Label("M10 던전 확장 QA");
        if (inDungeon)
        {
            GUILayout.BeginHorizontal();
            for (int floor = 1; floor <= ExpeditionSession.FinalFloor; floor++)
            {
                int target = floor;
                if (GUILayout.Button(target.ToString(), GUILayout.Width(34f))) JumpToFloor(target);
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("현재 층 보스 처치")) DefeatCurrentFloorBoss();
            if (GUILayout.Button("엘리트 소환"))
            {
                M10EnemyDefinition elite = System.Linq.Enumerable.FirstOrDefault(M10EnemyDatabase.All, value => value.IsElite);
                if (elite != null) SpawnDebugEnemy(elite.Id);
            }
            if (GUILayout.Button("엘리트 스크롤 드롭")) DropEliteEscapeScroll();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            qaEnemyIdInput = GUILayout.TextField(qaEnemyIdInput ?? string.Empty);
            if (GUILayout.Button("지정 몹 소환", GUILayout.Width(100f))) SpawnDebugEnemy(qaEnemyIdInput);
            GUILayout.EndHorizontal();
            DrawElapsedTickControls();
            GUILayout.Label($"레벨 Lv.{M10RunProgression.Level} · XP {M10RunProgression.Experience}/{M10RunProgression.NextLevelXP}");
            GUILayout.Label("최근 경험치");
            if (M10RunProgression.RecentExperienceEvents.Count == 0) GUILayout.Label("  기록 없음");
            else
                foreach (string entry in M10RunProgression.RecentExperienceEvents)
                    GUILayout.Label($"  {entry}");
            GUILayout.BeginHorizontal();
            for (int level = 1; level <= M10RunProgression.MaxLevel; level++)
            {
                int targetLevel = level;
                if (GUILayout.Button(level.ToString(), GUILayout.Width(28f))) M10RunProgression.DebugSetLevel(targetLevel);
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button("경험치 +100")) { M3MeasurementAutomation.MarkDebugRun(); M10RunProgression.AddExperience(100, "QA"); }
            GUILayout.Label(M10RunProgression.IsHealing
                ? $"포션 회복: {M10RunProgression.ActivePotionId}, 남은 {M10RunProgression.HealingTicksRemaining:0.#} Tick"
                : "포션 회복: 없음");
        }
        DrawScrollShopControls();
    }

    // 던전 경과 시간: 현재 값 표시 + 바로 쓰는 버튼(MIA 경계 직전, +10/+100) + 직접 입력
    private void DrawElapsedTickControls()
    {
        float mia = DungeonClock.MIAThresholdTicks;
        float now = ExpeditionSession.ElapsedTicks;
        GUILayout.Label($"던전 경과: {now:0} / {mia:0} Tick (MIA까지 {Mathf.Max(0f, mia - now):0} Tick = {Mathf.Max(0f, mia - now) * DungeonClock.MinutesPerTick:0}분)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"MIA 직전 ({mia - 1:0})")) SetElapsedTicks(mia - 1f);
        if (GUILayout.Button("+10", GUILayout.Width(44f))) SetElapsedTicks(now + 10f);
        if (GUILayout.Button("+100", GUILayout.Width(50f))) SetElapsedTicks(now + 100f);
        qaElapsedInput = GUILayout.TextField(qaElapsedInput ?? "0", GUILayout.Width(60f));
        if (GUILayout.Button("이 값으로", GUILayout.Width(70f)) && float.TryParse(qaElapsedInput, out float target))
            SetElapsedTicks(target);
        GUILayout.EndHorizontal();
    }

    // 탈출 스크롤 상점: 현재 상태 표시 + 자주 쓰는 상태 버튼 + 직접 입력(항목 이름 표시)
    private void DrawScrollShopControls()
    {
        int cap = M10DungeonConfig.EscapeScrollStockCap;
        int restock = M10DungeonConfig.EscapeScrollRestockTicks;
        GUILayout.Label(LootRuntimeState.IsEscapeScrollShopUnlocked
            ? $"스크롤 상점: 해금됨 · 재고 {LootRuntimeState.EscapeScrollPurchaseStock}/{cap} · 충전 진행 {LootRuntimeState.EscapeScrollRestockProgress:0}/{restock} Tick (원정 끝날 때 반영)"
            : "스크롤 상점: 잠김 (q06a·q06b 완료 시 해금)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("해금")) { M3MeasurementAutomation.MarkDebugRun(); QuestRuntimeState.DebugUnlockEscapeScrollShop(); M5SaveService.SaveNow(); }
        if (GUILayout.Button($"충전 직전 ({restock - 1}/재고 0)")) ApplyScrollShop(restock - 1, 0);
        if (GUILayout.Button($"재고 가득 ({cap})")) ApplyScrollShop(0, cap);
        if (GUILayout.Button("재고 0")) ApplyScrollShop(0, 0);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("충전 진행 Tick", GUILayout.Width(90f));
        qaShopProgressInput = GUILayout.TextField(qaShopProgressInput ?? "0", GUILayout.Width(60f));
        GUILayout.Label($"재고(0~{cap})", GUILayout.Width(70f));
        qaShopStockInput = GUILayout.TextField(qaShopStockInput ?? "0", GUILayout.Width(35f));
        if (GUILayout.Button("적용", GUILayout.Width(45f))
            && float.TryParse(qaShopProgressInput, out float progress)
            && int.TryParse(qaShopStockInput, out int stock))
            ApplyScrollShop(progress, stock);
        GUILayout.EndHorizontal();
    }

    private static void ApplyScrollShop(float progress, int stock)
    {
        M3MeasurementAutomation.MarkDebugRun();
        LootRuntimeState.DebugSetEscapeScrollShop(progress, stock);
        M5SaveService.SaveNow();
        Debug.Log($"[QA] 스크롤 상점: 충전 진행 {progress:0} Tick, 재고 {stock}");
    }

    private static void GiveDebugItem(LootItemData definition)
    {
        if (definition == null) return;
        M3MeasurementAutomation.MarkDebugRun();
        var item = new LootItemInstance(definition);
        PlayerTurnActor player = PlayerTurnActor.Instance;
        if (ExpeditionSession.IsActive && player != null && player.FloorTilemap != null)
        {
            if (!GroundLootPile.TryAddOrCreate(player.FloorTilemap, player.CurrentCell, item, out _, "QA 아이템 더미"))
            {
                Debug.LogWarning($"[QA] {definition.Name} 생성 실패 — 플레이어 발밑 바닥 더미 공간 부족");
                return;
            }
            Debug.Log($"[QA] {definition.Name} 생성 완료 — 플레이어 발밑 바닥 더미");
            return;
        }

        LootRuntimeState.AddRewardToStash(item);
        M5SaveService.SaveNow();
        Debug.Log($"[QA] {definition.Name} 생성 완료 — Stash (공간 부족 시 임시 보관함)");
    }

    private void DrawM3Panel()
    {
        GUILayout.BeginArea(new Rect(Screen.width - 370f, 16f, 354f, 270f), "M3 측정 자동화 (F6)", GUI.skin.window);
        GUILayout.Label($"현재 모드: {M3MeasurementAutomation.CurrentSeedMode}");
        GUILayout.Label($"이번 원정 Seed: {M3MeasurementAutomation.CurrentRunSeed}");
        GUILayout.Label("Run Seed");
        seedInput = GUILayout.TextField(seedInput ?? string.Empty);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Seed 적용 후 재시작"))
        {
            if (int.TryParse(seedInput, out int seed))
            {
                M3MeasurementAutomation.SetConfiguredSeed(seed);
                ExpeditionSession.BeginNewRun();
                SceneManager.LoadScene(DungeonSceneName);
            }
            else Debug.LogWarning("[M3Measure] Seed는 정수로 입력하세요.");
        }
        if (GUILayout.Button("Seed 복사"))
        {
            GUIUtility.systemCopyBuffer = M3MeasurementAutomation.CurrentRunSeed.ToString();
            Debug.Log($"[M3Measure] Seed {GUIUtility.systemCopyBuffer} 복사 완료");
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Random 모드로 전환 (다음 출격부터)"))
        {
            M3MeasurementAutomation.SetRandomMode();
            Debug.Log("[M3Measure] 다음 출격부터 Random Seed 모드를 사용합니다.");
        }
        if (GUILayout.Button("현재 맵 자동 검증"))
        {
            var manager = FindFirstObjectByType<DungeonRunManager>();
            if (manager != null) manager.ValidateGeneratedRun();
        }
        GUILayout.Label("원정 종료 시 CSV 자동 저장:");
        GUILayout.TextArea(M3MeasurementAutomation.OutputPath, GUILayout.Height(42f));
        GUILayout.EndArea();
    }
}
#endif
