using Godot;
using System;
using System.Collections.Generic;

public partial class MainMenu : Control
{
    private const string Art = "res://assets/art/ui/s2_main_menu/";
    private readonly SystemFont _font = new() { FontNames = new[] { "Microsoft YaHei", "Arial" } };
    private readonly SystemFont _bold = new() { FontNames = new[] { "Microsoft YaHei", "Arial" }, FontWeight = 700 };
    private readonly List<(Control Control, int Size)> _fontSizes = new();
    private static readonly Color Text = new(0.91f, 0.90f, 0.91f);
    public override void _Ready()
    {
        GetViewport().SizeChanged += UpdateFontSizes;
        AudioManager.Instance?.PlayMainMenuMusic();
        MouseFilter = MouseFilterEnum.Ignore;
        var bg = new TextureRect { Texture = GD.Load<Texture2D>(Art + "background.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(bg); Rect(bg, 0, 0, 1920, 1080);
        Image("shade.svg", 0, 0, 1920, 958);
        Image("logo.png", 35, 55, 470, 167);
        var line = new ColorRect { Color = new Color(.7f,.6f,.65f,.45f), MouseFilter = MouseFilterEnum.Ignore }; AddChild(line); Rect(line, 76, 321, 78, 824);
        Menu("StartButton", "开始游戏", 315, "primary", SceneRouter.Scenes.ModeSelect);
        Menu("RecruitButton", "招募大厅", 420, "secondary", SceneRouter.Scenes.Shop);
        Menu("LabButton", "研发基地", 525, "secondary", SceneRouter.Scenes.Lab);
        Menu("PrepareButton", "备战中心", 630, "secondary", SceneRouter.Scenes.DeckSelect);
        var exit = new Button { Name = "ExitButton", Text = "◇  退出游戏", Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand }; AddChild(exit); Rect(exit, 78, 750, 245, 804); Font(exit, 23); Empty(exit); exit.Pressed += () => GetTree().Quit();
        Image("notice.svg", 1470, 73, 1860, 238); Label("公告", 1538, 101, 1630, 136, 26, Text); Label("公告内容区域 · 示例", 1506, 169, 1820, 204, 21, Text);
        var bar = new ColorRect { Color = new Color(.035f,.04f,.06f,.96f), MouseFilter = MouseFilterEnum.Ignore }; AddChild(bar); Rect(bar, 0, 958, 1920, 1080); Line(0, 958, 1920, 960, new Color(.58f,.50f,.55f)); Line(0, 958, 250, 961, new Color(.75f,.2f,.27f));
        Image("player_badge.svg", 57, 980, 133, 1056); Label("玩家名称 · 示例", 150, 987, 325, 1020, 23, Text); Label("等级 1", 150, 1025, 290, 1050, 17, Text); Label("经验 · 示例", 337, 986, 490, 1015, 18, Text);
        var progress = new ProgressBar { Value = 32, ShowPercentage = false, MouseFilter = MouseFilterEnum.Ignore }; AddChild(progress); Rect(progress, 337, 1024, 587, 1032); progress.AddThemeStyleboxOverride("background", Box(new Color(.27f,.26f,.29f), Colors.Transparent, 0)); progress.AddThemeStyleboxOverride("fill", Box(new Color(.74f,.22f,.29f), Colors.Transparent, 0)); Label("320 / 1000", 599, 1014, 690, 1041, 16, Text);
        Line(704, 986, 705, 1048, new Color(.6f,.57f,.6f,.45f)); Line(1510, 986, 1511, 1048, new Color(.6f,.57f,.6f,.45f));
        for (int i=0; i<3; i++) { int x=760+i*239; Image($"currency_{i+1}.svg", x, 997, x+24, 1035); Label($"货币 {i+1} · XXX", x+40, 1002, x+220, 1033, 21, Text); }
        var settings = new Button { Name = "SettingsButton", Text = "设置", Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand }; AddChild(settings); Rect(settings, 1747, 990, 1845, 1045); Font(settings, 20); Empty(settings); settings.Pressed += () => SceneRouter.Instance.GoTo(SceneRouter.Scenes.Settings); Image("settings.svg", 1795, 996, 1835, 1038);
    }
    private void Menu(string name, string title, int y, string type, string target)
    {
        var b = new Button { Name = name, Text = "       " + title, Alignment = HorizontalAlignment.Left, MouseDefaultCursorShape = CursorShape.PointingHand }; AddChild(b); Rect(b, 62, y, 406, y+78); Font(b, 30); b.AddThemeFontOverride("font", _bold);
        foreach (var state in new[] { "normal", "hover", "pressed" }) { var style = new StyleBoxTexture { Texture = GD.Load<Texture2D>(Art + type + "_" + state + ".svg"), ContentMarginLeft = 0, ContentMarginRight = 0, ContentMarginTop = 0, ContentMarginBottom = 0 }; b.AddThemeStyleboxOverride(state, style); }
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty()); b.Pressed += () => SceneRouter.Instance.GoTo(target);
    }
    private void Image(string file, int x1, int y1, int x2, int y2) { var image = new TextureRect { Texture = GD.Load<Texture2D>(Art + file), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore }; AddChild(image); Rect(image,x1,y1,x2,y2); }
    private void Label(string text, int x1,int y1,int x2,int y2,int size,Color color) { var label=new Label { Text=text, VerticalAlignment=VerticalAlignment.Center, MouseFilter=MouseFilterEnum.Ignore }; AddChild(label); Rect(label,x1,y1,x2,y2); label.AddThemeFontOverride("font",_font); SetFontSize(label,size); label.AddThemeColorOverride("font_color",color); }
    private void Font(Button b,int size) { b.AddThemeFontOverride("font",_font); SetFontSize(b,size); b.AddThemeColorOverride("font_color",Text); b.AddThemeColorOverride("font_hover_color",Colors.White); b.AddThemeColorOverride("font_pressed_color",Text); }
    private void SetFontSize(Control control, int size) { _fontSizes.Add((control, size)); control.AddThemeFontSizeOverride("font_size", Scaled(size)); }
    private void UpdateFontSizes() { foreach (var (control, size) in _fontSizes) if (IsInstanceValid(control)) control.AddThemeFontSizeOverride("font_size", Scaled(size)); }
    public override void _ExitTree() { GetViewport().SizeChanged -= UpdateFontSizes; }
    private int Scaled(int size) => Math.Max(1, Mathf.RoundToInt(size * GetViewportRect().Size.X / 1920f));
    private static StyleBoxFlat Box(Color bg,Color border,int width) => new() { BgColor=bg, BorderColor=border, BorderWidthLeft=width, BorderWidthRight=width, BorderWidthTop=width, BorderWidthBottom=width };
    private static void Empty(Button b) { b.AddThemeStyleboxOverride("normal",new StyleBoxEmpty()); b.AddThemeStyleboxOverride("hover",Box(new Color(.5f,.5f,.5f,.16f),Colors.Transparent,0)); b.AddThemeStyleboxOverride("pressed",Box(new Color(.5f,.5f,.5f,.28f),Colors.Transparent,0)); b.AddThemeStyleboxOverride("focus",new StyleBoxEmpty()); }
    private void Line(int x1,int y1,int x2,int y2,Color color) { var line=new ColorRect { Color=color, MouseFilter=MouseFilterEnum.Ignore }; AddChild(line); Rect(line,x1,y1,x2,y2); }
    private static void Rect(Control c, int x1,int y1,int x2,int y2) { c.AnchorLeft=x1/1920f; c.AnchorTop=y1/1080f; c.AnchorRight=x2/1920f; c.AnchorBottom=y2/1080f; c.OffsetLeft=0; c.OffsetTop=0; c.OffsetRight=0; c.OffsetBottom=0; }
}
