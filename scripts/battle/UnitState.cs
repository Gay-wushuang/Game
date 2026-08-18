public sealed class UnitState
{
    public ContentDefinition Definition { get; init; } = null!;
    public string Name { get; set; } = "";
    public int Number { get; init; }
    public string Type { get; set; } = "";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Attack { get; set; }
    public int Exp { get; set; }
    public int ExpToStar { get; init; } = 6;
    public int Star { get; set; }
    public float RetaliationRatio { get; init; } = 0.5f;
    public int Cooldown { get; set; }
    public int SkillTurns { get; set; }
    public int TauntTurns { get; set; }
    public int FreeSelfCards { get; set; }
    public int DebuffTurns { get; set; }
    public int AttackRestore { get; set; }
    public int LinkTurns { get; set; }
    public float ShieldRatio { get; set; }
    public int ShieldTurns { get; set; }
    public int GrudgeStacks { get; set; }
    public int CeasefireTurns { get; set; }
    public float DamageTakenMultiplier { get; set; } = 1f;
    public int LinkedEnemy { get; set; } = -1;
    public bool Alive => Hp > 0;
    public string Id => Definition.id.ToString();

    // 临时效果原始值跟踪：首次临时修改时记录，恢复时使用，避免嵌套后恢复到中间状态。
    public string? OriginalType { get; set; }
    public int? OriginalAttack { get; set; }
}
