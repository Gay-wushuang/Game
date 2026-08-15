using Godot;

[GlobalClass]
public partial class ContentDefinition : Resource
{
    [ExportCategory("基础信息")]
    [Export] public StringName id { get; set; } = new();
    [Export] public string display_name { get; set; } = "未命名内容";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";
    [Export] public Texture2D? artwork { get; set; }
    [Export] public string[] tags { get; set; } = [];
    [ExportCategory("设计人员自定义信息")]
    [Export(PropertyHint.MultilineText)] public string designer_notes { get; set; } = "";
    [Export] public Godot.Collections.Dictionary custom_fields { get; set; } = new();

    public Variant CustomValue(StringName key, Variant fallback = default) =>
        custom_fields.TryGetValue(key, out Variant value) ? value : fallback;
}
