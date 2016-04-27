using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;
using Color = System.Drawing.Color;

namespace Simple_Twitch
{
    internal static class Program
    {
        private static Menu _menu;
        private static Menu _combo, _harass, _laneClear, _jungleClear, _drawings, _misc, _ewhitelist;
        public static Spell.Active ArgsQ, ArgsE, ArgsR;
        public static Spell.Skillshot ArgsW;

        private static int _passivemod;

        public static Dictionary<Func<int, bool>, Action> PassiveLvlReq = new Dictionary<Func<int, bool>, Action>
        {
            { x => x < 5 ,    () => _passivemod = 0 },
            { x => x < 9 ,    () => _passivemod = 1 },
            { x => x < 13 ,    () => _passivemod = 2 },
            { x => x < 18 ,    () => _passivemod = 3 },
            { x => x < 19 ,    () => _passivemod = 4 }
        };
        
        public static ColorPicker[] DrawingColors { get; set; }

        private enum Modes
        {
            Combo,
            Harass,
            LastHit,
            LaneClear,
            JungleClear,
            Flee
        }

        public static Text InfoText { get; set; }

        private static readonly Item Ghostblade = new Item(ItemId.Youmuus_Ghostblade);
        private static readonly Item Botrk = new Item(ItemId.Blade_of_the_Ruined_King, 550f);

        private static readonly int[] ArgsPassiveDmg = { 2, 3, 4, 5, 6 };
        private static readonly int[] ArgsEBaseDmg = { 0, 20, 35, 50, 65, 80 };
        private static readonly int[] ArgsEDmgPerStack = { 0, 15, 20, 25, 30, 35 };
        private static readonly int[] ArgsInvisTime = {0, 4, 5, 6, 7, 8};
        private const float ArgsEAdMod = .25f;
        private const float ArgsEApMod = .2f;
        private const int ScanRange = 2000;

        private static bool _aacancelpossible;
        private static bool _canusebotrk;
        
        public static bool QInCombo { get { return _combo["ComboQ"].Cast<CheckBox>().CurrentValue; } }
        public static int MinEnemiesForQ { get { return _combo["MinEnemiesForQ"].Cast<Slider>().CurrentValue; } }
        public static bool WInCombo { get { return _combo["ComboW"].Cast<CheckBox>().CurrentValue; } }
        public static bool UseeInCombo { get { return _combo["UseeInCombo"].Cast<CheckBox>().CurrentValue; } }
        public static int EInCombo { get { return _combo["ComboE"].Cast<Slider>().CurrentValue; } }
        public static bool UseRInCombo { get { return _combo["UseRInCombo"].Cast<CheckBox>().CurrentValue; } }
        public static bool UseBotrk { get { return _combo["useBotrk"].Cast<CheckBox>().CurrentValue; } }
        public static int MinEnemiesForR { get { return _combo["MinEnemiesForR"].Cast<Slider>().CurrentValue; } }

        public static bool HarassW { get { return _harass["HarassW"].Cast<CheckBox>().CurrentValue; } }

        public static bool LaneClearW { get { return _laneClear["LaneClearW"].Cast<CheckBox>().CurrentValue; } }
        public static int LaneClearManaPercentW { get { return _laneClear["LaneClearManaPercentW"].Cast<Slider>().CurrentValue; } }
        public static bool LaneClearE { get { return _laneClear["LaneClearE"].Cast<CheckBox>().CurrentValue; } }
        public static bool LaneClearEKillable { get { return _laneClear["LaneClearEKillable"].Cast<CheckBox>().CurrentValue; } }
        public static int LaneClearManaPercentE { get { return _laneClear["LaneClearManaPercentE"].Cast<Slider>().CurrentValue; } }
        public static int LaneClearEMin { get { return _laneClear["LaneClearEMin"].Cast<Slider>().CurrentValue; } }

        public static bool JangleSteal { get { return _jungleClear["JungleSteal"].Cast<CheckBox>().CurrentValue; } }
        public static bool EBaronDrake { get { return _jungleClear["EBaronDrake"].Cast<CheckBox>().CurrentValue; } }

        public static bool DrawW { get { return _drawings["Wrange"].Cast<CheckBox>().CurrentValue; } }
        public static bool DrawE { get { return _drawings["Erange"].Cast<CheckBox>().CurrentValue; } }
        public static bool DrawQTime { get { return _drawings["DrawQTime"].Cast<CheckBox>().CurrentValue; } }
        public static bool DrawEOnEnemies { get { return _drawings["EonEnemies"].Cast<CheckBox>().CurrentValue; } }
        public static bool DrawEOnCamps { get { return _drawings["EonCamps"].Cast<CheckBox>().CurrentValue; } }
        public static bool DrawTextNearHpBar { get { return _drawings["DrawTextNearHPBar"].Cast<CheckBox>().CurrentValue; } }

        public static bool EKillsteal { get { return _misc["EKillsteal"].Cast<CheckBox>().CurrentValue; } }
        public static bool QKill { get { return _misc["QKill"].Cast<CheckBox>().CurrentValue; } }
        public static bool CalcPassive { get { return _misc["CalcPassive"].Cast<CheckBox>().CurrentValue; } }
        public static bool GhostbladeAfterQ { get { return _misc["GhostbladeAfterQ"].Cast<CheckBox>().CurrentValue; } }
        public static bool GhostbladeAfterR { get { return _misc["GhostbladeAfterR"].Cast<CheckBox>().CurrentValue; } }
        public static bool Minionunkillable { get { return _misc["minionunkillable"].Cast<CheckBox>().CurrentValue; } }
        public static bool ERange { get { return _misc["Combo.E.Range"].Cast<CheckBox>().CurrentValue; } }
        public static int ERangeStacks { get { return _misc["eRangeStacks"].Cast<Slider>().CurrentValue; } }
        public static bool EBeforeDeath { get { return _misc["eBeforeDeath"].Cast<CheckBox>().CurrentValue; } }
        public static int EHealthPercent { get { return _misc["eHealthPercent"].Cast<Slider>().CurrentValue; } }

        private static void Main()
        {
            Loading.OnLoadingComplete += Event_OnLoadingComplete;
        }

        private static void Event_OnLoadingComplete(EventArgs args)
        {
            if (Player.Instance.ChampionName != "Twitch")
                return;

            try
            {
                ArgsQ = new Spell.Active(SpellSlot.Q);
                ArgsW = new Spell.Skillshot(SpellSlot.W, 925, SkillShotType.Circular, 250, 1400, 275)
                {
                    AllowedCollisionCount = int.MaxValue
                };
                ArgsE = new Spell.Active(SpellSlot.E, 1200);
                ArgsR = new Spell.Active(SpellSlot.R, 850);

                DrawingColors = new ColorPicker[3];

                CreateMenu();

                Game.OnTick += Event_Game_OnTick;
                Game.OnNotify += Game_OnNotify;
                Drawing.OnDraw += Event_OnDraw;
                Obj_AI_Base.OnProcessSpellCast += Event_OnProcessSpellCast;
                Obj_AI_Base.OnBuffGain += Event_OnBuffGain;
                Orbwalker.OnPostAttack += Event_OnPostAttack;
                Orbwalker.OnUnkillableMinion += Event_OnUnkillableMinion;
                DamageIndicator.DamageToUnit = GetFinalEDamage;
                Drawing.OnEndScene += Drawing_OnEndScene;

                InfoText = new Text("", new Font("calibri", 15, FontStyle.Regular));

                Console.WriteLine("[SimpleTwitch] Loading successfull.");

                Chat.Print("<font color=\"#88F054\"><b>[Simple Twitch]</b></font> Addon successfully loaded.");

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void Event_Game_OnTick(EventArgs args)
        {
            if (Player.Instance.IsDead)
            {
                return;
            }

            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo))
            {
                Execute(Modes.Combo);
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Flee))
            {
                Execute(Modes.Flee);
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass))
            {
                Execute(Modes.Harass);
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.JungleClear))
            {
                Execute(Modes.JungleClear);
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear))
            {
                Execute(Modes.LaneClear);
            }
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LastHit))
            {
                Execute(Modes.LastHit);
            }

            if (!ArgsE.IsReady()) return;

            foreach (var t in EntityManager.Heroes.Enemies.Where(
                enemy => enemy.IsValid && enemy.HasBuff("twitchdeadlyvenom") && enemy.IsValidTarget(ArgsE.Range) && CanUseEOnEnemy(enemy.BaseSkinName)))
            {
                if (ERange && CountEStacks(t) >= ERangeStacks &&
                    t.Distance(Player.Instance.ServerPosition) >= ArgsE.Range - 200)
                {
                    ArgsE.Cast();
                }
                if (EBeforeDeath && Player.Instance.HealthPercent <= EHealthPercent)
                {
                    ArgsE.Cast();
                }
                if (EKillsteal && IsEnemyKillable(t))
                {
                    ArgsE.Cast();
                }
                if (PassiveTime(t) < 0.5f && CountEStacks(t) >= 3)
                {
                    ArgsE.Cast();
                }
            }

            if (!JangleSteal) return;

            var monster =
                EntityManager.MinionsAndMonsters.GetJungleMonsters()
                    .FirstOrDefault(
                        a =>
                            a.IsValidTarget(ArgsE.Range) && a.HasBuff("twitchdeadlyvenom") && a.Health <= CalculateE_DmgOnUnit(a));

            if (monster == null) return;

            if ((monster.BaseSkinName == "SRU_Baron" || monster.BaseSkinName == "SRU_Dragon") && EBaronDrake)
            {
                ArgsE.Cast();
            }
            if ((monster.BaseSkinName == "SRU_Red" || monster.BaseSkinName == "SRU_Blue") && JangleSteal)
            {
                ArgsE.Cast();
            }
        }

        private static void Event_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (GhostbladeAfterR && sender.IsMe && Ghostblade.IsOwned() && args.Slot == SpellSlot.R && Ghostblade.IsReady())
            {
                Ghostblade.Cast();
            }
        }

        private static void Event_OnUnkillableMinion(Obj_AI_Base unit, Orbwalker.UnkillableMinionArgs args)
        {
            if (!Minionunkillable || !ArgsE.IsReady() || !unit.HasBuff("TwitchDeadlyVenom") || CountEnemiesInRange(ArgsE.Range) > 0)
            {
                return;
            }
            ArgsE.Cast();
        }
        
        private static void Event_OnPostAttack(AttackableUnit target, EventArgs args)
        {
            if (QInCombo && Orbwalker.ActiveModesFlags == Orbwalker.ActiveModes.Combo && ArgsQ.IsReady() && 
                target.Type == GameObjectType.AIHeroClient && CountEnemiesInRange(ScanRange) >= MinEnemiesForQ)
            {
                ArgsQ.Cast();
            }
            _aacancelpossible = true;
            Core.DelayAction(() => _aacancelpossible = false, 100);
        }

        private static void Game_OnNotify(GameNotifyEventArgs args)
        {
            if (!QKill || args.EventId != GameEventId.OnChampionKill || args.NetworkId != Player.Instance.NetworkId)
            {
                return;
            }

            var delay = new Random();

            Core.DelayAction(() =>
            {
                if (CountEnemiesInRange(ScanRange) > 0 && ArgsQ.IsReady())
                {
                    ArgsQ.Cast();
                }

            }, 
            delay.Next(100, 250));
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            if (DrawQTime && ArgsQ.IsLearned || ArgsR.IsLearned)
            {
                var qbuff = Player.Instance.GetBuff("TwitchHideInShadows");
                var rbuff = Player.Instance.GetBuff("TwitchUlt");
                if (qbuff != null)
                {
                    var percentage = 100*Math.Max(0, qbuff.EndTime - Game.Time)/ArgsInvisTime[ArgsQ.Level];

                    var g = Math.Max(0, 255f/100f*percentage);
                    var r = Math.Max(0, 255 - g);

                    var color = Color.FromArgb((int) r, (int) g, 0);

                    InfoText.Color = color;
                    InfoText.X = (int) Drawing.WorldToScreen(Player.Instance.Position).X;
                    InfoText.Y = (int) Drawing.WorldToScreen(Player.Instance.Position).Y;
                    InfoText.TextValue = "Q expiry time : " + Math.Max(0, qbuff.EndTime - Game.Time).ToString("F1");
                    InfoText.Draw();
                }
                if (rbuff != null)
                {
                    var percentage = 100 * Math.Max(0, rbuff.EndTime - Game.Time) / 5;

                    var g = Math.Max(0, 255f / 100f * percentage);
                    var r = Math.Max(0, 255 - g);

                    var color = Color.FromArgb((int)r, (int)g, 0);

                    InfoText.Color = color;
                    InfoText.X = (int)Drawing.WorldToScreen(Player.Instance.Position).X;
                    InfoText.Y = (int)Drawing.WorldToScreen(Player.Instance.Position).Y;
                    InfoText.TextValue = "\nR expiry time : " + Math.Max(0, rbuff.EndTime - Game.Time).ToString("F1");
                    InfoText.Draw();
                }
            }


            if (!DrawTextNearHpBar) return;

            foreach (
                var enemy in
                    EntityManager.Heroes.Enemies.Where(
                        a => !a.IsDead && a.HasBuff("twitchdeadlyvenom") && a.IsHPBarRendered && a.IsValidTarget(ArgsE.Range)))
            {
                var stacks = CountEStacks(enemy);

                if (ArgsE.IsReady())
                {
                    var calc = GetFinalEDamage(enemy)/GetTotalHealth(enemy)*100;

                    InfoText.Color = Color.DeepSkyBlue;
                    InfoText.X = (int) (enemy.HPBarPosition.X + 140);
                    InfoText.Y = (int) enemy.HPBarPosition.Y;
                    InfoText.TextValue = "" + Math.Min(calc, 100).ToString("F1") + " %";
                    InfoText.Draw();
                }

                if (stacks < 1) continue;
                for (var i = 0; i < 6; i++)
                {
                    Drawing.DrawLine(enemy.HPBarPosition.X + i*20, enemy.HPBarPosition.Y - 30, enemy.HPBarPosition.X + i * 20 + 20,
                        enemy.HPBarPosition.Y - 30, 10, stacks <= i ? Color.DarkGray : Color.MediumVioletRed);
                }
            }
        }

        private static void Event_OnBuffGain(Obj_AI_Base sender, Obj_AI_BaseBuffGainEventArgs args)
        {
            if (!sender.IsMe || args.Buff.Name != "twitchhideinshadowsbuff" ||
                CountEnemiesInRange((uint) Player.Instance.GetAutoAttackRange(sender) + 200) < 1) return;

            if (GhostbladeAfterQ && Ghostblade.IsOwned() && Ghostblade.IsReady())
            {
                Ghostblade.Cast();
            }

            if (!UseBotrk || !Botrk.IsOwned()) return;
            _canusebotrk = true;
            Core.DelayAction(() => _canusebotrk = false, 2000);
        }

        private static void Event_OnDraw(EventArgs args)
        {
            if (DrawW && ArgsW.IsLearned)
            {
                Circle.Draw(new ColorBGRA(DrawingColors[0].CurrentValue.R, DrawingColors[0].CurrentValue.G,
                    DrawingColors[0].CurrentValue.B, DrawingColors[0].CurrentValue.A), ArgsW.Range, Player.Instance.Position);
                
            }

            if (DrawE && ArgsE.IsLearned)
            {
                Circle.Draw(new ColorBGRA(DrawingColors[1].CurrentValue.R, DrawingColors[1].CurrentValue.G,
                    DrawingColors[1].CurrentValue.B, DrawingColors[1].CurrentValue.A), ArgsE.Range, Player.Instance.Position);
            }
        }

        private static void Execute(Modes args)
        {
            switch (args)
            {
                case Modes.Combo:
                {
                    var target = TargetSelector.GetTarget(ArgsE.Range, DamageType.Physical);

                    if (target == null) return;

                    if (_canusebotrk && Botrk.IsReady() && Botrk.IsInRange(target) && target.IsValidTarget(Botrk.Range))
                    {
                        Botrk.Cast(target);
                        _canusebotrk = false;
                    }

                    if (WInCombo && _combo["WMode"].Cast<ComboBox>().CurrentValue == 1 && _aacancelpossible && ArgsW.IsReady() && ArgsW.IsInRange(target) &&
                        Player.Instance.Mana > 120)
                    {
                        var wPrediction = ArgsW.GetPrediction(target);
                        if (wPrediction.HitChance >= HitChance.Medium)
                        {
                            ArgsW.Cast(wPrediction.CastPosition);
                        }
                    } else if (WInCombo && _combo["WMode"].Cast<ComboBox>().CurrentValue == 0 && ArgsW.IsReady() &&
                               ArgsW.IsInRange(target) &&
                               Player.Instance.Mana > 120)
                    {
                        var wPrediction = ArgsW.GetPrediction(target);
                        if (wPrediction.HitChance >= HitChance.Medium)
                        {
                            ArgsW.Cast(wPrediction.CastPosition);
                        }
                    }
                    if (UseeInCombo && ArgsE.IsReady() && ArgsE.IsInRange(target) && CountEStacks(target) >= EInCombo && CanUseEOnEnemy(target.BaseSkinName))
                    {
                        ArgsE.Cast();
                    }

                    if (UseRInCombo && CountEnemiesInRange(ArgsR.Range-150) >= MinEnemiesForR && ArgsR.IsReady())
                    {
                        ArgsR.Cast();
                    }

                    break;
                }
                case Modes.Flee:
                {
                    if (ArgsQ.IsReady())
                        ArgsQ.Cast();

                    break;
                }
                case Modes.Harass:
                {
                    if (HarassW && ArgsW.IsReady())
                    {
                        var t =
                            EntityManager.Heroes.Enemies.OrderByDescending(a => a.TotalAttackDamage)
                                .FirstOrDefault(a => a.IsValidTarget(ArgsW.Range));

                        if (t == null)
                        {
                            return;
                        }

                        var wPrediction = ArgsW.GetPrediction(t);

                        if (wPrediction.HitChance >= HitChance.Medium)
                        {
                            ArgsW.Cast(wPrediction.CastPosition);
                        }
                    }
                    break;
                }
                case Modes.JungleClear:
                {
                    if (!ArgsW.IsReady())
                        return;

                    var minionw = EntityManager.MinionsAndMonsters.GetJungleMonsters(Player.Instance.ServerPosition,
                        ArgsW.Range);
                    var wfarm = EntityManager.MinionsAndMonsters.GetCircularFarmLocation(minionw, ArgsW.Width,
                        (int) ArgsW.Range);

                    ArgsW.Cast(wfarm.CastPosition);

                    break;
                }
                case Modes.LaneClear:
                {
                    if (LaneClearW && ArgsW.IsReady() && Player.Instance.ManaPercent >= LaneClearManaPercentW)
                    {
                        var minionw = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                            Player.Instance.Position, ArgsW.Range);
                    
                        var wfarm = EntityManager.MinionsAndMonsters.GetCircularFarmLocation(minionw, ArgsW.Width,
                            (int) ArgsW.Range);

                        if (wfarm.HitNumber >= 3)
                        {
                            ArgsW.Cast(wfarm.CastPosition);
                        }
                    }
                    if (LaneClearE && ArgsE.IsReady() && Player.Instance.ManaPercent >= LaneClearManaPercentE)
                    {
                        var minione = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                            Player.Instance.Position, ArgsE.Range).Where(
                                a => a.IsValidTarget() && a.HasBuff("twitchdeadlyvenom"));

                        var objAiMinions = minione as IList<Obj_AI_Minion> ?? minione.ToList();
                        if (objAiMinions.Count >= LaneClearEMin && !LaneClearEKillable)
                        {
                            ArgsE.Cast();
                            break;
                        }
                        if (LaneClearEKillable && objAiMinions.Count(a => a.Health <= CalculateE_DmgOnUnit(a)) >= LaneClearEMin)
                        {
                            ArgsE.Cast();
                        }
                    }
                    break;
                }
                case Modes.LastHit:
                {
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(args), args, null);
            }
        }

        private static bool IsEnemyKillable(Obj_AI_Base target)
        {
            if (target == null || !target.IsValidTarget(ArgsE.Range) || !target.HasBuff("TwitchDeadlyVenom"))
            {
                return false;
            }

            var hero = target as AIHeroClient;

            if (hero == null || IsEnemyUnkillable(hero) || HasSpellShield(hero))
            {
                return false;
            }

            if (hero.ChampionName != "Blitzcrank")
                return GetFinalEDamage(target) >= GetTotalHealth(target);

            if (!hero.HasBuff("BlitzcrankManaBarrierCD") && !hero.HasBuff("ManaBarrier"))
            {
                return GetFinalEDamage(target) > (target.GetTotalHealth() + (hero.Mana/2));
            }

            if (hero.HasBuff("ManaBarrier") && !(hero.AllShield > 0))
            {
                return false;
            }

            return GetFinalEDamage(target) >= GetTotalHealth(target);
        }

        private static bool HasSpellShield(AIHeroClient target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "bansheesveil"))
            {
                return true;
            }
            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "SivirE"))
            {
                return true;
            }
            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "NocturneW"))
            {
                return true;
            }
            return target.HasBuffOfType(BuffType.SpellShield) || target.HasBuffOfType(BuffType.SpellImmunity);
        }

        private static bool IsEnemyUnkillable(Obj_AI_Base target)
        {
            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "UndyingRage"))
            {
                return true;
            }

            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "ChronoShift"))
            {
                return true;
            }

            if (target.Buffs.Any(b => b.IsValid() && b.DisplayName == "JudicatorIntervention"))
            {
                return true;
            }

            return target.Buffs.Any(b => b.IsValid() && b.DisplayName == "kindredrnodeathbuff") || target.HasBuffOfType(BuffType.Invulnerability);
        }

        public static float GetFinalEDamage(Obj_AI_Base target)
        {
            if (!ArgsE.IsReady() || !target.HasBuff("TwitchDeadlyVenom")) return 0f;

            var damage = CalculateE_DmgOnUnit(target);

            if (target.Name.Contains("Baron"))
            {
                damage = Player.Instance.HasBuff("barontarget") ? damage*0.5f : damage;
            }

            else if (target.Name.Contains("Dragon"))
            {
                damage = Player.Instance.HasBuff("s5test_dragonslayerbuff") ? damage*(1 - (.07f*Player.Instance.GetBuffCount("s5test_dragonslayerbuff"))) : damage;
            }

            if (Player.Instance.HasBuff("summonerexhaust"))
            {
                damage = damage*0.6f;
            }

            if (target.HasBuff("FerociousHowl"))
            {
                damage = damage*0.7f;
            }

            return damage;
        }
        private static float CalculateE_DmgOnUnit(Obj_AI_Base target)
        {
            if (!ArgsE.IsReady() || !target.HasBuff("TwitchDeadlyVenom")) return 0.0f;

            var currentstacks = CountEStacks(target);

            var dmgg = Player.Instance.CalculateDamageOnUnit(target, DamageType.Physical,
                ArgsEBaseDmg[ArgsE.Level] +
                currentstacks *
                (Player.Instance.FlatMagicDamageMod * ArgsEApMod + Player.Instance.FlatPhysicalDamageMod * ArgsEAdMod +
                 ArgsEDmgPerStack[ArgsE.Level])
                );

            if (!CalcPassive || target.Type == GameObjectType.obj_AI_Minion)
            {
                return dmgg;
            }

            PassiveLvlReq.First(sw => sw.Key(Player.Instance.Level)).Value();
            dmgg += ArgsPassiveDmg[_passivemod] * currentstacks * 6;

            return dmgg;
        }

        private static int CountEStacks(Obj_AI_Base unit)
        {
            if (unit.IsDead || !unit.IsEnemy || unit.Type != GameObjectType.AIHeroClient && unit.Type != GameObjectType.obj_AI_Minion)
            {
                return 0;
            }

            var index = ObjectManager.Get<Obj_GeneralParticleEmitter>().ToList().Where(e => e.Name.Contains("twitch_poison_counter") && 
            e.Position.Distance(unit.ServerPosition) <= (unit.Type == GameObjectType.obj_AI_Minion ? 65 : 175));

            var stacks = 0;

            foreach (var x in index)
            {
                switch (x.Name)
                {
                    case "twitch_poison_counter_01.troy":
                        stacks = 1;
                        break;
                    case "twitch_poison_counter_02.troy":
                        stacks = 2;
                        break;
                    case "twitch_poison_counter_03.troy":
                        stacks = 3;
                        break;
                    case "twitch_poison_counter_04.troy":
                        stacks = 4;
                        break;
                    case "twitch_poison_counter_05.troy":
                        stacks = 5;
                        break;
                    case "twitch_poison_counter_06.troy":
                        stacks = 6;
                        break;
                    default:
                        stacks = 0;
                        break;
                }
            }
            return stacks;
        }

        private static int CountEnemiesInRange(float range)
        {
            return EntityManager.Heroes.Enemies.Count(enemy => enemy.IsValidTarget(range) && !enemy.IsDead && !enemy.IsZombie);
        }

        public static float PassiveTime(Obj_AI_Base target)
        {
            if (target.HasBuff("twitchdeadlyvenom"))
            {
                return Math.Max(0, target.GetBuff("twitchdeadlyvenom").EndTime) - Game.Time;
            }
            return 0;
        }

        public static float GetTotalHealth(this Obj_AI_Base target)
        {
            return target.Health + target.AllShield + target.AttackShield + target.MagicShield + (target.HPRegenRate*2);
        }

        public static bool CanUseEOnEnemy(string championName)
        {
            return _ewhitelist["Use.E.On" + championName].Cast<CheckBox>().CurrentValue;
        }

        private static void CreateMenu()
        {
            _menu = MainMenu.AddMenu("Simple Twitch", "stwitch");
            _menu.AddLabel("Just a simple twitch addon.");

            _combo = _menu.AddSubMenu("Combo");
            _combo.AddGroupLabel("Combo mode settings");
            _combo.Add("ComboQ", new CheckBox("Use Q."));
            _combo.Add("MinEnemiesForQ", new Slider("Use Q when {0} enemy/es is/are nearby.", 3, 1, 5));
            _combo.Add("ComboW", new CheckBox("Use W."));
            _combo.Add("WMode", new ComboBox("W Mode", new[] {"Always", "Only after AutoAttack"}));
            _combo.AddSeparator(15);
            _combo.Add("UseeInCombo", new CheckBox("Use E."));
            _combo.Add("ComboE", new Slider("Use E on enemy with {0} venom stacks.", 6, 1, 6));
            _combo.AddSeparator();
            _combo.Add("UseRInCombo", new CheckBox("Use R."));
            _combo.Add("MinEnemiesForR", new Slider("Use R when {0} enemy/es is/are inside R range.", 3, 1, 5));
            _combo.Add("useBotrk", new CheckBox("Use Botrk after Q ends."));

            _harass = _menu.AddSubMenu("Harass");
            _harass.AddGroupLabel("Harass");
            _harass.Add("HarassW", new CheckBox("Use W"));

            _laneClear = _menu.AddSubMenu("LaneClear");
            _laneClear.AddGroupLabel("LaneClear");
            _laneClear.Add("LaneClearW", new CheckBox("Use W"));
            _laneClear.Add("LaneClearManaPercentW", new Slider("Minimal mana percent to use W", 70, 1));
            _laneClear.Add("LaneClearE", new CheckBox("Use E"));
            _laneClear.Add("LaneClearEKillable", new CheckBox("Use E only if minions are killable with E", false));
            _laneClear.Add("LaneClearManaPercentE", new Slider("Minimal mana percent to use E", 70, 1));
            _laneClear.Add("LaneClearEMin", new Slider("Minimal minions hit to use E", 3, 1, 12));

            _jungleClear = _menu.AddSubMenu("JungleClear");
            _jungleClear.AddGroupLabel("JungleClear");
            _jungleClear.Add("JungleSteal", new CheckBox("Steal red / blue with E"));
            _jungleClear.Add("EBaronDrake", new CheckBox("Use E to steal dragon or baron"));

            _drawings = _menu.AddSubMenu("Drawings");
            _drawings.AddGroupLabel("Drawing.");
            _drawings.Add("DrawQTime", new CheckBox("Draw Q and R expiry time"));
            _drawings.Add("Wrange", new CheckBox("Draw W range"));
            _drawings.Add("Erange", new CheckBox("Draw E range"));
            _drawings.Add("EonEnemies", new CheckBox("Draw E damage on enemies"));
            _drawings.Add("EonCamps", new CheckBox("Draw E damage on jungle camps"));
            _drawings.Add("DrawTextNearHPBar", new CheckBox("Draw Info near enemy HP Bar"));

            DrawingColors[0] = _drawings.Add("WColor", new ColorPicker("0", Color.Coral, "W range drawing color", 80));
            _drawings.AddSeparator();
            DrawingColors[1] = _drawings.Add("EColor", new ColorPicker("1", Color.FromArgb(255, 212,66,243), "E range drawing color", 80));
            _drawings.AddSeparator();
            DrawingColors[2] = _drawings.Add("EColorHPBar", new ColorPicker("2", Color.FromArgb(153,255,149,62), "E drawing color on enemies", 80));
            _drawings.AddSeparator();
            
            _misc = _menu.AddSubMenu("Misc");
            _misc.AddGroupLabel("Misc.");

            _misc.AddSeparator();
            _misc.Add("EKillsteal", new CheckBox("Use E to killsteal"));
            _misc.Add("QKill", new CheckBox("Cast Q after kill"));
            _misc.Add("CalcPassive", new CheckBox("Calcualte passive damage over time", false));
            _misc.Add("GhostbladeAfterQ", new CheckBox("Use Youmuu's Ghostblade after Q ends"));
            _misc.Add("GhostbladeAfterR", new CheckBox("Use Youmuu's Ghostblade before R"));
            _misc.Add("minionunkillable", new CheckBox("Use E to kill unkillable by orbwalker minions", false));
            _misc.AddSeparator();
            _misc.Add("Combo.E.Range", new CheckBox("Use E before enemy leaves the range of E."));
            _misc.Add("eRangeStacks", new Slider("Minimal venon stacks", 4, 1, 6));
            _misc.Add("eBeforeDeath", new CheckBox("Use E before death."));
            _misc.Add("eHealthPercent", new Slider("Use E below {0}% health", 10, 1));

            _ewhitelist = _menu.AddSubMenu("E Whitelist");
            _ewhitelist.AddGroupLabel("E Whitelist");

            foreach (var enemy in EntityManager.Heroes.Enemies)
            {
                _ewhitelist.Add("Use.E.On" + enemy.BaseSkinName, new CheckBox(enemy.BaseSkinName));
            }

        }
    }
}