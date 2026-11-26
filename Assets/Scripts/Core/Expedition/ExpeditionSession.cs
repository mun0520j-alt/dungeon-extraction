using System.Collections.Generic;
using UnityEngine;

// 씬을 다시 불러도 한 원정의 층/Tick/결과가 유지되는 런타임 세션이다.
public static class ExpeditionSession
{
    public const int FirstFloor = 1;
    public static int FinalFloor => M10DungeonConfig.FinalFloor;

    public static bool IsActive { get; private set; }
    public static int CurrentFloor { get; private set; } = FirstFloor;
    public static float ElapsedTicks { get; private set; }
    public static ExpeditionResultData LastResult { get; private set; }
    public static int PlayerCurrentHP { get; private set; }
    public static bool HasPlayerHealth { get; private set; }

    private static readonly List<string> acquiredItems = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        IsActive = false;
        CurrentFloor = FirstFloor;
        ElapsedTicks = 0f;
        LastResult = null;
        PlayerCurrentHP = 0;
        HasPlayerHealth = false;
        acquiredItems.Clear();
    }

    public static void BeginNewRun()
    {
        M3MeasurementAutomation.PrepareNewSession();
        LootRuntimeState.BeginNewRun();
        ExpeditionRunRecord.BeginRun();
        M10RunProgression.BeginRun();
        IsActive = true;
        CurrentFloor = FirstFloor;
        ElapsedTicks = 0f;
        LastResult = null;
        PlayerCurrentHP = 0;
        HasPlayerHealth = false;
        acquiredItems.Clear();
    }

    public static void RecordElapsedTicks(float elapsedTicks)
    {
        if (IsActive && elapsedTicks > ElapsedTicks) ElapsedTicks = elapsedTicks;
    }

    public static void RecordPlayerHealth(int currentHP)
    {
        if (!IsActive) return;
        PlayerCurrentHP = Mathf.Max(0, currentHP);
        HasPlayerHealth = true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetElapsedTicks(float elapsedTicks)
    {
        if (IsActive) ElapsedTicks = Mathf.Max(0f, elapsedTicks);
    }

    public static void DebugSetCurrentFloor(int floor)
    {
        if (IsActive) CurrentFloor = Mathf.Clamp(floor, FirstFloor, FinalFloor);
    }
#endif

    public static bool TryAdvanceFloor()
    {
        if (!IsActive || CurrentFloor >= FinalFloor) return false;
        CurrentFloor++;
        M10RunProgression.RecordFloorArrival(CurrentFloor, ElapsedTicks);
        return true;
    }

    public static void Complete(ExpeditionResult result)
    {
        if (!IsActive) return;

        var carriedItems = new List<LootItemInstance>(LootRuntimeState.RunBag);
        carriedItems.AddRange(LootRuntimeState.EquippedLoot.Values);
        ExpeditionRunSnapshot run = ExpeditionRunRecord.Complete(result, CurrentFloor, ElapsedTicks);

        LastResult = new ExpeditionResultData
        {
            Result = result,
            ElapsedTicks = ElapsedTicks,
            ReachedFloor = CurrentFloor,
            AcquiredItems = new List<string>(acquiredItems)
        };
        M3MeasurementAutomation.Finish(result, ElapsedTicks, CurrentFloor);
        bool scrollShopWasUnlocked = LootRuntimeState.IsEscapeScrollShopUnlocked;
        QuestRuntimeState.RecordExpeditionCompleted(run, carriedItems);
        LootRuntimeState.RecordExpeditionTicksForEscapeScrollShop(ElapsedTicks, scrollShopWasUnlocked);
        LootRuntimeState.ResolveRun(result);
        M10RunProgression.EndRun();
        IsActive = false;
        M5SaveService.CompleteExpedition();
    }
}
