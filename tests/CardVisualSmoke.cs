using Godot;
using System;
using System.Linq;

public partial class CardVisualSmoke : Node
{
    public override async void _Ready()
    {
        try
        {
            var viewport = new SubViewport
            {
                Size = new Vector2I(720, 360),
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            AddChild(viewport);
            var preview = GD.Load<PackedScene>("res://scenes/ui/cards/previews/violent_means_preview.tscn").Instantiate<Control>();
            viewport.AddChild(preview);

            for (var frame = 0; frame < 6; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var visual = preview.GetNode<CardVisual>("Comparison/LiveColumn/ViolentMeans");
            Check(visual.Size == CardVisual.NativeSize, "CardVisual不是192×244");
            Check(visual.TextureFilter == CanvasItem.TextureFilterEnum.Nearest, "CardVisual未使用Nearest过滤");
            Check(visual.GetNode<TextureRect>("Illustration").Texture?.GetSize() == CardVisual.NativeSize, "插图不是192×244");
            Check(visual.GetNode<TextureRect>("Frame").Texture?.GetSize() == CardVisual.NativeSize, "卡框不是192×244");
            Check(visual.GetNode<Label>("APValue").Text == "1", "Prefab默认预览费用未同步正式主数据");
            Check(visual.GetNode<Control>("Stars").GetChildCount() == 2, "Prefab默认预览稀有度未同步正式主数据");

            var outputArgument = OS.GetCmdlineUserArgs().FirstOrDefault(value => value.StartsWith("--output=", StringComparison.Ordinal));
            if (outputArgument != null)
            {
                var output = outputArgument[9..];
                var error = viewport.GetTexture().GetImage().SavePng(output);
                Check(error == Error.Ok, $"预览截图保存失败：{error}");
            }

            var definition = new CardDefinition { id = "card_violent_means", display_name = "运行时暴力手段", action_cost = 1, rarity = 2 };
            var instance = new CardInstance(definition);
            visual.Bind(instance);
            Check(visual.GetNode<Label>("APValue").Text == "1", "AP未绑定CardInstance.CurrentCost()");
            Check(visual.GetNode<Label>("CardName").Text == "运行时暴力手段", "卡名未绑定CardDefinition.display_name");
            Check(visual.GetNode<Control>("Stars").GetChildCount() == 2, "星级未绑定CardDefinition.rarity");

            var tile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>();
            viewport.AddChild(tile);
            tile.Setup(instance);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(tile.GetChildren().OfType<CardVisual>().Any(), "CardTile未按card_id装入正式CardVisual");
            Check(tile.CustomMinimumSize == CardVisual.NativeSize, "CardTile未采用192×244最小尺寸");
            Check(tile.SizeFlagsHorizontal == Control.SizeFlags.ShrinkCenter, "正面CardTile仍会被Grid横向拉伸");
            tile.Size = new Vector2(300, 300);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(tile.GetChildren().OfType<CardVisual>().Single().Size == CardVisual.NativeSize, "外层动画尺寸拉伸了CardVisual内部图层");

            var grid = new GridContainer { Columns = 3, Size = new Vector2(900, 300) };
            viewport.AddChild(grid);
            for (var index = 0; index < 3; index++)
            {
                var gridTile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>();
                grid.AddChild(gridTile);
                gridTile.Setup(instance);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(grid.GetChildren().OfType<CardTile>().All(card => card.Size == CardVisual.NativeSize), "卡包Grid仍会拉宽正式卡牌");
            Check(grid.GetChildren().OfType<CardTile>().All(card => card.GetChildren().OfType<CardVisual>().Single().Size == CardVisual.NativeSize), "卡包Grid导致动态文字与卡框错位");

            var hand = new HandFan { Size = new Vector2(900, 276), ClipContents = true };
            viewport.AddChild(hand);
            for (var index = 0; index < 8; index++)
            {
                var handTile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>();
                hand.AddChild(handTile);
                handTile.Setup(instance);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(hand.GetChildren().OfType<CardTile>().All(card => Mathf.IsZeroApprox(card.Rotation)), "正式像素卡在手牌区仍被旋转扭曲");
            Check(hand.GetChildren().OfType<CardTile>().All(card => card.Position == card.Position.Round()), "正式像素卡未对齐整数像素");
            Check(hand.GetChildren().OfType<CardTile>().All(card => card.Position.Y >= 0 && card.Position.Y + card.Size.Y <= hand.Size.Y), "正式卡面仍被HandFan裁剪");

            var miracle = GD.Load<PackedScene>("res://scenes/ui/cards/cards/miracle_heal.tscn").Instantiate<CardVisual>();
            viewport.AddChild(miracle);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(miracle.Size == CardVisual.NativeSize, "妙手回春CardVisual不是192×244");
            Check(miracle.GetNode<TextureRect>("Illustration").Texture?.GetSize() == CardVisual.NativeSize, "妙手回春插图不是192×244");
            Check(miracle.GetNode<TextureRect>("Frame").Texture?.GetSize() == CardVisual.NativeSize, "妙手回春卡框不是192×244");
            Check(miracle.GetNode<Label>("APValue").Text == "1", "妙手回春Prefab费用不是1");
            Check(miracle.GetNode<Control>("Stars").GetChildCount() == 1, "妙手回春Prefab稀有度不是1");

            var miracleDefinition = new CardDefinition { id = "card_miracle_heal", display_name = "妙手回春", action_cost = 1, rarity = 1 };
            var miracleTile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>();
            viewport.AddChild(miracleTile);
            miracleTile.Setup(new CardInstance(miracleDefinition));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(miracleTile.GetChildren().OfType<CardVisual>().Single().Name == "MiracleHeal", "CardTile未自动装入妙手回春正式场景");

            GD.Print("CARD_VISUAL_SMOKE_OK");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError("CARD_VISUAL_SMOKE_FAILED: " + exception);
            GetTree().Quit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
