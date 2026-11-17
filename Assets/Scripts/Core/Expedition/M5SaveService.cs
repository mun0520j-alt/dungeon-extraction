using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

// LootRuntimeState가 상태를 소유하고, 이 클래스는 저장 파일 직렬화와 교체만 담당한다.
public static class M5SaveService
{
    private const int CurrentSaveVersion = 6;
    private const string SaveFolderName = "Save";
    private const string SaveFileName = "save.json";
    private const string BackupFileName = "save.bak";
    private const string LegacyV2BackupFileName = "save_v2.bak";
    private const string LegacyV3BackupFileName = "save_v3.bak";
    private const string LegacyV4BackupFileName = "save_v4.bak";
    private const string LegacyV5BackupFileName = "save_v5.bak";
    private const string TempFileName = "save.tmp";

    private static bool isRestoring;
    private static string recoveryNotice;
    private static SaveSettings currentSettings = SaveSettings.Default();

    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, SaveFolderName);
    public static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
    public static string BackupPath => Path.Combine(SaveDirectory, BackupFileName);
    public static string LegacyV2BackupPath => Path.Combine(SaveDirectory, LegacyV2BackupFileName);
    public static string LegacyV3BackupPath => Path.Combine(SaveDirectory, LegacyV3BackupFileName);
    public static string LegacyV4BackupPath => Path.Combine(SaveDirectory, LegacyV4BackupFileName);
    public static string LegacyV5BackupPath => Path.Combine(SaveDirectory, LegacyV5BackupFileName);
    public static bool Fullscreen => currentSettings.fullscreen;
    public static int ResolutionWidth => currentSettings.resolution_width;
    public static int ResolutionHeight => currentSettings.resolution_height;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        LootRuntimeState.Changed -= HandleLootChanged;
        QuestRuntimeState.Changed -= HandleQuestChanged;
        Load();
        LootRuntimeState.Changed += HandleLootChanged;
        QuestRuntimeState.Changed += HandleQuestChanged;
    }

    public static void PrepareForExpedition()
    {
        var lossState = Capture(expeditionInProgress: true, includeExpeditionItems: false);
        Write(lossState);
    }

    public static void CompleteExpedition()
    {
        Write(Capture(expeditionInProgress: false, includeExpeditionItems: true));
    }

    public static void SaveNow()
    {
        Write(Capture(expeditionInProgress: ExpeditionSession.IsActive, includeExpeditionItems: !ExpeditionSession.IsActive));
    }

    public static void ApplySavedDisplaySettings()
    {
        if (currentSettings.resolution_width <= 0 || currentSettings.resolution_height <= 0)
            currentSettings = SaveSettings.Default();
        var available = Screen.resolutions;
        if (available.Length > 0 && !available.Any(value =>
                value.width == currentSettings.resolution_width && value.height == currentSettings.resolution_height))
        {
            Debug.LogWarning($"[Settings] 저장된 해상도 {currentSettings.resolution_width}x{currentSettings.resolution_height}를 사용할 수 없어 현재 화면으로 복귀합니다.");
            currentSettings.resolution_width = Mathf.Max(640, Screen.width);
            currentSettings.resolution_height = Mathf.Max(360, Screen.height);
        }
        Screen.SetResolution(currentSettings.resolution_width, currentSettings.resolution_height, currentSettings.fullscreen);
    }

    public static void UpdateDisplaySettings(int width, int height, bool fullscreen)
    {
        currentSettings.fullscreen = fullscreen;
        currentSettings.resolution_width = Mathf.Max(640, width);
        currentSettings.resolution_height = Mathf.Max(360, height);
        Screen.SetResolution(currentSettings.resolution_width, currentSettings.resolution_height, fullscreen);

        SaveData data = TryRead(SavePath) ?? TryRead(BackupPath)
            ?? Capture(expeditionInProgress: ExpeditionSession.IsActive, includeExpeditionItems: !ExpeditionSession.IsActive);
        data.settings = currentSettings.Clone();
        Write(data);
    }

    public static string ConsumeRecoveryNotice()
    {
        string notice = recoveryNotice;
        recoveryNotice = null;
        return notice;
    }

    private static void HandleLootChanged()
    {
        if (isRestoring || ExpeditionSession.IsActive) return;
        Write(Capture(expeditionInProgress: false, includeExpeditionItems: true));
    }

    private static void HandleQuestChanged()
    {
        if (isRestoring || ExpeditionSession.IsActive) return;
        Write(Capture(expeditionInProgress: false, includeExpeditionItems: true));
    }

    private static void Load()
    {
        SaveData data = TryRead(SavePath);
        string loadedPath = SavePath;
        bool recoveredFromBackup = false;
        if (data == null)
        {
            data = TryRead(BackupPath);
            loadedPath = BackupPath;
            recoveredFromBackup = data != null;
        }

        if (data == null)
        {
            if (File.Exists(SavePath) || File.Exists(BackupPath))
                Debug.LogWarning("[Save] 주 세이브와 백업을 읽지 못해 새 게임으로 시작합니다.");
            return;
        }

        if (recoveredFromBackup) Debug.LogWarning("[Save] save.json이 손상되어 save.bak으로 복구했습니다.");
        bool migrated = data.save_version < CurrentSaveVersion;
        if (data.save_version == 2 && !TryPreserveLegacyV2Save(loadedPath))
        {
            Debug.LogError("[Save] v2 원본 백업에 실패해 v3 마이그레이션을 중단했습니다. 기존 파일은 변경하지 않습니다.");
            return;
        }
        if (data.save_version == 3 && !TryPreserveLegacyV3Save(loadedPath))
        {
            Debug.LogError("[Save] v3 원본 백업에 실패해 v4 마이그레이션을 중단했습니다. 기존 파일은 변경하지 않습니다.");
            return;
        }
        if (data.save_version == 4 && !TryPreserveLegacyV4Save(loadedPath))
        {
            Debug.LogError("[Save] v4 원본 백업에 실패해 v5 마이그레이션을 중단했습니다. 기존 파일은 변경하지 않습니다.");
            return;
        }
        if (data.save_version == 5 && !TryPreserveLegacyV5Save(loadedPath))
        {
            Debug.LogError("[Save] v5 원본 백업에 실패해 v6 마이그레이션을 중단했습니다. 기존 파일은 변경하지 않습니다.");
            return;
        }
        Restore(data);
        if (!data.expedition_in_progress)
        {
            if (migrated)
            {
                Write(Capture(expeditionInProgress: false, includeExpeditionItems: true));
                Debug.Log($"[Save] v{data.save_version} 세이브를 v{CurrentSaveVersion}으로 마이그레이션했습니다.");
            }
            return;
        }

        recoveryNotice = "이전 원정이 종료되어 MIA 처리되었습니다.";
        QuestRuntimeState.RecordInterruptedMIA();
        LootRuntimeState.ClearConfirmedInsuranceWithoutReturn();
        // 복원된 런타임 상태를 다시 캡처해 v6 재귀 구조와 임시 보관함까지 일관되게 기록한다.
        Write(Capture(expeditionInProgress: false, includeExpeditionItems: true));
        Debug.LogWarning("[Save] 종료되지 않은 원정을 MIA로 처리했습니다.");
    }

    private static bool TryPreserveLegacyV2Save(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            if (File.Exists(LegacyV2BackupPath)) return true;
            File.Copy(sourcePath, LegacyV2BackupPath, false);
            Debug.Log($"[Save] v2 원본을 별도 백업으로 보존했습니다: {LegacyV2BackupPath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] v2 원본 백업 실패: {LegacyV2BackupPath}\n{exception.Message}");
            return false;
        }
    }

    private static bool TryPreserveLegacyV3Save(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            if (File.Exists(LegacyV3BackupPath)) return true;
            File.Copy(sourcePath, LegacyV3BackupPath, false);
            Debug.Log($"[Save] v3 원본을 별도 백업으로 보존했습니다: {LegacyV3BackupPath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] v3 원본 백업 실패: {LegacyV3BackupPath}\n{exception.Message}");
            return false;
        }
    }

    private static bool TryPreserveLegacyV4Save(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            if (File.Exists(LegacyV4BackupPath)) return true;
            File.Copy(sourcePath, LegacyV4BackupPath, false);
            Debug.Log($"[Save] v4 원본을 별도 백업으로 보존했습니다: {LegacyV4BackupPath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] v4 원본 백업 실패: {LegacyV4BackupPath}\n{exception.Message}");
            return false;
        }
    }

    private static bool TryPreserveLegacyV5Save(string sourcePath)
    {
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            if (File.Exists(LegacyV5BackupPath)) return true;
            File.Copy(sourcePath, LegacyV5BackupPath, false);
            Debug.Log($"[Save] v5 원본을 별도 백업으로 보존했습니다: {LegacyV5BackupPath}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] v5 원본 백업 실패: {LegacyV5BackupPath}\n{exception.Message}");
            return false;
        }
    }

    private static SaveData TryRead(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.save_version <= 0 || data.save_version > CurrentSaveVersion)
            {
                Debug.LogWarning($"[Save] 지원하지 않는 세이브 버전입니다: {data?.save_version ?? 0} ({path})");
                return null;
            }
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Save] 세이브 읽기 실패: {path}\n{exception.Message}");
            return null;
        }
    }

    private static void Restore(SaveData data)
    {
        isRestoring = true;
        try
        {
            // Stash 높이는 완료 퀘스트(q03/q06)에서 계산하므로 아이템 배치보다 먼저 복원한다.
            QuestRuntimeState.RestorePersistentState(data.quest_state);
            var context = new RestoreContext(data.save_version);
            List<LootGridRestoreItem> restoredStash = RestoreGridItems(data.stash, context, bagDepth: 0,
                placementSupported: data.save_version >= 6);
            List<LootItemInstance> restoredEquipment = RestoreItems(data.equipped, context, bagDepth: 0);
            LootRuntimeState.RestorePersistentState(
                RestoreRunBag(data.run_bag, context),
                Array.Empty<LootItemInstance>(),
                restoredEquipment,
                RestoreBasicSlots(data.unequipped_basic_slots),
                data.has_basic_loadout,
                data.gold);
            var restoredTemporary = RestoreItems(data.temporary_storage, context, bagDepth: 0);
            restoredTemporary.AddRange(context.Overflow);
            LootRuntimeState.RestoreM9Storage(restoredStash, restoredTemporary);
            LootRuntimeState.RestoreInsurance(data.insurance_returns);
            LootRuntimeState.RestoreConfirmedInsurance(data.insured_this_run);
            currentSettings = data.settings != null ? data.settings.Normalized() : SaveSettings.Default();
        }
        finally
        {
            isRestoring = false;
        }
    }

    private static List<LootGridRestoreItem> RestoreRunBag(List<SavedItem> savedItems, RestoreContext context)
    {
        return RestoreGridItems(savedItems, context, bagDepth: 0, placementSupported: context.SaveVersion >= 3);
    }

    private static List<LootGridRestoreItem> RestoreGridItems(List<SavedItem> savedItems, RestoreContext context,
        int bagDepth, bool placementSupported)
    {
        var result = new List<LootGridRestoreItem>();
        if (savedItems == null) return result;
        foreach (SavedItem saved in savedItems)
        {
            LootItemInstance item = RestoreItem(saved, context, bagDepth);
            if (item != null) result.Add(new LootGridRestoreItem(item, saved.x, saved.y, saved.rotated, placementSupported));
        }
        return result;
    }

    private static List<LootItemInstance> RestoreItems(List<SavedItem> savedItems, RestoreContext context, int bagDepth)
    {
        var result = new List<LootItemInstance>();
        if (savedItems == null) return result;
        foreach (SavedItem saved in savedItems)
        {
            LootItemInstance item = RestoreItem(saved, context, bagDepth);
            if (item != null) result.Add(item);
        }
        return result;
    }

    private static LootItemInstance RestoreItem(SavedItem saved, RestoreContext context, int bagDepth)
    {
        if (saved == null) return null;
        LootItemData definition = ResolveDefinition(saved.item_id);
        if (definition == null)
        {
            Debug.LogWarning($"[Save] CSV에 없는 item_id를 건너뜁니다: {saved.item_id}");
            return null;
        }
        string instanceId = string.IsNullOrWhiteSpace(saved.instance_id) ? Guid.NewGuid().ToString("N") : saved.instance_id;
        if (!context.InstanceIds.Add(instanceId))
        {
            Debug.LogWarning($"[Save] 중복 instance_id를 건너뜁니다: {instanceId}");
            return null;
        }
        var item = new LootItemInstance(definition, instanceId, context.SaveVersion >= 5 && saved.found_in_raid);
        if (definition.Type != LootItemType.Bag || saved.children == null) return item;

        int currentDepth = bagDepth + 1;
        foreach (SavedItem childSaved in saved.children)
        {
            LootItemInstance child = RestoreItem(childSaved, context, currentDepth);
            if (child == null) continue;
            if (child.Definition?.Type == LootItemType.Bag && currentDepth >= LootRuntimeState.MaxBagNestingDepth)
            {
                context.Overflow.Add(child);
                Debug.LogWarning($"[Save] 가방 중첩 4단계 초과 아이템을 임시 보관함으로 이동했습니다: {child.Definition.Name}");
                continue;
            }
            bool placed = item.BagGrid.TryPlace(child, childSaved.x, childSaved.y, childSaved.rotated)
                || item.BagGrid.TryAutoPlace(child, out _);
            if (!placed)
            {
                context.Overflow.Add(child);
                Debug.LogWarning($"[Save] 가방 내부 배치 실패 아이템을 임시 보관함으로 이동했습니다: {child.Definition?.Name}");
            }
        }
        return item;
    }

    private static IEnumerable<LootItemType> RestoreBasicSlots(List<string> slots)
    {
        if (slots == null) yield break;
        foreach (string slot in slots)
        {
            if (Enum.TryParse(slot, out LootItemType type)) yield return type;
        }
    }

    private static LootItemData ResolveDefinition(string itemId)
    {
        LootItemData csvItem = LootItemDatabase.FindById(itemId);
        if (csvItem != null) return csvItem;
        switch (itemId)
        {
            case "basic_weapon": return Basic("basic_weapon", "기본 검", LootItemType.Weapon, 20, 0);
            case "basic_shield": return Basic("basic_shield", "낡은 방패", LootItemType.Shield, 0, 6);
            case "basic_armor": return Basic("basic_armor", "가죽 갑옷", LootItemType.Armor, 0, 6);
            default: return null;
        }
    }

    private static LootItemData Basic(string id, string name, LootItemType type, int attack, int defense)
    {
        return new LootItemData
        {
            Id = id, Name = name, Type = type, Rarity = LootRarity.Normal,
            AttackBonus = attack, DefenseBonus = defense, CapacityBonus = 0,
            IconKey = string.Empty, DropWeight = 0,
            GridWidth = type == LootItemType.Weapon ? 1 : 2,
            GridHeight = type == LootItemType.Shield ? 2 : 3
        };
    }

    private static SaveData Capture(bool expeditionInProgress, bool includeExpeditionItems)
    {
        bool equippedBagOwnsRunBag = LootRuntimeState.GetEquipped(LootItemType.Bag) != null;
        return new SaveData
        {
            save_version = CurrentSaveVersion,
            saved_at = DateTime.UtcNow.ToString("O"),
            stash = SaveRestoreItems(LootRuntimeState.PersistentStashRoots()),
            temporary_storage = SaveItems(LootRuntimeState.PersistentTemporaryItems()),
            run_bag = includeExpeditionItems && !equippedBagOwnsRunBag ? SaveRunBagItems() : new List<SavedItem>(),
            equipped = includeExpeditionItems ? SaveItems(LootRuntimeState.EquippedLoot.Values) : new List<SavedItem>(),
            has_basic_loadout = includeExpeditionItems && LootRuntimeState.HasBasicLoadout,
            unequipped_basic_slots = includeExpeditionItems
                ? Enum.GetValues(typeof(LootItemType)).Cast<LootItemType>()
                    .Where(LootRuntimeState.IsBasicSlotUnequipped).Select(type => type.ToString()).ToList()
                : new List<string>(),
            settings = currentSettings.Clone(),
            expedition_in_progress = expeditionInProgress
            ,gold = LootRuntimeState.Gold
            ,insurance_returns = LootRuntimeState.InsuranceReturns.Select(CloneInsurance).ToList()
            ,insured_this_run = LootRuntimeState.InsuredThisRunIds.Select(instanceId => new LootRuntimeState.InsurancePolicy
            {
                InstanceId = instanceId,
                ItemId = LootRuntimeState.GetInsuredItemId(instanceId)
            }).ToList()
            ,quest_state = QuestRuntimeState.CapturePersistentState()
        };
    }

    private static LootRuntimeState.InsuranceReturn CloneInsurance(LootRuntimeState.InsuranceReturn entry)
    {
        return new LootRuntimeState.InsuranceReturn
        {
            ReturnId = entry.ReturnId, ItemId = entry.ItemId, InstanceId = entry.InstanceId,
            Returned = entry.Returned, Revealed = entry.Revealed,
            WaitTicksRemaining = entry.WaitTicksRemaining, ClaimTicksRemaining = entry.ClaimTicksRemaining
        };
    }

    private static List<SavedItem> SaveRunBagItems()
    {
        var result = new List<SavedItem>();
        foreach (LootItemInstance item in LootRuntimeState.RunBag)
        {
            if (item?.Definition == null) continue;
            LootGridPlacement placement = LootRuntimeState.RunBagGrid.Find(item.InstanceId);
            if (placement == null) continue;
            result.Add(SaveItem(item, placement));
        }
        return result;
    }

    private static List<SavedItem> SaveItems(IEnumerable<LootItemInstance> items)
    {
        return items.Where(item => item?.Definition != null).Select(item => SaveItem(item, null)).ToList();
    }

    private static List<SavedItem> SaveRestoreItems(IEnumerable<LootGridRestoreItem> items)
    {
        var result = new List<SavedItem>();
        foreach (LootGridRestoreItem restored in items)
        {
            if (restored?.Item?.Definition == null) continue;
            result.Add(SaveItem(restored.Item, new LootGridPlacement(restored.Item, restored.X, restored.Y, restored.Rotated)));
        }
        return result;
    }

    private static SavedItem SaveItem(LootItemInstance item, LootGridPlacement placement)
    {
        var saved = new SavedItem
        {
            item_id = item.Definition.Id,
            instance_id = item.InstanceId,
            found_in_raid = item.IsFoundInRaid,
            x = placement?.X ?? 0,
            y = placement?.Y ?? 0,
            rotated = placement?.Rotated ?? false
        };
        if (item.Definition.Type == LootItemType.Bag && item.BagGrid != null)
        {
            foreach (LootGridPlacement child in item.BagGrid.Placements)
                if (child?.Item?.Definition != null) saved.children.Add(SaveItem(child.Item, child));
        }
        return saved;
    }

    private static bool Write(SaveData data)
    {
        string tempPath = Path.Combine(SaveDirectory, TempFileName);
        try
        {
            Directory.CreateDirectory(SaveDirectory);
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            if (File.Exists(SavePath))
            {
                try
                {
                    File.Replace(tempPath, SavePath, BackupPath);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(SavePath, BackupPath, true);
                    File.Delete(SavePath);
                    File.Move(tempPath, SavePath);
                }
            }
            else
            {
                File.Move(tempPath, SavePath);
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] 저장 실패. 기존 세이브는 유지됩니다.\n{exception.Message}");
            return false;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (Exception) { }
        }
    }

    [Serializable]
    private sealed class SaveData
    {
        public int save_version = CurrentSaveVersion;
        public string saved_at;
        public List<SavedItem> stash = new List<SavedItem>();
        public List<SavedItem> temporary_storage = new List<SavedItem>();
        public List<SavedItem> run_bag = new List<SavedItem>();
        public List<SavedItem> equipped = new List<SavedItem>();
        public bool has_basic_loadout;
        public List<string> unequipped_basic_slots = new List<string>();
        public SaveSettings settings = new SaveSettings();
        public bool expedition_in_progress;
        public int gold;
        public List<LootRuntimeState.InsuranceReturn> insurance_returns = new List<LootRuntimeState.InsuranceReturn>();
        public List<LootRuntimeState.InsurancePolicy> insured_this_run = new List<LootRuntimeState.InsurancePolicy>();
        public QuestRuntimeState.PersistentState quest_state = new QuestRuntimeState.PersistentState();
    }

    [Serializable]
    private sealed class SavedItem
    {
        public string item_id;
        public string instance_id;
        public int x;
        public int y;
        public bool rotated;
        public bool found_in_raid;
        public List<SavedItem> children = new List<SavedItem>();
    }

    private sealed class RestoreContext
    {
        public int SaveVersion { get; }
        public HashSet<string> InstanceIds { get; } = new HashSet<string>(StringComparer.Ordinal);
        public List<LootItemInstance> Overflow { get; } = new List<LootItemInstance>();

        public RestoreContext(int saveVersion) => SaveVersion = saveVersion;
    }

    [Serializable]
    private sealed class SaveSettings
    {
        public bool fullscreen;
        public int resolution_width;
        public int resolution_height;

        public static SaveSettings Default()
        {
            return new SaveSettings
            {
                fullscreen = Screen.fullScreen,
                resolution_width = Mathf.Max(640, Screen.width),
                resolution_height = Mathf.Max(360, Screen.height)
            };
        }

        public SaveSettings Normalized()
        {
            if (resolution_width <= 0 || resolution_height <= 0) return Default();
            return Clone();
        }

        public SaveSettings Clone()
        {
            return new SaveSettings
            {
                fullscreen = fullscreen,
                resolution_width = resolution_width,
                resolution_height = resolution_height
            };
        }
    }
}
