using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattleUiScreenshotCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            var arguments = OS.GetCmdlineUserArgs();
            var output = arguments.FirstOrDefault(value => value.StartsWith("--output=", StringComparison.Ordinal))?[9..];
            var sizeText = arguments.FirstOrDefault(value => value.StartsWith("--size=", StringComparison.Ordinal))?[7..] ?? "1920x1080";
            if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("缺少 --output=截图路径");
            var parts = sizeText.Split('x');
            var outputSize = new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));

            var viewport = new SubViewport
            {
                Size = new Vector2I(1920, 1080),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            AddChild(viewport);
            viewport.AddChild(GD.Load<PackedScene>("res://scenes/training_arena.tscn").Instantiate<TrainingArena>());
            for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var image = viewport.GetTexture().GetImage();
            if (outputSize != viewport.Size) image.Resize(outputSize.X, outputSize.Y, Image.Interpolation.Nearest);
            var error = image.SavePng(output);
            if (error != Error.Ok) throw new InvalidOperationException($"截图保存失败：{error}");
            GD.Print($"BATTLE_UI_SCREENSHOT_OK {outputSize.X}x{outputSize.Y}: {output}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("BATTLE_UI_SCREENSHOT_FAILED: " + exception);
            GetTree().Quit(1);
        }
    }
}
