using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattleUiWindowSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var sizeText = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--size=", StringComparison.Ordinal))?[7..] ?? "1920x1080";
            var parts = sizeText.Split('x');
            var requested = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
            DisplayServer.WindowSetSize(requested);
            for (var frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var actual = DisplayServer.WindowGetSize();
            if (actual != requested) throw new InvalidOperationException($"实际窗口 {actual.X}x{actual.Y}，预期 {requested.X}x{requested.Y}");

            var arena = GD.Load<PackedScene>("res://scenes/training_arena.tscn").Instantiate<TrainingArena>();
            AddChild(arena);
            for (var frame = 0; frame < 4; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var logical = arena.GetViewportRect().Size;
            if (!logical.IsEqualApprox(new Vector2(1920, 1080))) throw new InvalidOperationException($"16:9窗口下逻辑画布异常：{logical}");

            var settings = arena.GetNode<Button>("%SettingsButton");
            var position = settings.GetGlobalRect().GetCenter();
            GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            GetViewport().PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!arena.GetNode<AcceptDialog>("%SettingsDialog").Visible) throw new InvalidOperationException("Stretch后的GUI点击区域与视觉位置不一致");
            GD.Print($"BATTLE_UI_WINDOW_SMOKE_OK {requested.X}x{requested.Y}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("BATTLE_UI_WINDOW_SMOKE_FAILED: " + exception);
            GetTree().Quit(1);
        }
    }
}
