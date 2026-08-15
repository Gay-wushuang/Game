using Godot;

[GlobalClass]
public partial class HeroDefinition : ContentDefinition
{
    public enum HeroType { Vanguard, Assassin, Scout, Lawknoer }
    [ExportCategory("英雄战斗数据")]
    [Export(PropertyHint.Range, "1,999,1")] public int character_number { get; set; } = 1;
    [Export] public HeroType hero_type { get; set; }
    [Export(PropertyHint.Range, "1,999,1")] public int max_hp { get; set; } = 10;
    [Export(PropertyHint.Range, "0,999,1")] public int attack { get; set; } = 1;
    [Export(PropertyHint.Range, "0,6,1")] public int initial_star { get; set; }
    [Export(PropertyHint.Range, "1,999,1")] public int exp_to_star { get; set; } = 6;
    [Export] public StringName innate_ability { get; set; } = new();
    [Export] public string[] skill_ids { get; set; } = [];
    [Export(PropertyHint.MultilineText)] public string passive_text { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string skill_1_text { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string skill_2_text { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string leader_bonus_text { get; set; } = "";
    [Export(PropertyHint.Range, "0,20,1")] public int skill_cooldown { get; set; } = 5;
    [Export] public int[] star_attack_choices { get; set; } = [0, 0, 0, 0, 0, 0];
    [Export] public int[] star_hp_choices { get; set; } = [0, 0, 0, 0, 0, 0];
    [Export(PropertyHint.MultilineText)] public string star_progression_text { get; set; } = "";
    public string TypeName() => new[] { "先锋", "刺客", "斥候", "祭司" }[(int)hero_type];
}
