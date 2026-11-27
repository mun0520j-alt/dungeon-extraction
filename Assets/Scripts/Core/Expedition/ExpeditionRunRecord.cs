using System;
using System.Collections.Generic;
using UnityEngine;

// 원정에서 일어난 사실만 기록한다. 퀘스트·보상 규칙은 이 상태를 직접 소유하지 않는다.
public static class ExpeditionRunRecord
{
    public static bool BossDefeated { get; private set; }
    public static int BossDefeatedFloor { get; private set; }
    private static readonly Dictionary<int, int> killsByFloor = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> eliteKillsByFloor = new Dictionary<int, int>();
    public static ExpeditionRunSnapshot LastCompleted { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        BossDefeated = false;
        BossDefeatedFloor = 0;
        killsByFloor.Clear();
        eliteKillsByFloor.Clear();
        LastCompleted = null;
    }

    public static void BeginRun()
    {
        BossDefeated = false;
        BossDefeatedFloor = 0;
        killsByFloor.Clear();
        eliteKillsByFloor.Clear();
    }

    public static void RecordBossDefeated()
    {
        if (!ExpeditionSession.IsActive) return;
        BossDefeated = true;
        BossDefeatedFloor = ExpeditionSession.CurrentFloor;
        Debug.Log($"[RunRecord] {ExpeditionSession.CurrentFloor}층 보스 처치 기록");
    }

    public static void RecordEnemyDefeated(bool elite)
    {
        if (!ExpeditionSession.IsActive) return;
        int floor = ExpeditionSession.CurrentFloor;
        killsByFloor[floor] = Count(killsByFloor, floor) + 1;
        if (elite) eliteKillsByFloor[floor] = Count(eliteKillsByFloor, floor) + 1;
    }

    public static ExpeditionRunSnapshot Complete(ExpeditionResult result, int reachedFloor, float elapsedTicks)
    {
        LastCompleted = new ExpeditionRunSnapshot
        {
            Result = result,
            ReachedFloor = reachedFloor,
            ElapsedTicks = elapsedTicks,
            BossDefeated = BossDefeated,
            BossDefeatedFloor = BossDefeatedFloor,
            KillsByFloor = Copy(killsByFloor),
            EliteKillsByFloor = Copy(eliteKillsByFloor)
        };
        return LastCompleted;
    }

    private static int Count(Dictionary<int,int> source, int key) => source.TryGetValue(key, out int value) ? value : 0;
    private static List<FloorKillCount> Copy(Dictionary<int,int> source)
    {
        var result = new List<FloorKillCount>();
        foreach (var pair in source) result.Add(new FloorKillCount { Floor=pair.Key, Count=pair.Value });
        return result;
    }
}

[Serializable]
public sealed class ExpeditionRunSnapshot
{
    public ExpeditionResult Result;
    public int ReachedFloor;
    public float ElapsedTicks;
    public bool BossDefeated;
    public int BossDefeatedFloor;
    public List<FloorKillCount> KillsByFloor = new List<FloorKillCount>();
    public List<FloorKillCount> EliteKillsByFloor = new List<FloorKillCount>();

    public int CountKills(int minFloor, int maxFloor, bool elite)
    {
        List<FloorKillCount> source = elite ? EliteKillsByFloor : KillsByFloor;
        int total = 0;
        foreach (FloorKillCount entry in source)
            if (entry.Floor >= minFloor && (maxFloor <= 0 || entry.Floor <= maxFloor)) total += entry.Count;
        return total;
    }
}

[Serializable]
public sealed class FloorKillCount { public int Floor; public int Count; }
