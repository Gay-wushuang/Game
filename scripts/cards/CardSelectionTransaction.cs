using System.Collections.Generic;

/// <summary>可序列化的多阶段卡牌选择；只保存稳定ID/槽位，不保存场景节点。</summary>
public sealed class CardSelectionTransaction
{
    public string CardInstanceId { get; set; } = "";
    public string OwnerId { get; set; } = "player";
    public string Phase { get; set; } = "SOURCE";
    public int SourceSlot { get; set; } = -1;
    public List<int> AllySlots { get; set; } = [];
    public List<int> EnemySlots { get; set; } = [];
    public List<string> SelectedCardIds { get; set; } = [];
    public bool IsComplete { get; set; }
}
