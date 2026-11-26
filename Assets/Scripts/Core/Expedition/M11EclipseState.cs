using UnityEngine;
using UnityEngine.Tilemaps;

// 월식 퀘스트의 원정 상태와 영지 제단 해금 상태를 한 곳에서 소유한다.
public static class M11EclipseState
{
    public static bool AltarUnlocked { get; private set; }
    public static bool AltarEnabled { get; private set; }
    public static bool QuestAcceptedThisRun { get; private set; }
    public static bool FragmentAcquiredThisRun { get; private set; }
    private static bool fragmentSpawned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubsystem()
    {
        AltarUnlocked = false;
        AltarEnabled = false;
        ResetRun();
    }

    public static void BeginRun() => ResetRun();

    private static void ResetRun()
    {
        QuestAcceptedThisRun = false;
        FragmentAcquiredThisRun = false;
        fragmentSpawned = false;
    }

    public static void OnMimicDefeated(Tilemap floor, Vector3Int cell)
    {
        if (ExpeditionSession.CurrentFloor != M10DungeonConfig.FirstBossFloor || AltarUnlocked) return;
        M11EclipseInteractable.Spawn(floor, cell, fragment: false);
        CombatFeedback.AddLine("제단지기가 나타났다");
    }

    public static void AcceptQuest()
    {
        QuestAcceptedThisRun = true;
        CombatFeedback.AddLine("퀘스트 수락: 멈춰버린 밤");
    }

    public static void AcquireFragment()
    {
        FragmentAcquiredThisRun = true;
        CombatFeedback.AddLine("월식의 파편 획득 — 생환해야 보존된다");
    }

    public static void TrySpawnFragment(Tilemap floor, Tilemap walls, Vector3Int playerCell)
    {
        int current = ExpeditionSession.CurrentFloor;
        if (fragmentSpawned || FragmentAcquiredThisRun || !QuestAcceptedThisRun || !DungeonClock.IsNightNow
            || current < 6 || current > 9 || floor == null) return;
        var distances = TilePathfinder.FindReachableDistances(playerCell, walls, floor);
        Vector3Int best = playerCell;
        int farthest = -1;
        foreach (var pair in distances)
        {
            if (pair.Value <= farthest || pair.Key == playerCell || DungeonDoor.IsDoorCell(floor, pair.Key)) continue;
            best = pair.Key;
            farthest = pair.Value;
        }
        if (farthest < 0) return;
        fragmentSpawned = true;
        M11EclipseInteractable.Spawn(floor, best, fragment: true);
        Debug.Log($"[M11 Eclipse] 월식의 파편 생성 — F{current} {best}");
    }

    public static void ResolveRun(ExpeditionResult result)
    {
        if (result == ExpeditionResult.Escaped && FragmentAcquiredThisRun)
        {
            AltarUnlocked = true;
            CombatFeedback.AddLine("월식의 제단이 영지에 생성되었다");
        }
        ResetRun();
    }

    public static bool ToggleAltar()
    {
        if (!AltarUnlocked) return false;
        AltarEnabled = !AltarEnabled;
        return true;
    }

    public static void RestorePersistent(bool unlocked, bool enabled)
    {
        AltarUnlocked = unlocked;
        AltarEnabled = unlocked && enabled;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugUnlockAltar(bool enabled)
    {
        AltarUnlocked = true;
        AltarEnabled = enabled;
        M3MeasurementAutomation.MarkDebugRun();
    }
#endif
}
