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
    public int LinkedEnemy { get; set; } = -1;
    public bool Alive => Hp > 0;
    public string Id => Definition.id.ToString();
}
