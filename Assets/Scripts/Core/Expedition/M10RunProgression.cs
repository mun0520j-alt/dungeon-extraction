using System.Collections.Generic;
using UnityEngine;

// 원정 안에서만 유지되는 레벨·경험치·지속 회복 상태. 영지 세이브에는 저장하지 않는다.
public static class M10RunProgression
{
    private static readonly HashSet<int> visitedFloors = new HashSet<int>();
    private static int level = 1;
    private static int experience;
    private static float lastProcessedTick;
    private static float healRemaining;
    private static float healTicksRemaining;
    private static float healFraction;
    private static string activePotionId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly List<string> recentExperienceEvents = new List<string>();
    public static IReadOnlyList<string> RecentExperienceEvents => recentExperienceEvents;
#endif

    public static int Level => level;
    public static int Experience => experience;
    public static int MaxLevel => 12;
    public static int HPBonus => M10ProgressionDatabase.Level(level).HPBonus;
    public static int AttackBonus => M10ProgressionDatabase.Level(level).AttackBonus;
    public static int DefenseBonus => M10ProgressionDatabase.Level(level).DefenseBonus;
    public static bool IsHealing => healRemaining > 0f && healTicksRemaining > 0f;
    public static float HealingTicksRemaining => healTicksRemaining;
    public static string ActivePotionId => activePotionId;
    public static int NextLevelXP => level >= MaxLevel ? experience : M10ProgressionDatabase.Level(level + 1).CumulativeXP;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSubsystem() => ResetRun();

    public static void BeginRun()
    {
        ResetRun();
        CombatFeedback.Clear();
        RecordFloorArrival(1, 0f);
    }

    public static void EndRun() => ResetRun();

    private static void ResetRun()
    {
        level = 1;
        experience = 0;
        lastProcessedTick = 0f;
        healRemaining = 0f;
        healTicksRemaining = 0f;
        healFraction = 0f;
        activePotionId = null;
        visitedFloors.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        recentExperienceEvents.Clear();
#endif
    }

    public static void AddExperience(int amount, string source)
    {
        if (!ExpeditionSession.IsActive || amount <= 0 || level >= MaxLevel) return;
        experience = Mathf.Min(experience + amount, M10ProgressionDatabase.Level(MaxLevel).CumulativeXP);
        int before = level;
        while (level < MaxLevel && experience >= M10ProgressionDatabase.Level(level + 1).CumulativeXP) level++;
        string experienceLog = $"+{amount} XP · {source} · Lv.{level} · {experience}/{NextLevelXP}";
        Debug.Log($"[M10XP] {experienceLog}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        recentExperienceEvents.Insert(0, experienceLog);
        if (recentExperienceEvents.Count > 3) recentExperienceEvents.RemoveAt(recentExperienceEvents.Count - 1);
#endif
        if (level > before)
        {
            PlayerTurnActor.Instance?.ApplyM10LevelUp(level - before);
            Debug.Log($"[M10Level] Lv{before} → Lv{level} ({source}), XP {experience}/{NextLevelXP}");
        }
    }

    public static void RecordFloorArrival(int floor, float elapsedTicks)
    {
        if (!ExpeditionSession.IsActive || floor <= 1 || !visitedFloors.Add(floor)) return;
        int baseXP = floor <= 5 ? M10DungeonConfig.FloorArrivalXPShallow : M10DungeonConfig.FloorArrivalXPDeep;
        bool fast = elapsedTicks <= floor * M10DungeonConfig.FastArrivalTicksPerFloor;
        int award = fast ? Mathf.RoundToInt(baseXP * (1f + M10DungeonConfig.FastArrivalBonusPercent / 100f)) : baseXP;
        AddExperience(award, fast ? $"{floor}층 첫 도착·빠른 도착" : $"{floor}층 첫 도착");
    }

    public static void RecordEnemyDefeated(M10EnemyDefinition definition, float experienceMultiplier = 1f)
    {
        if (definition != null)
        {
            int award = Mathf.Max(1, Mathf.RoundToInt(definition.XP * Mathf.Max(0f, experienceMultiplier)));
            AddExperience(award, $"{definition.Id} 처치");
            CombatFeedback.AddLine($"{definition.Id} 처치 +{award} XP");
        }
    }

    public static bool TryUsePotion(string itemId, PlayerTurnActor player, out string failure)
    {
        failure = null;
        M10PotionDefinition potion = M10ProgressionDatabase.Potion(itemId);
        if (potion == null) { failure = "사용할 수 없는 소비 아이템입니다."; return false; }
        if (player == null || !player.IsReadyForInput) { failure = "지금은 포션을 사용할 수 없습니다."; return false; }
        if (!player.TryPerformTimedAction(potion.UseTicks, "UsePotion", () =>
        {
            if (!LootRuntimeState.TryConsumeRunBagItemByItemId(itemId)) return false;
            int totalHeal = Mathf.RoundToInt(player.CurrentMaxHP * potion.HealPercent / 100f);
            int instantHeal = Mathf.RoundToInt(totalHeal * potion.InstantPercent / 100f);
            healRemaining = Mathf.Max(0, totalHeal - instantHeal);
            healTicksRemaining = healRemaining > 0f ? potion.DurationTicks : 0f;
            healFraction = 0f;
            activePotionId = healRemaining > 0f ? itemId : null;
            string potionName = LootItemDatabase.FindById(itemId)?.Name ?? itemId;
            player.HealFromM10Potion(instantHeal, $"{potionName}: 즉시");
            Debug.Log($"[M10Potion] {itemId} 즉시 회복 {instantHeal}, 지속 회복 예약 {healRemaining:0.#}/{potion.DurationTicks:0.#} Tick");
            return true;
        }))
        {
            failure = "포션 사용에 실패했습니다.";
            return false;
        }
        Debug.Log($"[M10Potion] {itemId} 사용 — {potion.UseTicks:0} Tick, 총 {potion.HealPercent}% 중 {potion.InstantPercent}% 즉시/나머지 {potion.DurationTicks:0} Tick 지속");
        return true;
    }

    public static void AdvanceTo(float absoluteTick)
    {
        float delta = Mathf.Max(0f, absoluteTick - lastProcessedTick);
        lastProcessedTick = Mathf.Max(lastProcessedTick, absoluteTick);
        if (!IsHealing || delta <= 0f) return;
        float appliedTicks = Mathf.Min(delta, healTicksRemaining);
        float amount = healRemaining * (appliedTicks / healTicksRemaining);
        healRemaining -= amount;
        healTicksRemaining -= appliedTicks;
        healFraction += amount;
        int whole = Mathf.FloorToInt(healFraction);
        if (whole > 0)
        {
            healFraction -= whole;
            string potionName = LootItemDatabase.FindById(activePotionId)?.Name ?? activePotionId;
            PlayerTurnActor.Instance?.HealFromM10Potion(whole, potionName);
        }
        if (healTicksRemaining <= 0.001f || healRemaining <= 0.001f)
        {
            int tail = Mathf.RoundToInt(healFraction + healRemaining);
            if (tail > 0)
            {
                string potionName = LootItemDatabase.FindById(activePotionId)?.Name ?? activePotionId;
                PlayerTurnActor.Instance?.HealFromM10Potion(tail, potionName);
            }
            healRemaining = healTicksRemaining = healFraction = 0f;
            activePotionId = null;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetLevel(int value)
    {
        int target = Mathf.Clamp(value, 1, MaxLevel);
        int difference = target - level;
        level = target;
        experience = M10ProgressionDatabase.Level(level).CumulativeXP;
        if (difference > 0) PlayerTurnActor.Instance?.ApplyM10LevelUp(difference);
        else if (difference < 0) PlayerTurnActor.Instance?.ClampHealthToM10Max();
        M3MeasurementAutomation.MarkDebugRun();
    }
#endif
}
