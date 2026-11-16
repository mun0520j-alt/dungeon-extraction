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
        GUILayout.Label("버튼 클릭: 현재 출격 가방에 즉시 지급 · 측정 원정 제외");
        GUILayout.Label($"가방: {LootRuntimeState.RunBagGrid.Width}×{LootRuntimeState.RunBagGrid.Height} / 아이템 {LootRuntimeState.RunBag.Count}개");
        if (GUILayout.Button($"보험 대기 {insuranceAdvanceTicks:0} Tick 진행", GUILayout.Height(30f)))
        {
            M3MeasurementAutomation.MarkDebugRun();
            LootRuntimeState.AdvanceInsuranceTicks(insuranceAdvanceTicks);
            M5SaveService.SaveNow();
            Debug.Log($"[QA] 보험 대기 시간을 {insuranceAdvanceTicks:0} Tick 진행했습니다.");
        }
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

    private static void GiveDebugItem(LootItemData definition)
    {
        if (definition == null) return;
        M3MeasurementAutomation.MarkDebugRun();
        if (!LootRuntimeState.TryAddToRunBag(new LootItemInstance(definition)))
        {
            Debug.LogWarning($"[QA] {definition.Name} 지급 실패 — {LootRuntimeState.LastFailureReason ?? "가방 공간 부족"}");
            return;
        }
        Debug.Log($"[QA] {definition.Name} 지급 완료 — 출격 가방 {LootRuntimeState.RunBag.Count}개");
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
