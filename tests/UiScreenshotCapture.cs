using Godot;
using System;
using System.Linq;

public partial class UiScreenshotCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            var scene = args.FirstOrDefault(value => value.StartsWith("--scene=", StringComparison.Ordinal))?[8..];
            var output = args.FirstOrDefault(value => value.StartsWith("--output=", StringComparison.Ordinal))?[9..];
            var requestedSize = args.FirstOrDefault(value => value.StartsWith("--size=", StringComparison.Ordinal))?[7..];
            if (string.IsNullOrWhiteSpace(scene) || string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("需要 --scene= 与 --output= 参数");
            var viewportSize = ParseSize(requestedSize);
            var viewport = new SubViewport
            {
                Size = viewportSize,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            AddChild(viewport);
            viewport.AddChild(GD.Load<PackedScene>(scene).Instantiate<Control>());
            for (var frame = 0; frame < 8; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var image = viewport.GetTexture().GetImage();
            var error = image.SavePng(output);
            if (error != Error.Ok) throw new InvalidOperationException($"截图保存失败：{error}");
            GD.Print($"UI_SCREENSHOT_OK {output}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("UI_SCREENSHOT_FAILED: " + exception);
            GetTree().Quit(1);
        }
    }

    private static Vector2I ParseSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new Vector2I(1920, 1080);
        var parts = value.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var width) || !int.TryParse(parts[1], out var height) || width <= 0 || height <= 0)
        {
            throw new InvalidOperationException($"无效的 --size={value}，格式应为宽x高");
        }

        return new Vector2I(width, height);
    }
}
