using System;
using System.Linq;

public static class BattleOutcomeTest
{
    public static void Run()
    {
        TestPlayerVictory();
        TestContinueBattle();
        TestEnemyVictory();
        TestDraw();
        TestResetOutcome();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    private static void TestPlayerVictory()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 12345);

        var playerHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄",
            Type = "先锋",
            Hp = 100,
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "敌方英雄",
            Type = "刺客",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        battle.SynchronizeUnits([playerHero], [enemyHero]);

        bool battleEndedFired = false;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            battleEndedFired = true;
            outcomeAmount = data.Amount;
        });

        var result = battle.EvaluateOutcome();

        Check(result == BattleOutcome.PlayerVictory, $"Expected PlayerVictory, got {result}");
        Check(battleEndedFired, "BattleEnded event was not fired");
        Check(outcomeAmount == 1, $"Expected Amount=1 for PlayerVictory, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestPlayerVictory: 击杀最后一个敌方英雄后触发PlayerVictory");
    }

    private static void TestContinueBattle()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 12346);

        var playerHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄",
            Type = "先锋",
            Hp = 100,
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero1 = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "敌方英雄1",
            Type = "刺客",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero2 = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_3" },
            Name = "敌方英雄2",
            Type = "斥候",
            Hp = 50,
            MaxHp = 100,
            Attack = 10
        };

        battle.SynchronizeUnits([playerHero], [enemyHero1, enemyHero2]);

        bool battleEndedFired = false;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => battleEndedFired = true);

        var result = battle.EvaluateOutcome();

        Check(result == BattleOutcome.Playing, $"Expected Playing, got {result}");
        Check(!battleEndedFired, "BattleEnded event should not fire when enemy still has units");
        Check(!battle.IsFinished, "Battle should not be finished");

        Console.WriteLine("[PASS] TestContinueBattle: 多敌方存活时战斗继续");
    }

    private static void TestEnemyVictory()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 12347);

        var playerHero1 = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄1",
            Type = "先锋",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        var playerHero2 = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "我方英雄2",
            Type = "刺客",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_3" },
            Name = "敌方英雄",
            Type = "斥候",
            Hp = 50,
            MaxHp = 100,
            Attack = 10
        };

        battle.SynchronizeUnits([playerHero1, playerHero2], [enemyHero]);

        bool battleEndedFired = false;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            battleEndedFired = true;
            outcomeAmount = data.Amount;
        });

        var result = battle.EvaluateOutcome();

        Check(result == BattleOutcome.EnemyVictory, $"Expected EnemyVictory, got {result}");
        Check(battleEndedFired, "BattleEnded event was not fired");
        Check(outcomeAmount == -1, $"Expected Amount=-1 for EnemyVictory, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestEnemyVictory: 我方英雄全灭时触发EnemyVictory");
    }

    private static void TestDraw()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 12348);

        var playerHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄",
            Type = "先锋",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "敌方英雄",
            Type = "刺客",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        battle.SynchronizeUnits([playerHero], [enemyHero]);

        bool battleEndedFired = false;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            battleEndedFired = true;
            outcomeAmount = data.Amount;
        });

        var result = battle.EvaluateOutcome();

        Check(result == BattleOutcome.Draw, $"Expected Draw, got {result}");
        Check(battleEndedFired, "BattleEnded event was not fired");
        Check(outcomeAmount == 0, $"Expected Amount=0 for Draw, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestDraw: 双方同归于尽时触发Draw");
    }

    private static void TestResetOutcome()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 12349);

        var enemyHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "敌方英雄",
            Type = "刺客",
            Hp = 0,
            MaxHp = 100,
            Attack = 10
        };

        battle.SynchronizeUnits([], [enemyHero]);

        battle.EvaluateOutcome();
        Check(battle.IsFinished, "Battle should be finished before reset");

        battle.ResetOutcome();
        Check(!battle.IsFinished, "Battle should not be finished after reset");
        Check(battle.Outcome == BattleOutcome.Playing, "Outcome should be Playing after reset");

        var playerHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄",
            Type = "先锋",
            Hp = 100,
            MaxHp = 100,
            Attack = 10
        };
        battle.SynchronizeUnits([playerHero], [enemyHero]);

        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.PlayerVictory, $"Expected PlayerVictory after adding player unit, got {result}");

        Console.WriteLine("[PASS] TestResetOutcome: ResetOutcome后可以重新评估胜负");
    }
}
