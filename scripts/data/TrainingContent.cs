using Godot;

[GlobalClass]
public partial class TrainingContent : Resource
{
    [ExportCategory("训练场内容")]
    [Export] public Godot.Collections.Array<HeroDefinition> heroes { get; set; } = [];
    [Export] public Godot.Collections.Array<CardDefinition> cards { get; set; } = [];
    [Export] public Godot.Collections.Array<MonsterDefinition> monsters { get; set; } = [];
}
