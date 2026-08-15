using Godot;

[GlobalClass]
public partial class MonsterDefinition : ContentDefinition
{
    [ExportCategory("怪物战斗数据")]
    [Export] public HeroDefinition.HeroType unit_type { get; set; } = HeroDefinition.HeroType.Scout;
    [Export(PropertyHint.Range, "1,99999,1")] public int max_hp { get; set; } = 10;
    [Export(PropertyHint.Range, "0,9999,1")] public int attack { get; set; } = 1;
    [Export(PropertyHint.Range, "0,5,0.05")] public float retaliation_ratio { get; set; } = 0.5f;
    [Export] public int exp_reward { get; set; }
    [Export] public StringName behavior_id { get; set; } = "stationary_dummy";
    [Export] public string[] ability_ids { get; set; } = [];
    public string TypeName() => new[] { "先锋", "刺客", "斥候", "祭司" }[(int)unit_type];
}
