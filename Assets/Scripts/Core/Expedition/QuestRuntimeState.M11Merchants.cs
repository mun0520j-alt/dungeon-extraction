using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class QuestRuntimeState
{
    private static readonly Dictionary<string, MerchantReputationState> merchantReputations =
        new Dictionary<string, MerchantReputationState>(StringComparer.Ordinal);

    [Serializable]
    public sealed class MerchantReputationState
    {
        public string MerchantId;
        public int Reputation;
        public int Level = 1;
    }

    public static int GetReputation(string merchantId) => State(merchantId).Reputation;
    public static int GetReputationLevel(string merchantId) => State(merchantId).Level;
    public static bool CanLevelUpMerchant(string merchantId)
    {
        MerchantReputationState state = State(merchantId);
        int required = state.Level == 1 ? M8EconomyConfig.Level2Reputation : M8EconomyConfig.Level3Reputation;
        return state.Level < 3 && state.Reputation >= required;
    }

    public static int NextLevelGoldCostFor(string merchantId)
    {
        int level = GetReputationLevel(merchantId);
        return level == 1 ? M8EconomyConfig.Level2Gold : level == 2 ? M8EconomyConfig.Level3Gold : 0;
    }

    public static bool TryLevelUpMerchant(string merchantId)
    {
        if (!CanLevelUpMerchant(merchantId) || !LootRuntimeState.TrySpendGold(NextLevelGoldCostFor(merchantId))) return false;
        State(merchantId).Level++;
        SyncArmorCompatibility();
        Changed?.Invoke();
        return true;
    }

    private static void AddMerchantReputation(string merchantId, int amount)
    {
        MerchantReputationState state = State(merchantId);
        state.Reputation += Mathf.Max(0, amount);
        SyncArmorCompatibility();
    }

    private static MerchantReputationState State(string merchantId)
    {
        if (string.IsNullOrEmpty(merchantId)) merchantId = M11MerchantDatabase.Armor;
        if (!merchantReputations.TryGetValue(merchantId, out MerchantReputationState state))
        {
            state = new MerchantReputationState { MerchantId=merchantId, Level=1 };
            merchantReputations[merchantId] = state;
        }
        return state;
    }

    private static void ResetMerchantReputation()
    {
        merchantReputations.Clear();
        foreach (M11MerchantDefinition merchant in M11MerchantDatabase.All) State(merchant.Id);
        SyncArmorCompatibility();
    }

    private static void SyncArmorCompatibility()
    {
        MerchantReputationState armor = State(M11MerchantDatabase.Armor);
        Reputation = armor.Reputation;
        ReputationLevel = armor.Level;
    }

    private static List<MerchantReputationState> CaptureMerchantReputations() => merchantReputations.Values
        .Select(x => new MerchantReputationState { MerchantId=x.MerchantId, Reputation=x.Reputation, Level=x.Level }).ToList();

    private static void RestoreMerchantReputations(List<MerchantReputationState> saved, int legacyReputation, int legacyLevel)
    {
        merchantReputations.Clear();
        if (saved != null)
        {
            foreach (MerchantReputationState entry in saved)
            {
                if (entry == null || M11MerchantDatabase.Find(entry.MerchantId) == null) continue;
                merchantReputations[entry.MerchantId] = new MerchantReputationState
                {
                    MerchantId=entry.MerchantId, Reputation=Mathf.Max(0, entry.Reputation), Level=Mathf.Clamp(entry.Level, 1, 3)
                };
            }
        }
        foreach (M11MerchantDefinition merchant in M11MerchantDatabase.All) State(merchant.Id);
        if (saved == null || saved.Count == 0)
        {
            MerchantReputationState armor = State(M11MerchantDatabase.Armor);
            armor.Reputation = Mathf.Max(0, legacyReputation);
            armor.Level = Mathf.Clamp(legacyLevel, 1, 3);
        }
        SyncArmorCompatibility();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static void DebugSetMerchantReputation(string merchantId, int reputation, int level)
    {
        MerchantReputationState state = State(merchantId);
        state.Reputation = Mathf.Max(0, reputation);
        state.Level = Mathf.Clamp(level, 1, 3);
        SyncArmorCompatibility();
        Changed?.Invoke();
        M3MeasurementAutomation.MarkDebugRun();
    }
#endif
}
