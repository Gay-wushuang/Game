using Godot;
using System;

public partial class BattleOutcomeRunner : Node
{
    public override void _Ready()
    {
        try { BattleOutcomeTest.Run(); GetTree().Quit(); }
        catch (Exception error) { GD.PushError("BATTLE_OUTCOME_FAILED: " + error); GetTree().Quit(1); }
    }
}
