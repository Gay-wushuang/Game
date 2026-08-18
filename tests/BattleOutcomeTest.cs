using System;
using System.Linq;

public static class BattleOutcomeTest
{
    public static void Run()
    {
        // ===== 现有测试（Reserve默认=0，行为不变） =====
        TestPlayerVictory();
        TestContinueBattle();
        TestEnemyVictory();
        TestDraw();
        TestResetOutcome();
        
        // ===== BattleRules 规则测试 =====
        TestBattleRulesCounters();
        
        // ===== FinalizeDeaths 集成测试 =====
        TestFinalizeDeathsPlayerVictory();
        TestFinalizeDeathsContinue();
        TestFinalizeDeathsEnemyVictory();
        TestFinalizeDeathsDraw();
        TestBattleEndedFiresOnce();
        TestBattleEndedNotFiresAgain();
        TestResetAfterBattleEnded();
        TestIsFinishedPreventsFurtherActions();
        TestFinalizeDeathsWithHeroBag();
        TestFinalizeDeathsDoubleKill();
        
        // ===== Phase A: Reserve Hero 测试 =====
        TestReserveHeroPlaying();
        TestReserveHeroPreventsDefeat();
        TestReserveHeroEnemyVictory();
        TestReserveHeroPreventsVictory();
        TestReserveHeroPlayerVictory();
        TestReserveHeroDraw();
        TestReserveHeroDecrement();
        TestReserveHeroReset();
        TestReserveHeroBattleEndedOnce();
        
        // ===== Phase B: RNG 测试 =====
        TestRngSameSeed();
        TestRngDifferentSeed();
        TestRngResetSameSeed();
        
        // ===== Phase C: Passive 测试 =====
        TestPassivePlacementSuccess();
        TestPassiveDuplicatePlacement();
        TestPassiveWrongOwner();
        TestPassiveDeadHero();
        TestPassiveMissingCard();
        TestPassiveRemoval();
        
        // ===== Phase D: 回归测试 =====
        TestRngResetBeforeSetup();
        TestDeployedHeroCanPlacePassive();
        TestDeadHeroRejectsPassiveAfterDeath();
        
        Console.WriteLine("[PASS] 所有 BattleOutcome / BattleRules / Reserve / RNG / Passive / 回归 测试通过");
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    // ===== 现有测试 =====

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

    // ===== BattleRules 规则测试 =====

    private static void TestBattleRulesCounters()
    {
        // 先锋克制刺客
        Check(BattleRules.GetRelation("先锋", "刺客") == "克制", "先锋应克制刺客");
        // 先锋克制祭司
        Check(BattleRules.GetRelation("先锋", "祭司") == "克制", "先锋应克制祭司");
        // 刺客克制斥候
        Check(BattleRules.GetRelation("刺客", "斥候") == "克制", "刺客应克制斥候");
        // 刺客克制祭司
        Check(BattleRules.GetRelation("刺客", "祭司") == "克制", "刺客应克制祭司");
        // 斥候克制先锋
        Check(BattleRules.GetRelation("斥候", "先锋") == "克制", "斥候应克制先锋");
        // 斥候克制祭司
        Check(BattleRules.GetRelation("斥候", "祭司") == "克制", "斥候应克制祭司");
        // 祭司面对前三者返回"被克制"
        Check(BattleRules.GetRelation("祭司", "先锋") == "被克制", "祭司应被先锋克制");
        Check(BattleRules.GetRelation("祭司", "刺客") == "被克制", "祭司应被刺客克制");
        Check(BattleRules.GetRelation("祭司", "斥候") == "被克制", "祭司应被斥候克制");
        // 循环关系
        Check(BattleRules.GetRelation("刺客", "先锋") == "被克制", "刺客应被先锋克制");
        Check(BattleRules.GetRelation("斥候", "刺客") == "被克制", "斥候应被刺客克制");
        Check(BattleRules.GetRelation("先锋", "斥候") == "被克制", "先锋应被斥候克制");
        // 同职业中性
        Check(BattleRules.GetRelation("先锋", "先锋") == "中性", "同职业应为中性");
        Check(BattleRules.GetRelation("刺客", "刺客") == "中性", "同职业应为中性");
        // 未知职业中性
        Check(BattleRules.GetRelation("无职业", "先锋") == "中性", "未知职业应为中性");
        Check(BattleRules.GetRelation("先锋", "未知职业") == "中性", "未知职业应为中性");

        Console.WriteLine("[PASS] TestBattleRulesCounters: 职业克制规则全部正确");
    }

    // ===== FinalizeDeaths 集成测试 =====

    private static void TestFinalizeDeathsPlayerVictory()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20001);

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

        int eventCount = 0;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            eventCount++;
            outcomeAmount = data.Amount;
        });

        // 使用 FinalizeDeaths 统一结算
        var result = battle.FinalizeDeaths([playerHero], [enemyHero]);

        Check(result == BattleOutcome.PlayerVictory, $"Expected PlayerVictory, got {result}");
        Check(eventCount == 1, $"Expected exactly 1 BattleEnded event, got {eventCount}");
        Check(outcomeAmount == 1, $"Expected Amount=1, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestFinalizeDeathsPlayerVictory: FinalizeDeaths正确判定PlayerVictory");
    }

    private static void TestFinalizeDeathsContinue()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20002);

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

        int eventCount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => eventCount++);

        var result = battle.FinalizeDeaths([playerHero], [enemyHero1, enemyHero2]);

        Check(result == BattleOutcome.Playing, $"Expected Playing, got {result}");
        Check(eventCount == 0, "BattleEnded should not fire when enemy still has units");
        Check(!battle.IsFinished, "Battle should not be finished");

        Console.WriteLine("[PASS] TestFinalizeDeathsContinue: 多敌方存活时FinalizeDeaths正确保持Playing");
    }

    private static void TestFinalizeDeathsEnemyVictory()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20003);

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

        int eventCount = 0;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            eventCount++;
            outcomeAmount = data.Amount;
        });

        var result = battle.FinalizeDeaths([playerHero1, playerHero2], [enemyHero]);

        Check(result == BattleOutcome.EnemyVictory, $"Expected EnemyVictory, got {result}");
        Check(eventCount == 1, $"Expected exactly 1 BattleEnded event, got {eventCount}");
        Check(outcomeAmount == -1, $"Expected Amount=-1, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestFinalizeDeathsEnemyVictory: FinalizeDeaths正确判定EnemyVictory");
    }

    private static void TestFinalizeDeathsDraw()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20004);

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

        int eventCount = 0;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            eventCount++;
            outcomeAmount = data.Amount;
        });

        var result = battle.FinalizeDeaths([playerHero], [enemyHero]);

        Check(result == BattleOutcome.Draw, $"Expected Draw, got {result}");
        Check(eventCount == 1, $"Expected exactly 1 BattleEnded event, got {eventCount}");
        Check(outcomeAmount == 0, $"Expected Amount=0, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestFinalizeDeathsDraw: FinalizeDeaths正确判定Draw");
    }

    private static void TestBattleEndedFiresOnce()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20005);

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

        int eventCount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => eventCount++);

        // 第一次调用 FinalizeDeaths
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 1, $"After first FinalizeDeaths: expected 1 event, got {eventCount}");

        // 第二次调用 EvaluateOutcome (不应再次触发)
        battle.EvaluateOutcome();
        Check(eventCount == 1, $"After second EvaluateOutcome: expected still 1 event, got {eventCount}");

        // 第三次调用 FinalizeDeaths (不应再次触发)
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 1, $"After third FinalizeDeaths: expected still 1 event, got {eventCount}");

        Console.WriteLine("[PASS] TestBattleEndedFiresOnce: BattleEnded事件同一场只触发一次");
    }

    private static void TestBattleEndedNotFiresAgain()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20006);

        // 初始状态：双方都有存活单位
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
            Hp = 50,
            MaxHp = 100,
            Attack = 10
        };

        int eventCount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => eventCount++);

        // 第一次：双方都存活，不应触发
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 0, $"Expected 0 events when both sides alive, got {eventCount}");
        Check(!battle.IsFinished, "Battle should not be finished");

        // 击杀敌方
        enemyHero.Hp = 0;
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 1, $"Expected 1 event after enemy killed, got {eventCount}");
        Check(battle.IsFinished, "Battle should be finished");

        // 再次调用不应触发
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 1, $"Expected still 1 event, got {eventCount}");

        Console.WriteLine("[PASS] TestBattleEndedNotFiresAgain: 战斗结束后EvaluateOutcome不再触发事件");
    }

    private static void TestResetAfterBattleEnded()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20007);

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

        // 触发战斗结束
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(battle.IsFinished, "Battle should be finished");

        // Reset
        battle.ResetOutcome();
        Check(!battle.IsFinished, "Battle should not be finished after reset");
        Check(battle.Outcome == BattleOutcome.Playing, "Outcome should be Playing");

        // 可以重新触发
        playerHero.Hp = 100;
        enemyHero.Hp = 0;
        int eventCount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => eventCount++);
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(eventCount == 1, $"Expected 1 event after reset+kill, got {eventCount}");
        Check(battle.IsFinished, "Battle should be finished again");

        Console.WriteLine("[PASS] TestResetAfterBattleEnded: ResetOutcome后下一场可再次触发BattleEnded");
    }

    private static void TestIsFinishedPreventsFurtherActions()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20008);

        // 初始存活
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
            Hp = 100,
            MaxHp = 100,
            Attack = 10
        };

        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(!battle.IsFinished, "Battle should not be finished initially");

        // 玩家英雄死亡
        playerHero.Hp = 0;
        battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(battle.IsFinished, "Battle should be finished after player hero dies");

        // 战斗结束后，FinalizeDeaths不应再改变状态
        // 即使我们恢复玩家英雄HP
        playerHero.Hp = 100;
        var result = battle.FinalizeDeaths([playerHero], [enemyHero]);
        Check(result == BattleOutcome.EnemyVictory, $"Outcome should remain EnemyVictory, got {result}");
        Check(battle.IsFinished, "Battle should still be finished");

        Console.WriteLine("[PASS] TestIsFinishedPreventsFurtherActions: 战斗结束后状态不可变");
    }

    private static void TestFinalizeDeathsWithHeroBag()
    {
        // 存活英雄只统计场上，不包括HeroBag
        // 这是现有行为的确认：如果场上有存活英雄，战斗继续
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20009);

        // 玩家场上没有英雄（全死了），但这应该判定失败
        // HeroBag不在BattleState中，所以无法在此测试
        // 但我们确认：如果双方场上都没有单位，平局
        battle.FinalizeDeaths([], []);
        Check(battle.Outcome == BattleOutcome.Draw, 
            $"Expected Draw when no units on either side, got {battle.Outcome}");

        Console.WriteLine("[PASS] TestFinalizeDeathsWithHeroBag: 存活判定基于场上单位");
    }

    private static void TestFinalizeDeathsDoubleKill()
    {
        // 反伤造成双方同时死亡 -> Draw
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 20010);

        var playerHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_1" },
            Name = "我方英雄",
            Type = "先锋",
            Hp = 0,  // 反伤致死
            MaxHp = 100,
            Attack = 10
        };

        var enemyHero = new UnitState
        {
            Definition = new HeroDefinition { id = "hero_role_2" },
            Name = "敌方英雄",
            Type = "刺客",
            Hp = 0,  // 攻击致死
            MaxHp = 100,
            Attack = 10
        };

        int eventCount = 0;
        int outcomeAmount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, data =>
        {
            eventCount++;
            outcomeAmount = data.Amount;
        });

        var result = battle.FinalizeDeaths([playerHero], [enemyHero]);

        Check(result == BattleOutcome.Draw, $"Expected Draw for double kill, got {result}");
        Check(eventCount == 1, $"Expected 1 event, got {eventCount}");
        Check(outcomeAmount == 0, $"Expected Amount=0 for Draw, got {outcomeAmount}");
        Check(battle.IsFinished, "Battle should be finished");

        Console.WriteLine("[PASS] TestFinalizeDeathsDoubleKill: 双方同时死亡触发Draw");
    }

    // ===== Phase A: Reserve Hero 测试 =====

    // Test 1: 场上有存活英雄 + Reserve=0 → Playing
    private static void TestReserveHeroPlaying()
    {
        var battle = NewBattle(30001);
        var playerHero = AliveHero("p1", "先锋", 100);
        var enemyHero = AliveHero("e1", "刺客", 50);
        battle.SynchronizeUnits([playerHero], [enemyHero]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.Playing, $"Expected Playing, got {result}");
        Check(!battle.IsFinished, "Battle should not be finished");
        
        Console.WriteLine("[PASS] TestReserveHeroPlaying: 双方场上存活→Playing");
    }

    // Test 2: 玩家场上全灭 + Reserve=1 → 不得判负
    private static void TestReserveHeroPreventsDefeat()
    {
        var battle = NewBattle(30002);
        battle.SetReserveHeroCount("player", 1);
        var deadPlayer = DeadHero("p1", "先锋");
        var enemyHero = AliveHero("e1", "刺客", 50);
        battle.SynchronizeUnits([deadPlayer], [enemyHero]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.Playing, $"Expected Playing when player has reserve, got {result}");
        Check(!battle.IsFinished, "Battle should not be finished with player reserve");
        
        Console.WriteLine("[PASS] TestReserveHeroPreventsDefeat: 玩家场上全灭但有Reserve→Playing");
    }

    // Test 3: 玩家场上全灭 + Reserve=0 + 敌方有英雄 → EnemyVictory
    private static void TestReserveHeroEnemyVictory()
    {
        var battle = NewBattle(30003);
        battle.SetReserveHeroCount("player", 0);
        battle.SetReserveHeroCount("ai", 0);
        var deadPlayer = DeadHero("p1", "先锋");
        var enemyHero = AliveHero("e1", "刺客", 50);
        battle.SynchronizeUnits([deadPlayer], [enemyHero]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.EnemyVictory, $"Expected EnemyVictory, got {result}");
        Check(battle.IsFinished, "Battle should be finished");
        
        Console.WriteLine("[PASS] TestReserveHeroEnemyVictory: 玩家全灭+Reserve=0→EnemyVictory");
    }

    // Test 4: 敌方场上全灭 + EnemyReserve=1 → 不得 PlayerVictory
    private static void TestReserveHeroPreventsVictory()
    {
        var battle = NewBattle(30004);
        battle.SetReserveHeroCount("ai", 1);
        var playerHero = AliveHero("p1", "先锋", 100);
        var deadEnemy = DeadHero("e1", "刺客");
        battle.SynchronizeUnits([playerHero], [deadEnemy]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.Playing, $"Expected Playing when enemy has reserve, got {result}");
        Check(!battle.IsFinished, "Battle should not be finished with enemy reserve");
        
        Console.WriteLine("[PASS] TestReserveHeroPreventsVictory: 敌方全灭但有Reserve→Playing");
    }

    // Test 5: 敌方场上全灭 + EnemyReserve=0 + 玩家有英雄 → PlayerVictory
    private static void TestReserveHeroPlayerVictory()
    {
        var battle = NewBattle(30005);
        battle.SetReserveHeroCount("player", 0);
        battle.SetReserveHeroCount("ai", 0);
        var playerHero = AliveHero("p1", "先锋", 100);
        var deadEnemy = DeadHero("e1", "刺客");
        battle.SynchronizeUnits([playerHero], [deadEnemy]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.PlayerVictory, $"Expected PlayerVictory, got {result}");
        Check(battle.IsFinished, "Battle should be finished");
        
        Console.WriteLine("[PASS] TestReserveHeroPlayerVictory: 敌方全灭+Reserve=0→PlayerVictory");
    }

    // Test 6: 双方场上全灭 + 双方Reserve=0 → Draw
    private static void TestReserveHeroDraw()
    {
        var battle = NewBattle(30006);
        battle.SetReserveHeroCount("player", 0);
        battle.SetReserveHeroCount("ai", 0);
        var deadPlayer = DeadHero("p1", "先锋");
        var deadEnemy = DeadHero("e1", "刺客");
        battle.SynchronizeUnits([deadPlayer], [deadEnemy]);
        
        var result = battle.EvaluateOutcome();
        Check(result == BattleOutcome.Draw, $"Expected Draw, got {result}");
        Check(battle.IsFinished, "Battle should be finished");
        
        Console.WriteLine("[PASS] TestReserveHeroDraw: 双方全灭+Reserve=0→Draw");
    }

    // Test 7: 部署后 Reserve 正确减少
    private static void TestReserveHeroDecrement()
    {
        var battle = NewBattle(30007);
        battle.SetReserveHeroCount("player", 3);
        Check(battle.PlayerReserveHeroCount == 3, "Initial reserve should be 3");
        
        battle.DecrementReserveHero("player");
        Check(battle.PlayerReserveHeroCount == 2, "After 1st deploy: reserve should be 2");
        
        battle.DecrementReserveHero("player");
        Check(battle.PlayerReserveHeroCount == 1, "After 2nd deploy: reserve should be 1");
        
        battle.DecrementReserveHero("ai");
        Check(battle.EnemyReserveHeroCount == 0, "Enemy reserve should stay 0 (not decremented for player)");
        
        // Decrement enemy
        battle.SetReserveHeroCount("ai", 2);
        battle.DecrementReserveHero("ai");
        Check(battle.EnemyReserveHeroCount == 1, "Enemy reserve should be 1 after decrement");
        
        // Underflow guard
        battle.DecrementReserveHero("player"); // already 1
        battle.DecrementReserveHero("player"); // now 0, should not go negative
        Check(battle.PlayerReserveHeroCount == 0, "Reserve should not go negative");
        
        Console.WriteLine("[PASS] TestReserveHeroDecrement: Reserve正确减少，不会负数");
    }

    // Test 8: Reset 后 Reserve 恢复初始值
    private static void TestReserveHeroReset()
    {
        var battle = NewBattle(30008);
        battle.SetReserveHeroCount("player", 3);
        battle.SetReserveHeroCount("ai", 3);
        battle.DecrementReserveHero("player");
        battle.DecrementReserveHero("player");
        battle.DecrementReserveHero("ai");
        
        // Simulate reset
        battle.SetReserveHeroCount("player", 3);
        battle.SetReserveHeroCount("ai", 3);
        
        Check(battle.PlayerReserveHeroCount == 3, "Player reserve should be reset to 3");
        Check(battle.EnemyReserveHeroCount == 3, "Enemy reserve should be reset to 3");
        
        // Now deploy 1 player + 2 enemy, enemy still has 1 reserve
        battle.DecrementReserveHero("player");
        battle.DecrementReserveHero("ai");
        battle.DecrementReserveHero("ai");
        
        Check(battle.PlayerReserveHeroCount == 2, "Player reserve should be 2");
        Check(battle.EnemyReserveHeroCount == 1, "Enemy reserve should be 1");
        
        Console.WriteLine("[PASS] TestReserveHeroReset: Reserve可重置并正确计算");
    }

    // Test 9: BattleEnded 同一局只能触发一次（带Reserve）
    private static void TestReserveHeroBattleEndedOnce()
    {
        var battle = NewBattle(30009);
        battle.SetReserveHeroCount("player", 0);
        battle.SetReserveHeroCount("ai", 0);
        var deadPlayer = DeadHero("p1", "先锋");
        var deadEnemy = DeadHero("e1", "刺客");
        battle.SynchronizeUnits([deadPlayer], [deadEnemy]);
        
        int eventCount = 0;
        battle.Events.Subscribe(BattleEvent.BattleEnded, _ => eventCount++);
        
        battle.EvaluateOutcome();
        Check(eventCount == 1, $"First Evaluate: expected 1 event, got {eventCount}");
        
        battle.EvaluateOutcome();
        Check(eventCount == 1, $"Second Evaluate: expected still 1, got {eventCount}");
        
        Console.WriteLine("[PASS] TestReserveHeroBattleEndedOnce: Reserve场景下BattleEnded只触发一次");
    }

    // ===== Phase B: RNG 测试 =====

    private static void TestRngSameSeed()
    {
        // Same seed on same BattleState should produce same deck order
        var deck1 = new DeckState();
        var battle1 = NewBattleWithDecks(50001, deck1, new DeckState());
        deck1.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5") }, "player");
        
        var deck2 = new DeckState();
        var battle2 = NewBattleWithDecks(50001, deck2, new DeckState());
        deck2.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5") }, "player");
        
        var draw1 = deck1.Draw(5);
        var draw2 = deck2.Draw(5);
        
        Check(draw1.Count == draw2.Count, "Drawn card counts should match");
        for (int i = 0; i < draw1.Count; i++)
        {
            Check(draw1[i].Definition.id == draw2[i].Definition.id,
                $"Card at index {i}: expected {draw2[i].Definition.id}, got {draw1[i].Definition.id}");
        }
        
        Console.WriteLine("[PASS] TestRngSameSeed: 相同seed产生相同牌序");
    }

    private static void TestRngDifferentSeed()
    {
        // Different seeds should produce different shuffles
        var deck1 = new DeckState();
        var battle1 = NewBattleWithDecks(50002, deck1, new DeckState());
        deck1.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5"), MakeCard("c6"), MakeCard("c7"), MakeCard("c8") }, "player");
        
        var deck2 = new DeckState();
        var battle2 = NewBattleWithDecks(54321, deck2, new DeckState());
        deck2.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5"), MakeCard("c6"), MakeCard("c7"), MakeCard("c8") }, "player");
        
        var draw1 = deck1.Draw(8);
        var draw2 = deck2.Draw(8);
        
        // With 8 unique cards, it's very unlikely both shuffles produce identical order
        bool allSame = true;
        for (int i = 0; i < draw1.Count && i < draw2.Count; i++)
        {
            if (draw1[i].Definition.id != draw2[i].Definition.id) { allSame = false; break; }
        }
        Check(!allSame, "不同seed应产生不同的牌序（8张唯一卡）");
        
        Console.WriteLine("[PASS] TestRngDifferentSeed: 不同seed产生不同牌序");
    }

    private static void TestRngResetSameSeed()
    {
        // Test actual ResetRandom on SAME BattleState:
        // consume RNG, Reset, re-consume → same sequence
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 66666);
        
        // First round: setup + draw
        playerDeck.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5") }, "player");
        var draw1 = playerDeck.Draw(3);
        var firstIds = draw1.Select(c => c.Definition.id).ToList();
        
        // Reset: reset RNG + re-setup deck
        battle.ResetRandom();
        playerDeck.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5") }, "player");
        var draw2 = playerDeck.Draw(3);
        var secondIds = draw2.Select(c => c.Definition.id).ToList();
        
        Check(firstIds.Count == secondIds.Count, "Draw counts should match after reset");
        for (int i = 0; i < firstIds.Count; i++)
        {
            Check(firstIds[i] == secondIds[i],
                $"After ResetRandom: card at index {i} mismatch (first={firstIds[i]}, second={secondIds[i]})");
        }
        
        Console.WriteLine("[PASS] TestRngResetSameSeed: ResetRandom后相同seed可复现牌序");
    }

    // ===== Phase C: Passive 测试 =====

    /// <summary>
    /// 为被动测试设置 BattleState：在玩家/AI Deck中放入手牌，并在指定槽位设置存活英雄。
    /// </summary>
    private static (BattleState Battle, CardInstance PassiveCard) SetupPassiveTest(
        int seed, string ownerId, int slotIndex, UnitState? hero = null, bool addToHand = true)
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, seed);
        
        // Create a Passive card in the appropriate deck
        var deck = ownerId == "player" ? playerDeck : enemyDeck;
        var passiveDef = new CardDefinition { id = "passive_test", display_name = "Passive Test", action_cost = 1, card_kind = CardDefinition.CardKind.Passive };
        var card = new CardInstance(passiveDef, ownerId);
        
        if (addToHand)
        {
            deck.Hand.Add(card);
        }
        
        // Set hero at slot
        if (hero != null)
        {
            battle.SetSlotUnit(ownerId, slotIndex, hero);
            // Also add to the flat unit list for completeness
            if (ownerId == "player") battle.PlayerUnits.Add(hero);
            else battle.EnemyUnits.Add(hero);
        }
        
        return (battle, card);
    }

    private static void TestPassivePlacementSuccess()
    {
        var hero = AliveHero("p1", "先锋", 100);
        var (battle, card) = SetupPassiveTest(40001, "player", 2, hero);
        
        bool result = battle.TryPlacePassive("player", 2, card, out var error);
        Check(result, $"TryPlacePassive should succeed but got error: {error}");
        Check(battle.Passives.Count == 1, "Should have 1 passive");
        Check(battle.Passives[0].OwnerId == "player", "Owner should be player");
        Check(battle.Passives[0].SlotIndex == 2, "Slot should be 2");
        
        Console.WriteLine("[PASS] TestPassivePlacementSuccess: TryPlacePassive成功放置被动");
    }

    private static void TestPassiveDuplicatePlacement()
    {
        var hero1 = AliveHero("p1", "先锋", 100);
        var hero2 = AliveHero("p2", "刺客", 80);
        var heroAi = AliveHero("e1", "祭司", 90);
        
        var (battle, card1) = SetupPassiveTest(40002, "player", 1, hero1);
        // Add second card to player hand
        var card2 = new CardInstance(new CardDefinition { id = "c2", display_name = "c2", action_cost = 1, card_kind = CardDefinition.CardKind.Passive }, "player");
        battle.PlayerDeck.Hand.Add(card2);
        
        // First placement succeeds
        bool r1 = battle.TryPlacePassive("player", 1, card1, out _);
        Check(r1, "First TryPlacePassive should succeed");
        
        // Same owner + same slot → should fail
        bool r2 = battle.TryPlacePassive("player", 1, card2, out var err2);
        Check(!r2, $"Second placement on same slot should fail: {err2}");
        
        // Different owner + same slot index → should SUCCEED (different owner, different slot list)
        // Setup AI slot 1 with hero
        battle.SetSlotUnit("ai", 1, heroAi);
        battle.EnemyUnits.Add(heroAi);
        var aiCard = new CardInstance(new CardDefinition { id = "ai_card", display_name = "AI Card", action_cost = 1, card_kind = CardDefinition.CardKind.Passive }, "ai");
        battle.EnemyDeck.Hand.Add(aiCard);
        bool r3 = battle.TryPlacePassive("ai", 1, aiCard, out _);
        Check(r3, "AI placing on slot 1 should succeed (different owner = different slot list)");
        
        // Same owner + different slot → should succeed
        battle.SetSlotUnit("player", 3, hero2);
        battle.PlayerUnits.Add(hero2);
        bool r4 = battle.TryPlacePassive("player", 3, card2, out _);
        Check(r4, "Different slot for same owner should succeed");
        
        Console.WriteLine("[PASS] TestPassiveDuplicatePlacement: 同槽同owner拒绝，不同owner或不同槽位允许");
    }

    private static void TestPassiveWrongOwner()
    {
        var hero = AliveHero("p1", "先锋", 100);
        // Create AI card in player's hand (wrong owner)
        var (battle, card) = SetupPassiveTest(40003, "player", 0, hero);
        // Override: card has owner "ai" but is in player's hand
        card.OwnerId = "ai";
        
        bool result = battle.TryPlacePassive("player", 0, card, out var error);
        Check(!result, $"Wrong owner should fail: {error}");
        Check(error.Contains("归属"), $"Error should mention ownership mismatch, got: {error}");
        
        Console.WriteLine("[PASS] TestPassiveWrongOwner: Owner不匹配时放置失败");
    }

    private static void TestPassiveDeadHero()
    {
        var deadHero = DeadHero("p1", "先锋");
        var (battle, card) = SetupPassiveTest(40004, "player", 0, deadHero);
        
        bool result = battle.TryPlacePassive("player", 0, card, out var error);
        Check(!result, $"Dead hero should fail: {error}");
        Check(error.Contains("阵亡"), $"Error should mention hero death, got: {error}");
        
        Console.WriteLine("[PASS] TestPassiveDeadHero: 死亡英雄槽放置被动失败");
    }

    private static void TestPassiveMissingCard()
    {
        var hero = AliveHero("p1", "先锋", 100);
        // Create battle without adding card to hand
        var (battle, _) = SetupPassiveTest(40005, "player", 0, hero, addToHand: false);
        
        // Create a card that's NOT in hand
        var missingCard = new CardInstance(new CardDefinition { id = "missing", display_name = "Missing", action_cost = 1, card_kind = CardDefinition.CardKind.Passive }, "player");
        
        bool result = battle.TryPlacePassive("player", 0, missingCard, out var error);
        Check(!result, $"Card not in hand should fail: {error}");
        Check(error.Contains("手牌"), $"Error should mention hand, got: {error}");
        
        Console.WriteLine("[PASS] TestPassiveMissingCard: 不在手牌中的卡放置失败");
    }

    private static void TestPassiveRemoval()
    {
        var hero = AliveHero("p1", "先锋", 100);
        var (battle, card) = SetupPassiveTest(40006, "player", 1, hero);
        
        // Use internal SetPassive for setup (bypasses validation for removal test)
        battle.SetPassive("player", 1, card);
        Check(battle.Passives.Count == 1, "Should have 1 passive before removal");
        
        battle.RemovePassive(card);
        Check(battle.Passives.Count == 0, "Should have 0 passives after removal");
        
        // Remove non-existent card - should not throw
        var fakeCard = new CardInstance(new CardDefinition { id = "fake", display_name = "Fake", action_cost = 1 }, "player");
        battle.RemovePassive(fakeCard);
        Check(battle.Passives.Count == 0, "Removing non-existent card is safe");
        
        Console.WriteLine("[PASS] TestPassiveRemoval: 被动移除成功，安全处理不存在的卡");
    }

    // ===== Phase D: 回归测试 =====

    /// <summary>
    /// 验证 ResetRandom → Setup → Draw 两次产生相同开局牌序。
    /// 这模拟了 ResetTraining 的正确调用顺序：先 Reset，再 Setup/Draw。
    /// </summary>
    private static void TestRngResetBeforeSetup()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 77777);
        
        // 第一轮：ResetRandom → Setup → Draw
        battle.ResetRandom();
        playerDeck.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5"), MakeCard("c6") }, "player");
        var draw1 = playerDeck.Draw(3);
        var firstIds = draw1.Select(c => c.Definition.id).ToList();
        
        // 第二轮：同样顺序
        battle.ResetRandom();
        playerDeck.Setup(new[] { MakeCard("c1"), MakeCard("c2"), MakeCard("c3"), MakeCard("c4"), MakeCard("c5"), MakeCard("c6") }, "player");
        var draw2 = playerDeck.Draw(3);
        var secondIds = draw2.Select(c => c.Definition.id).ToList();
        
        // 验证两次相同
        Check(firstIds.Count == secondIds.Count, $"Draw count mismatch: {firstIds.Count} vs {secondIds.Count}");
        for (int i = 0; i < firstIds.Count; i++)
        {
            Check(firstIds[i] == secondIds[i],
                $"After Reset→Setup→Draw cycle: card at index {i} mismatch (first={firstIds[i]}, second={secondIds[i]})");
        }
        
        Console.WriteLine("[PASS] TestRngResetBeforeSetup: ResetRandom→Setup→Draw两次产生相同牌序");
    }

    /// <summary>
    /// 验证刚部署的存活英雄可以立即放置 Passive。
    /// 这确保 BattleState slot-unit 映射在部署后立即同步。
    /// </summary>
    private static void TestDeployedHeroCanPlacePassive()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 88888);
        
        // 模拟部署：先在 BattleState 中设置 slot 单位
        var hero = AliveHero("deployed", "先锋", 120);
        battle.SetSlotUnit("player", 2, hero);
        battle.PlayerUnits.Add(hero);
        
        // 创建 Passive 卡并加入手牌
        var passiveDef = new CardDefinition { id = "deployed_passive", display_name = "Deployed Passive", action_cost = 1, card_kind = CardDefinition.CardKind.Passive };
        var card = new CardInstance(passiveDef, "player");
        playerDeck.Hand.Add(card);
        
        // 立即尝试放置 — 应该成功
        bool result = battle.TryPlacePassive("player", 2, card, out var error);
        Check(result, $"刚部署的英雄应该能放置被动，但失败了: {error}");
        Check(battle.Passives.Count == 1, "应该有1个被动");
        Check(battle.Passives[0].SlotIndex == 2, "被动应该在slot 2");
        
        Console.WriteLine("[PASS] TestDeployedHeroCanPlacePassive: 刚部署的存活英雄可立即放置Passive");
    }

    /// <summary>
    /// 验证死亡后的英雄槽立即拒绝 Passive 放置。
    /// 这确保死亡同步后 BattleState slot-unit 映射反映真实状态。
    /// </summary>
    private static void TestDeadHeroRejectsPassiveAfterDeath()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 99999);
        
        // 模拟英雄死亡后：slot-unit 映射包含已死亡单位
        var deadHero = DeadHero("dead", "刺客");
        battle.SetSlotUnit("player", 3, deadHero);
        battle.PlayerUnits.Add(deadHero);
        
        // 创建 Passive 卡并加入手牌
        var passiveDef = new CardDefinition { id = "dead_passive", display_name = "Dead Passive", action_cost = 1, card_kind = CardDefinition.CardKind.Passive };
        var card = new CardInstance(passiveDef, "player");
        playerDeck.Hand.Add(card);
        
        // 尝试放置 — 应该失败
        bool result = battle.TryPlacePassive("player", 3, card, out var error);
        Check(!result, $"死亡英雄槽应该拒绝放置被动，但成功了");
        Check(error.Contains("阵亡"), $"错误信息应包含'阵亡'，但得到: {error}");
        
        // 同时验证空 slot 也拒绝
        battle.SetSlotUnit("player", 4, null);
        bool result2 = battle.TryPlacePassive("player", 4, card, out var error2);
        Check(!result2, $"空英雄槽应该拒绝放置被动，但成功了");
        Check(error2.Contains("没有英雄"), $"错误信息应包含'没有英雄'，但得到: {error2}");
        
        Console.WriteLine("[PASS] TestDeadHeroRejectsPassiveAfterDeath: 死亡/空slot立即拒绝Passive");
    }

    // ===== 辅助方法 =====

    private static BattleState NewBattle(int seed)
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        return new BattleState(playerDeck, enemyDeck, seed);
    }

    private static BattleState NewBattleWithDecks(int seed, DeckState playerDeck, DeckState enemyDeck)
    {
        return new BattleState(playerDeck, enemyDeck, seed);
    }

    private static UnitState AliveHero(string id, string type, int hp) => new()
    {
        Definition = new HeroDefinition { id = id },
        Name = id,
        Type = type,
        Hp = hp,
        MaxHp = hp,
        Attack = 10
    };

    private static UnitState DeadHero(string id, string type) => new()
    {
        Definition = new HeroDefinition { id = id },
        Name = id,
        Type = type,
        Hp = 0,
        MaxHp = 100,
        Attack = 10
    };

    private static CardDefinition MakeCard(string id) => new() { id = id, display_name = id, action_cost = 1 };
    private static CardInstance MakeCardInstance(string id) => new(MakeCard(id));
}
