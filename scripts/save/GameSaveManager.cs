using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public partial class GameSaveManager : Node
{
    public const int SlotCount = 4;
    private const string SaveDirectory = "user://saves";
    public static GameSaveManager Instance { get; private set; } = null!;
    public static List<string> SelectedDeckIds { get; } = [];
    public BattleSave? PendingSave { get; set; }
    public int PendingLoadSlot { get; set; }
    public bool SavingBecauseFull { get; set; }
    public int PendingLoadAfterSave { get; set; }
    public bool ResumePendingSave { get; set; }

    public override void _Ready()
    {
        Instance = this;
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(SaveDirectory));
    }

    public bool HasSave(int slot) => FileAccess.FileExists(SlotPath(slot));
    public int FirstEmptySlot() => Enumerable.Range(1, SlotCount).FirstOrDefault(slot => !HasSave(slot));

    public void Write(int slot, BattleSave save)
    {
        ValidateSlot(slot);
        save.SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        using var file = FileAccess.Open(SlotPath(slot), FileAccess.ModeFlags.Write);
        if (file == null) throw new System.IO.IOException($"无法写入存档槽 {slot}");
        file.StoreString(JsonSerializer.Serialize(save, JsonOptions));
    }

    public BattleSave? Read(int slot)
    {
        ValidateSlot(slot);
        using var file = FileAccess.Open(SlotPath(slot), FileAccess.ModeFlags.Read);
        return file == null ? null : JsonSerializer.Deserialize<BattleSave>(file.GetAsText(), JsonOptions);
    }

    public void Delete(int slot)
    {
        ValidateSlot(slot);
        var path = SlotPath(slot);
        if (FileAccess.FileExists(path)) DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }

    public string Describe(int slot)
    {
        var save = Read(slot);
        return save == null ? "空存档" : $"第 {save.Turn} 回合 · AP {save.ActionPoints}\n{save.SavedAt}";
    }

    public BattleSave? ConsumePendingLoad()
    {
        if (ResumePendingSave && PendingSave != null) { ResumePendingSave = false; var pending = PendingSave; PendingSave = null; return pending; }
        if (PendingLoadSlot <= 0) return null;
        var slot = PendingLoadSlot;
        PendingLoadSlot = 0;
        return Read(slot);
    }

    private static string SlotPath(int slot) => $"{SaveDirectory}/slot_{slot}.json";
    private static void ValidateSlot(int slot)
    {
        if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}

public sealed class BattleSave
{
    public string SavedAt { get; set; } = "";
    public int Turn { get; set; } = 1;
    public int ActionPoints { get; set; } = BattleState.DefaultActionPoints;
    public bool PlayerDeployedThisTurn { get; set; }
    public bool EnemyDeployedThisTurn { get; set; }
    public int EnemyActionPoints { get; set; } = BattleState.DefaultActionPoints;
    public int PlayerNextTurnBonus { get; set; }
    public int EnemyNextTurnBonus { get; set; }
    public int? PlayerNextTurnActionPointsOverride { get; set; }
    public int? EnemyNextTurnActionPointsOverride { get; set; }
    public bool PlayerZeroNextTurnActionPoints { get; set; }
    public bool EnemyZeroNextTurnActionPoints { get; set; }
    public string LeaderId { get; set; } = "";
    public int LeaderTurns { get; set; }
    public string FreeCardId { get; set; } = "";
    public bool CancelNextEnemyEffect { get; set; }
    public List<string> SelectedDeckIds { get; set; } = [];
    public List<string> PlayerHeroBagIds { get; set; } = [];
    public List<string> EnemyHeroBagIds { get; set; } = [];
    public List<UnitSave?> PlayerSlots { get; set; } = [];
    public List<UnitSave?> EnemySlots { get; set; } = [];
    public DeckSave PlayerDeck { get; set; } = new();
    public DeckSave EnemyDeck { get; set; } = new();
    public List<PassiveSave> Passives { get; set; } = [];
}

public sealed class DeckSave
{
    public List<CardSave> Draw { get; set; } = [];
    public List<CardSave> Hand { get; set; } = [];
    public List<CardSave> Discard { get; set; } = [];
    public List<CardSave> Exile { get; set; } = [];
}

public sealed class CardSave
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "player";
    public string OriginalOwnerId { get; set; } = "player";
    public int CostModifier { get; set; }
    public int CostOverride { get; set; } = -1;
    public int Cooldown { get; set; }
    public bool Temporary { get; set; }
    public bool ExileAtTurnEnd { get; set; }
    public bool ReturnToOriginalOwnerDiscardAtTurnEnd { get; set; }
    public bool EmergencyUsed { get; set; }
}

public sealed class UnitSave
{
    public string DefinitionId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Attack { get; set; }
    public int Exp { get; set; }
    public int Star { get; set; }
    public bool HasAttacked { get; set; }
    public int SkillTurns { get; set; }
    public int TauntTurns { get; set; }
    public int DebuffTurns { get; set; }
    public float ShieldRatio { get; set; }
    public int ShieldTurns { get; set; }
    public int FreeSelfCards { get; set; }
    public int AttackRestore { get; set; }
    public int LinkTurns { get; set; }
    public int GrudgeStacks { get; set; }
    public int GrudgeAttackPenaltyPerStack { get; set; }
    public int CeasefireTurns { get; set; }
    public float DamageTakenMultiplier { get; set; } = 1f;
    public int LinkedEnemy { get; set; } = -1;
    public bool DeathHandled { get; set; }
    public int ExtraAttacksRemaining { get; set; }
    public int ShieldPoints { get; set; }
}

public sealed class PassiveSave
{
    public string OwnerId { get; set; } = "player";
    public int SlotIndex { get; set; }
    public CardSave Card { get; set; } = new();
}
