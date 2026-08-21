using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattleUiSmoke : Node
{
    public override async void _Ready()
    {
        try { await Run(); GD.Print("CSHARP_BATTLE_UI_SMOKE_OK"); GetTree().Quit(0); }
        catch (Exception exception) { GD.PushError("BATTLE_UI_SMOKE_FAILED: " + exception); GetTree().Quit(1); }
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Run()
    {
        var viewport = new SubViewport { Size = new Vector2I(1920, 1080), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(viewport);
        var arena = GD.Load<PackedScene>("res://scenes/training_arena.tscn").Instantiate<TrainingArena>();
        viewport.AddChild(arena); await Frame(); await Frame();

        var root = arena.GetNode<Control>("Margin/Root");
        var top = arena.GetNode<Control>("Margin/Root/TopBar");
        var left = arena.GetNode<Control>("Margin/Root/MainRow/LeftSidebar");
        var center = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn");
        var right = arena.GetNode<Control>("Margin/Root/MainRow/RightSidebar");
        var battlefield = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn/Battlefield");
        var handArea = arena.GetNode<Control>("Margin/Root/MainRow/CenterColumn/HandArea");
        Check(Mathf.IsEqualApprox(root.Size.X, 1888), "1920 Master 安全区宽度错误");
        Check(Mathf.IsEqualApprox(top.Size.Y, 64), "TopBar 不是64px");
        Check(Mathf.IsEqualApprox(left.Size.X, 240), "LeftSidebar 不是240px");
        Check(Mathf.IsEqualApprox(center.Size.X, 1328), "CenterColumn 不是1328px");
        Check(Mathf.IsEqualApprox(right.Size.X, 304), "RightSidebar 不是304px");
        Check(Mathf.IsEqualApprox(battlefield.Size.Y, 700), $"Battlefield 不是700px，实际 {battlefield.Size.Y}");
        Check(Mathf.IsEqualApprox(handArea.Size.Y, 268), $"HandArea 不是268px，实际 {handArea.Size.Y}");

        var allies = arena.GetNode<HBoxContainer>("%AllyRow").GetChildren().OfType<UnitSlot>().ToArray();
        var enemies = arena.GetNode<HBoxContainer>("%EnemyRow").GetChildren().OfType<UnitSlot>().ToArray();
        Check(allies.Length == 5 && enemies.Length == 5, "BattleSlot 未保持5v5复用");
        for (var index = 0; index < 5; index++) Check(Mathf.IsEqualApprox(allies[index].GlobalPosition.X, enemies[index].GlobalPosition.X), $"第{index + 1}列敌我槽未对齐");

        var rightPanel = arena.GetNode<BattleRightSidebar>("%ContentHost");
        var detailText = arena.GetNode<RichTextLabel>("%DetailText");
        var rightClick = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        var firstCard = arena.GetNode<HandFan>("%Hand").GetChildren().OfType<CardTile>().First();
        firstCard.EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.CardDetail, "卡牌详情未进入固定右栏");
        arena.GetNode<Button>("%CancelButton").EmitSignal(Button.SignalName.Pressed); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.CommanderOverview, "取消选择未恢复指挥官总览");

        enemies[0].SetUnit(new HeroCardInstance(arena.content.heroes[1], "ai").Deploy());
        enemies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.EnemyDetail, "敌人右键详情未进入固定右栏");
        var enemyDefinition = arena.content.heroes[1];
        Check(!detailText.Text.Contains(enemyDefinition.skill_1_text) && !detailText.Text.Contains(enemyDefinition.passive_text) && !detailText.Text.Contains(enemyDefinition.leader_bonus_text), "EnemyDetail 泄露了敌方完整技能、被动或队长能力");
        allies[0].SetUnit(new HeroCardInstance(arena.content.heroes[0]).Deploy());
        allies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.HeroDetail, "我方英雄右键详情未进入固定右栏");
        Check(detailText.Text.Contains(arena.content.heroes[0].skill_1_text), "HeroDetail 没有显示我方已知技能");

        var allyCard = new CardInstance(arena.content.cards.First(card => card.target_kind == CardDefinition.TargetKind.AllyHero));
        var dragTile = GD.Load<PackedScene>("res://scenes/components/card_tile.tscn").Instantiate<CardTile>(); AddChild(dragTile); dragTile.Setup(allyCard); await Frame();
        var dropped = false; allies[0].CardDropped += (_, card) => dropped = card == allyCard;
        dragTile.ForceDrag(dragTile.GetInstanceId(), new Button { CustomMinimumSize = new Vector2(144, 192) });
        var dragData = dragTile.GetViewport().GuiGetDragData();
        Check(allies[0]._CanDropData(Vector2.Zero, dragData), "合法锦囊拖拽目标未被识别");
        allies[0]._DropData(Vector2.Zero, dragData); await Frame();
        Check(dropped, "Drag/Drop 回调没有把卡牌交给合法战位");
        enemies[0].SetInteractionEnabled(false);
        enemies[0].GetNode<Button>("%InteractionArea").EmitSignal(Control.SignalName.GuiInput, rightClick); await Frame();
        Check(rightPanel.Mode == BattleRightSidebar.RightPanelMode.HeroDetail, "禁用战斗交互后敌方战位仍响应右键");
        Check(!enemies[0]._CanDropData(Vector2.Zero, dragData), "禁用战斗交互后敌方战位仍接收拖放");
        dragTile.GetViewport().GuiCancelDrag();
        dragTile.QueueFree();

        var fan = new HandFan { Size = new Vector2(1280, 268) }; AddChild(fan); await Frame();
        foreach (var count in new[] { 1, 2, 4, 5, 6, 8 })
        {
            foreach (var child in fan.GetChildren()) child.QueueFree();
            await Frame();
            for (var index = 0; index < count; index++) fan.AddChild(new Button());
            await Frame(); fan.ArrangeCards(); await Frame();
            var cards = fan.GetChildren().OfType<Control>().ToArray();
            Check(cards.Length == count, $"{count}张手牌布局数量错误");
            Check(cards.All(card => Mathf.IsEqualApprox(card.Size.X / card.Size.Y, .75f)), $"{count}张手牌未保持3:4");
            Check(cards.All(card => card.Position.X >= 0 && card.Position.X + card.Size.X <= fan.Size.X + .5f), $"{count}张手牌越出HandArea");
            if (count > 1) Check(cards.First().Rotation < cards.Last().Rotation, $"{count}张手牌没有形成扇形旋转");
        }
        var hoverCard = fan.GetChild<Control>(3); hoverCard.EmitSignal(Control.SignalName.MouseEntered); await Delay(.2);
        Check(Mathf.IsEqualApprox(hoverCard.Rotation, 0, .01f) && hoverCard.Scale.X > 1, "Hover未回正并放大卡牌");
        hoverCard.EmitSignal(Control.SignalName.MouseExited); await Delay(.2);
        Check(Mathf.IsEqualApprox(hoverCard.Scale.X, 1, .01f), "Hover结束后卡牌未平滑回位");

        fan.QueueFree(); viewport.QueueFree(); await Frame();
    }
}
