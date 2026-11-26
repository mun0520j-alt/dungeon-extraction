using UnityEngine;

public static partial class LootRuntimeState
{
    private static float escapeScrollRestockProgress;
    private static int escapeScrollPurchaseStock;

    public static bool IsEscapeScrollShopUnlocked => QuestRuntimeState.HasCompleted("q06a") || QuestRuntimeState.HasCompleted("q06b");
    public static int EscapeScrollPurchaseStock => Mathf.Clamp(escapeScrollPurchaseStock, 0, M10DungeonConfig.EscapeScrollStockCap);
    public static float EscapeScrollRestockProgress => Mathf.Max(0f, escapeScrollRestockProgress);
    public static int EscapeScrollTicksUntilRestock => EscapeScrollPurchaseStock >= M10DungeonConfig.EscapeScrollStockCap
        ? M10DungeonConfig.EscapeScrollRestockTicks
        : Mathf.Max(0, Mathf.CeilToInt(M10DungeonConfig.EscapeScrollRestockTicks - EscapeScrollRestockProgress));

    private static void ResetM10ScrollShop()
    {
        escapeScrollRestockProgress = 0f;
        escapeScrollPurchaseStock = 0;
    }

    public static void RestoreM10ScrollShop(float progressTicks, int stock)
    {
        escapeScrollRestockProgress = Mathf.Clamp(progressTicks, 0f, M10DungeonConfig.EscapeScrollRestockTicks - 0.001f);
        escapeScrollPurchaseStock = Mathf.Clamp(stock, 0, M10DungeonConfig.EscapeScrollStockCap);
        if (escapeScrollPurchaseStock >= M10DungeonConfig.EscapeScrollStockCap) escapeScrollRestockProgress = 0f;
    }

    public static void RecordExpeditionTicksForEscapeScrollShop(float elapsedTicks, bool shopWasUnlocked)
    {
        if (!shopWasUnlocked || elapsedTicks <= 0f) return;
        if (escapeScrollPurchaseStock >= M10DungeonConfig.EscapeScrollStockCap)
        {
            escapeScrollRestockProgress = 0f;
            return;
        }

        escapeScrollRestockProgress += elapsedTicks;
        if (escapeScrollRestockProgress < M10DungeonConfig.EscapeScrollRestockTicks) return;
        int restocks = Mathf.FloorToInt(escapeScrollRestockProgress / M10DungeonConfig.EscapeScrollRestockTicks);
        escapeScrollPurchaseStock = Mathf.Min(M10DungeonConfig.EscapeScrollStockCap,
            escapeScrollPurchaseStock + restocks * M10DungeonConfig.EscapeScrollRestockAmount);
        escapeScrollRestockProgress = escapeScrollPurchaseStock >= M10DungeonConfig.EscapeScrollStockCap
            ? 0f
            : escapeScrollRestockProgress % M10DungeonConfig.EscapeScrollRestockTicks;
        Changed?.Invoke();
    }

    private static bool TryConsumeEscapeScrollPurchaseStock()
    {
        if (!IsEscapeScrollShopUnlocked) return Fail("마지막 퀘스트 완료 시 해금됩니다.");
        if (escapeScrollPurchaseStock <= 0) return Fail("탈출 스크롤 구매 가능 수가 없습니다.");
        escapeScrollPurchaseStock--;
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetEscapeScrollShop(float progressTicks, int stock)
    {
        RestoreM10ScrollShop(progressTicks, stock);
        Changed?.Invoke();
    }
#endif
}
