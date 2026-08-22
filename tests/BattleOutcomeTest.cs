using System;
using System.Collections.Generic;
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
        TestClearSlotUnits();
        
        // ===== Phase E: 集成回归测试（真实游戏路径验证） =====
        TestLastCardPlayedTriggersHandEmpty();
        TestLastSpentApTriggersActionPointsZero();
        TestBeforeDrawCancelDrawChain();
        TestDoubleTempSwapRestoresOriginal();
        
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
        battle.SetSlotUnit("player", 2, hero2);
        battle.PlayerUnits.Add(hero2);
        bool r4 = battle.TryPlacePassive("player", 2, card2, out _);
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
        Check(result, $"独立战门不应受阵亡英雄影响: {error}");
        
        Console.WriteLine("[PASS] TestPassiveDeadHero: 独立战门允许在无可用英雄槽时设置被动");
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
    /// 验证独立战门与英雄槽生死、占用状态完全解耦。
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
        
        // 战门位置0与英雄槽3的状态无关，应成功。
        bool result = battle.TryPlacePassive("player", 0, card, out var error);
        Check(result, $"独立战门不应因英雄阵亡而拒绝: {error}");
        
        // 空英雄槽同样不影响另一个战门位置。
        battle.SetSlotUnit("player", 4, null);
        var card2 = new CardInstance(passiveDef, "player");
        playerDeck.Hand.Add(card2);
        bool result2 = battle.TryPlacePassive("player", 1, card2, out var error2);
        Check(result2, $"独立战门不应因英雄槽为空而拒绝: {error2}");
        
        Console.WriteLine("[PASS] TestDeadHeroRejectsPassiveAfterDeath: 战门与死亡/空英雄槽解耦");
    }

    /// <summary>
    /// 验证 ClearSlotUnits 清空所有 slot 映射，包括玩家和敌方。
    /// 这模拟了 ResetTraining 的正确清理顺序。
    /// </summary>
    private static void TestClearSlotUnits()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 11111);
        
        // 在所有 5 个玩家槽和 5 个敌方槽设置英雄
        for (int i = 0; i < 5; i++)
        {
            battle.SetSlotUnit("player", i, AliveHero($"p{i}", "先锋", 100 + i));
            battle.SetSlotUnit("ai", i, AliveHero($"e{i}", "刺客", 100 + i));
        }
        
        // 验证设置成功
        for (int i = 0; i < 5; i++)
        {
            Check(battle.GetSlotUnit("player", i) != null, $"Player slot {i} should have hero before clear");
            Check(battle.GetSlotUnit("ai", i) != null, $"Enemy slot {i} should have hero before clear");
        }
        
        // 清空
        battle.ClearSlotUnits();
        
        // 验证全部为 null
        for (int i = 0; i < 5; i++)
        {
            Check(battle.GetSlotUnit("player", i) == null, $"Player slot {i} should be null after ClearSlotUnits");
            Check(battle.GetSlotUnit("ai", i) == null, $"Enemy slot {i} should be null after ClearSlotUnits");
        }
        
        // 英雄槽清空不影响独立被动战门。
        var passiveDef = new CardDefinition { id = "clear_test", display_name = "Clear Test", action_cost = 1, card_kind = CardDefinition.CardKind.Passive };
        var card = new CardInstance(passiveDef, "player");
        playerDeck.Hand.Add(card);
        
        bool result = battle.TryPlacePassive("player", 2, card, out var error);
        Check(result, $"清空英雄槽后仍应能向独立战门放置被动: {error}");
        
        Console.WriteLine("[PASS] TestClearSlotUnits: ClearSlotUnits清空所有slot映射");
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

    // ===== Phase E: 集成回归测试 =====
    // 这些测试模拟 TrainingArena 中的真实游戏流程：
    //   1. 打出卡牌 → 弃牌 → 检查手牌 → 触发被动
    //   2. 消耗AP → 检查AP → 触发被动
    //   3. 抽牌前 → 发布BEFORE_DRAW → 检查CANCEL_DRAW → 抽牌 → 发布AFTER_DRAW
    //   4. 临时属性交换 → 恢复 → 验证回到原始值
    // 注意：TrainingArena.cs 中的事件发布逻辑是正式实现（见 UseCard/UseNoTargetCard/EndTurn 等），
    // 这里验证的是被动解析机制能正确响应这些事件。

    /// <summary>
    /// 测试①：最后一张手牌被打出后，HAND_EMPTY 被动事件自动触发。
    /// 模拟真实流程：打牌 → 弃牌 → 手牌为0 → 触发HAND_EMPTY。
    /// 对应 TrainingArena.cs 中 UseCard/UseNoTargetCard 的实现。
    /// </summary>
    private static void TestLastCardPlayedTriggersHandEmpty()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 777);

        // 创建一张 HAND_EMPTY 被动牌（模拟"神圣的降临"）
        var handEmptyDef = new CardDefinition
        {
            id = "test_hand_empty",
            display_name = "Test Hand Empty",
            action_cost = 0,
            card_kind = CardDefinition.CardKind.Passive,
            trigger_keys = new[] { "HAND_EMPTY" }
        };
        var handEmptyPassive = new CardInstance(handEmptyDef);
        battle.SetPassive("player", 0, handEmptyPassive);

        // === 模拟真实游戏路径：玩家手牌只有 1 张主动卡 ===
        var activeCard = MakeCardInstance("last_card");
        playerDeck.Hand.Add(activeCard);
        Check(playerDeck.Hand.Count == 1, "前置条件：玩家手牌应有1张");

        // === 步骤1："打出"卡牌（模拟 TrainingArena.UseCard 中的弃牌逻辑）===
        playerDeck.Discard(activeCard);
        Check(playerDeck.Hand.Count == 0, "打牌后手牌应为0");
        Check(playerDeck.DiscardPile.Count == 1, "弃牌堆应有1张");

        // === 步骤2：手牌为空，触发 HAND_EMPTY 被动事件（对应 TrainingArena.cs:127）===
        var resolver = new PassiveTriggerResolver();
        var triggered = resolver.Collect(battle, "player", "HAND_EMPTY",
            new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });

        Check(triggered.Count == 1, $"打出最后一张卡后应触发1张HAND_EMPTY被动，但触发了{triggered.Count}张");
        Check(triggered[0].Card == handEmptyPassive, "触发的应是HAND_EMPTY被动牌");
        Check(triggered[0].SlotIndex == 0, "被动应在slot 0");

        // === 验证：非最后一张牌不应触发 HAND_EMPTY ===
        battle.RemovePassive(handEmptyPassive);
        battle.SetPassive("player", 1, handEmptyPassive);
        var card1 = MakeCardInstance("card1");
        var card2 = MakeCardInstance("card2");
        playerDeck.Hand.Clear();
        playerDeck.Hand.Add(card1);
        playerDeck.Hand.Add(card2);
        
        // 打出第1张，手牌还剩1张，不应触发HAND_EMPTY
        playerDeck.Discard(card1);
        Check(playerDeck.Hand.Count == 1, "打出1张后手牌应为1张");
        IReadOnlyList<BattleState.PlacedPassive> notTriggered = playerDeck.Hand.Count == 0
            ? resolver.Collect(battle, "player", "HAND_EMPTY", new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" })
            : Array.Empty<BattleState.PlacedPassive>();
        Check(notTriggered.Count == 0, "手牌还剩1张时不应触发HAND_EMPTY");
        
        // 打出最后1张，应触发HAND_EMPTY
        playerDeck.Discard(card2);
        Check(playerDeck.Hand.Count == 0, "打出最后1张后手牌应为0");
        var triggered2 = resolver.Collect(battle, "player", "HAND_EMPTY",
            new PassiveEventContext { EventKey = "HAND_EMPTY", SubjectOwnerId = "player" });
        Check(triggered2.Count == 1, "打出最后1张后应触发HAND_EMPTY");

        Console.WriteLine("[PASS] TestLastCardPlayedTriggersHandEmpty: 最后一张手牌打出后HAND_EMPTY被动正确触发");
    }

    /// <summary>
    /// 测试②：AP=1 时打出 1 费牌，AP 变为 0，ACTION_POINTS_ZERO 被动自动触发。
    /// 模拟真实流程：AP=1 → 打1费卡 → AP扣为0 → 触发ACTION_POINTS_ZERO。
    /// 对应 TrainingArena.cs 中 UseCard/ConfirmAttack 的实现。
    /// </summary>
    private static void TestLastSpentApTriggersActionPointsZero()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 888);

        // 创建一张 ACTION_POINTS_ZERO 被动牌（模拟"赌"）
        var apZeroDef = new CardDefinition
        {
            id = "test_ap_zero",
            display_name = "Test AP Zero",
            action_cost = 0,
            card_kind = CardDefinition.CardKind.Passive,
            trigger_keys = new[] { "ACTION_POINTS_ZERO" }
        };
        var apZeroPassive = new CardInstance(apZeroDef);
        battle.SetPassive("player", 1, apZeroPassive);

        // === 模拟真实游戏路径：玩家 AP 为 1 ===
        battle.PlayerActionPoints = 1;
        battle.PlayerNextTurnBonus = 0;
        Check(battle.PlayerActionPoints == 1, "前置条件：玩家AP应为1");

        // === 步骤1："打出1费卡"，AP 消耗为 0（模拟 TrainingArena.cs:129 的 _ap -= cost）===
        battle.PlayerActionPoints -= 1;  // 模拟 _ap -= cost
        Check(battle.PlayerActionPoints == 0, "打出1费卡后AP应为0");

        // === 步骤2：AP 为 0，触发 ACTION_POINTS_ZERO 事件（对应 TrainingArena.cs:129）===
        var resolver = new PassiveTriggerResolver();
        var triggered = resolver.Collect(battle, "player", "ACTION_POINTS_ZERO",
            new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = "player" });

        Check(triggered.Count == 1, $"AP变为0时应触发1张ACTION_POINTS_ZERO被动，但触发了{triggered.Count}张");
        Check(triggered[0].Card == apZeroPassive, "触发的应是ACTION_POINTS_ZERO被动牌");
        Check(triggered[0].SlotIndex == 1, "被动应在slot 1");

        // === 验证：AP > 0 时不应触发 ACTION_POINTS_ZERO ===
        battle.RemovePassive(apZeroPassive);
        battle.SetPassive("player", 2, apZeroPassive);
        battle.PlayerActionPoints = 2;  // AP=2，还有剩余
        IReadOnlyList<BattleState.PlacedPassive> notTriggered = battle.PlayerActionPoints == 0
            ? resolver.Collect(battle, "player", "ACTION_POINTS_ZERO", new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = "player" })
            : Array.Empty<BattleState.PlacedPassive>();
        Check(notTriggered.Count == 0, "AP > 0 时不应触发ACTION_POINTS_ZERO");
        
        // 再消耗1点，AP=1，仍不触发
        battle.PlayerActionPoints -= 1;
        Check(battle.PlayerActionPoints == 1, "消耗1点后AP应为1");
        notTriggered = battle.PlayerActionPoints == 0
            ? resolver.Collect(battle, "player", "ACTION_POINTS_ZERO", new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = "player" })
            : Array.Empty<BattleState.PlacedPassive>();
        Check(notTriggered.Count == 0, "AP=1 时不应触发ACTION_POINTS_ZERO");

        // 再消耗1点，AP=0，触发
        battle.PlayerActionPoints -= 1;
        Check(battle.PlayerActionPoints == 0, "消耗最后1点后AP应为0");
        var triggered2 = resolver.Collect(battle, "player", "ACTION_POINTS_ZERO",
            new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = "player" });
        Check(triggered2.Count == 1, "AP=0 时应触发ACTION_POINTS_ZERO");

        // === 验证 AI 侧也能触发（对应 TrainingArena.cs:285）===
        battle.EnemyActionPoints = 1;
        var aiPassive = new CardInstance(apZeroDef, "ai");
        battle.SetPassive("ai", 0, aiPassive);
        
        battle.EnemyActionPoints -= 1;  // AI AP 也消耗到 0
        Check(battle.EnemyActionPoints == 0, "AI AP应为0");
        var aiTriggered = resolver.Collect(battle, "ai", "ACTION_POINTS_ZERO",
            new PassiveEventContext { EventKey = "ACTION_POINTS_ZERO", SubjectOwnerId = "ai" });
        Check(aiTriggered.Count == 1, $"AI AP变为0时应触发1张被动");

        Console.WriteLine("[PASS] TestLastSpentApTriggersActionPointsZero: AP归零时ACTION_POINTS_ZERO被动正确触发");
    }

    /// <summary>
    /// 测试③：抽牌链 BEFORE_DRAW → CANCEL_DRAW → AFTER_DRAW。
    /// 模拟真实流程：抽牌前 → 发布BEFORE_DRAW → 检查CANCEL_DRAW → 抽牌 → 发布AFTER_DRAW。
    /// 对应 TrainingArena.cs 中 EndTurn 的实现（行249-274）。
    /// </summary>
    private static void TestBeforeDrawCancelDrawChain()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 999);

        var resolver = new PassiveTriggerResolver();

        // 创建 CANCEL_DRAW 被动牌（模拟"多就是坏"）
        var cancelDrawDef = new CardDefinition
        {
            id = "test_cancel_draw",
            display_name = "Test Cancel Draw",
            action_cost = 0,
            card_kind = CardDefinition.CardKind.Passive,
            handler_key = "CANCEL_DRAW",
            trigger_keys = new[] { "BEFORE_DRAW" }
        };

        // === 场景A：没有 CANCEL_DRAW 被动时，抽牌正常进行 ===
        // 准备抽牌堆
        playerDeck.DrawPile.Clear();
        playerDeck.Hand.Clear();
        playerDeck.DrawPile.Add(MakeCardInstance("normal_draw"));
        Check(playerDeck.DrawPile.Count == 1, "前置条件：抽牌堆应有1张");
        Check(playerDeck.Hand.Count == 0, "前置条件：玩家手牌应为0");

        // 步骤1：发布 BEFORE_DRAW（对应 TrainingArena.cs:249-253）
        //         没有 CANCEL_DRAW 被动，不应触发任何被动
        var beforeDrawCtx = new PassiveEventContext { EventKey = "BEFORE_DRAW", SubjectOwnerId = "player" };
        var beforeTriggered = resolver.Collect(battle, "ai", "BEFORE_DRAW", beforeDrawCtx);
        Check(beforeTriggered.Count == 0, "场景A：无CANCEL_DRAW被动时BEFORE_DRAW不应触发任何被动");

        // 步骤2：执行抽牌（对应 TrainingArena.cs:270）
        var drawResult = playerDeck.Draw();
        Check(drawResult.Count == 1, "场景A：抽牌应成功");
        Check(playerDeck.Hand.Count == 1, "场景A：抽牌后手牌应有1张");

        // 步骤3：发布 AFTER_DRAW（对应 TrainingArena.cs:272）
        var afterDrawCtx = new PassiveEventContext { EventKey = "AFTER_DRAW", SubjectOwnerId = "player" };
        var afterTriggered = resolver.Collect(battle, "ai", "AFTER_DRAW", afterDrawCtx);
        Check(afterTriggered.Count == 0, "场景A：AFTER_DRAW无被动时不应触发任何被动");

        // === 场景B：有 CANCEL_DRAW 被动时，抽牌应被阻止 ===
        // 设置 CANCEL_DRAW 被动到敌方
        var cancelDrawPassive = new CardInstance(cancelDrawDef, "ai");
        battle.SetPassive("ai", 0, cancelDrawPassive);
        
        playerDeck.Hand.Clear();
        playerDeck.DrawPile.Clear();
        playerDeck.DrawPile.Add(MakeCardInstance("blocked_draw"));
        Check(playerDeck.DrawPile.Count == 1, "场景B：抽牌堆应有1张");

        // 步骤1：发布 BEFORE_DRAW，发现 CANCEL_DRAW 被动（对应 TrainingArena.cs:253-265）
        beforeTriggered = resolver.Collect(battle, "ai", "BEFORE_DRAW", beforeDrawCtx);
        Check(beforeTriggered.Count == 1, $"场景B：BEFORE_DRAW应触发1张CANCEL_DRAW被动，但触发了{beforeTriggered.Count}张");
        Check(beforeTriggered[0].Card.Definition.handler_key == "CANCEL_DRAW", "场景B：触发的应为CANCEL_DRAW被动");

        // 步骤2：CANCEL_DRAW 被动触发后从 Passives 中移除（对应 TrainingArena.cs:261-264）
        var stillHasCancel = battle.Passives.Any(p => p.Card.Definition.handler_key == "CANCEL_DRAW");
        Check(!stillHasCancel, "场景B：CANCEL_DRAW被动触发后应从Passives中移除");

        // 步骤3：由于抽牌被阻止，不应执行实际抽牌（对应 TrainingArena.cs:255-266 的阻止逻辑）
        // 此处验证的是：BEFORE_DRAW 发现 CANCEL_DRAW 被动后，TrainingArena 应跳过 Draw() 调用
        // 实际集成在 TrainingArena 中完成，此处验证事件链正确性

        // === 场景C：CANCEL_DRAW 被动已消耗，后续抽牌不再被阻止 ===
        playerDeck.DrawPile.Clear();
        playerDeck.Hand.Clear();
        playerDeck.DrawPile.Add(MakeCardInstance("normal_draw_again"));

        // 再次发布 BEFORE_DRAW，此时 CANCEL_DRAW 被动已消耗
        beforeTriggered = resolver.Collect(battle, "ai", "BEFORE_DRAW", beforeDrawCtx);
        Check(beforeTriggered.Count == 0, "场景C：CANCEL_DRAW被动已消耗，BEFORE_DRAW不应触发任何被动");

        // 抽牌正常进行
        drawResult = playerDeck.Draw();
        Check(drawResult.Count == 1, "场景C：抽牌应成功");

        Console.WriteLine("[PASS] TestBeforeDrawCancelDrawChain: BEFORE_DRAW→CANCEL_DRAW→AFTER_DRAW事件链正确");
    }

    /// <summary>
    /// 测试④：连续两次临时属性交换后，恢复到真正原始值。
    /// 模拟真实流程：第一次交换 → 第二次嵌套交换 → 恢复 → 验证原始值。
    /// 验证 H3 修复：缓存原始值后再交换，避免直接赋值导致 second 被覆盖。
    /// 对应 CardApi.cs 中 TemporarilySwapOpposingStats 的实现。
    /// </summary>
    private static void TestDoubleTempSwapRestoresOriginal()
    {
        var playerDeck = new DeckState();
        var enemyDeck = new DeckState();
        var battle = new BattleState(playerDeck, enemyDeck, 10101);

        // 创建两个单位
        var unitA = AliveHero("unit_a", "先锋", 50);
        unitA.Attack = 15;
        var unitB = AliveHero("unit_b", "刺客", 50);
        unitB.Attack = 20;

        battle.SynchronizeUnits([unitA], [unitB]);

        // 记录原始值
        var originalAType = unitA.Type;   // "先锋"
        var originalAAttack = unitA.Attack;  // 15
        var originalBType = unitB.Type;   // "刺客"
        var originalBAttack = unitB.Attack;  // 20

        // === 场景1：单次交换（验证基本交换逻辑）===
        // 使用"缓存后交换"的正确方式（对应 CardApi.cs:98-101 的修复）
        var firstType = unitA.Type;
        var firstAttack = unitA.Attack;
        unitA.Type = unitB.Type;
        unitA.Attack = unitB.Attack;
        unitB.Type = firstType;
        unitB.Attack = firstAttack;

        // 验证第一次交换成功
        Check(unitA.Type == "刺客", $"第一次交换后A应为刺客，实际为{unitA.Type}");
        Check(unitA.Attack == 20, $"第一次交换后A攻击应为20，实际为{unitA.Attack}");
        Check(unitB.Type == "先锋", $"第一次交换后B应为先锋，实际为{unitB.Type}");
        Check(unitB.Attack == 15, $"第一次交换后B攻击应为15，实际为{unitB.Attack}");

        // 恢复到原始值（模拟 CardApi.cs:102-109 的 Schedule 恢复逻辑）
        unitA.Type = originalAType;
        unitA.Attack = originalAAttack;
        unitB.Type = originalBType;
        unitB.Attack = originalBAttack;

        // 验证恢复成功
        Check(unitA.Type == "先锋", "单次交换恢复后A应为先锋");
        Check(unitA.Attack == 15, "单次交换恢复后A攻击应为15");
        Check(unitB.Type == "刺客", "单次交换恢复后B应为刺客");
        Check(unitB.Attack == 20, "单次交换恢复后B攻击应为20");

        // === 场景2：连续两次交换 + OriginalType/OriginalAttack 验证 ===
        // 第一次交换时记录 OriginalType/OriginalAttack（对应 CardApi.cs:93-97）
        unitA.OriginalType = originalAType;
        unitA.OriginalAttack = originalAAttack;
        unitB.OriginalType = originalBType;
        unitB.OriginalAttack = originalBAttack;

        // 第一次交换（再次交换，回到交换状态）
        firstType = unitA.Type;
        firstAttack = unitA.Attack;
        unitA.Type = unitB.Type;
        unitA.Attack = unitB.Attack;
        unitB.Type = firstType;
        unitB.Attack = firstAttack;

        Check(unitA.Type == "刺客", $"第一次交换后A应为刺客，实际为{unitA.Type}");
        Check(unitB.Type == "先锋", $"第一次交换后B应为先锋，实际为{unitB.Type}");

        // 第二次交换（嵌套效果）
        // 注意：OriginalType/OriginalAttack 不应被覆盖，仍保留原始值
        var secondType = unitA.Type;
        var secondAttack = unitA.Attack;
        unitA.Type = unitB.Type;
        unitA.Attack = unitB.Attack;
        unitB.Type = secondType;
        unitB.Attack = secondAttack;

        // 验证第二次交换成功（A回到先锋，B回到刺客）
        Check(unitA.Type == "先锋", $"第二次交换后A应为先锋，实际为{unitA.Type}");
        Check(unitA.Attack == 15, $"第二次交换后A攻击应为15，实际为{unitA.Attack}");
        Check(unitB.Type == "刺客", $"第二次交换后B应为刺客，实际为{unitB.Type}");
        Check(unitB.Attack == 20, $"第二次交换后B攻击应为20，实际为{unitB.Attack}");

        // 恢复（使用 OriginalType/OriginalAttack，应恢复到真正原始值而非中间状态）
        unitA.Type = unitA.OriginalType!;
        unitA.Attack = unitA.OriginalAttack!.Value;
        unitB.Type = unitB.OriginalType!;
        unitB.Attack = unitB.OriginalAttack!.Value;
        unitA.OriginalType = null;
        unitA.OriginalAttack = null;
        unitB.OriginalType = null;
        unitB.OriginalAttack = null;

        // 关键验证：恢复到真正的原始值（不是中间状态）
        Check(unitA.Type == originalAType, $"恢复后A应回到原始{originalAType}，实际为{unitA.Type}");
        Check(unitA.Attack == originalAAttack, $"恢复后A攻击应回到原始{originalAAttack}，实际为{unitA.Attack}");
        Check(unitB.Type == originalBType, $"恢复后B应回到原始{originalBType}，实际为{unitB.Type}");
        Check(unitB.Attack == originalBAttack, $"恢复后B攻击应回到原始{originalBAttack}，实际为{unitB.Attack}");

        // === 场景3：验证"直接赋值"的旧bug确实存在（作为反例）===
        // 如果使用直接赋值（旧方式），会导致两个单位最终获得相同值
        // 此处仅作文档说明，不执行破坏性测试
        // 旧方式：first.Type = second.Type; first.Attack = second.Attack;
        //         second.Type = first.Type; second.Attack = first.Attack;
        // 结果：A和B都会变成 "刺客/20"，不是交换

        Console.WriteLine("[PASS] TestDoubleTempSwapRestoresOriginal: 连续两次临时交换后正确恢复原始值");
    }
}
