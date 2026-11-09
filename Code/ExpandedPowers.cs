using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace PuniMod;

public abstract class TurnPuniPower : PuniPower
{
    protected bool Triggered;
    protected int Plays;
    protected bool IsOwnerPlay(CardPlay play) => play.Player == Owner.Player;
    protected Task GainBlock(decimal amount) => CreatureCmd.GainBlock(Owner,amount,ValueProp.Unpowered,null);
    public override Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if(player==Owner.Player) { Triggered=false; Plays=0; }
        return Task.CompletedTask;
    }
}

public sealed class RadiantArmorPower : TurnPuniPower, IImprintListener
{
    public override List<(string,string)> Localization => new PowerLoc("光铠","刻印提供格挡。","每次触发刻印后，获得 {Amount} 点格挡。");
    public async Task AfterImprint(PlayerChoiceContext context,CardPlay play,PuniCard card,SealImprint imprint)
    { if(IsOwnerPlay(play))await GainBlock(Amount); }
}
public sealed class SealMeditationPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("守印冥想","首次技能额外推进封印。","每回合首次打出技能牌时，获得 {Amount} 点圣灵。");
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if(!IsOwnerPlay(play)||Triggered||play.Card.Type!=CardType.Skill)return;
        Triggered=true; await SealProgressPower.Advance(context,Owner,Amount,play.Card);
    }
}
public sealed class DawnRecoveryPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("晨光守护","首次损失生命后提供格挡。","每回合首次损失生命后，获得 {Amount} 点格挡。");
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? source)
    {
        if(Triggered||target!=Owner||result.UnblockedDamage<=0)return;
        Triggered=true; await GainBlock(Amount);
    }
}
public sealed class PrismMindPower : TurnPuniPower, IImprintListener
{
    public override List<(string,string)> Localization => new PowerLoc("棱光心流","每回合首次刻印提供抽牌。","每回合首次触发刻印后，抽 {Amount} 张牌。");
    public async Task AfterImprint(PlayerChoiceContext context,CardPlay play,PuniCard card,SealImprint imprint)
    {
        if(!IsOwnerPlay(play)||Triggered)return;
        Triggered=true;await CardPileCmd.Draw(context,Amount,Owner.Player!);
    }
}
public sealed class RiftMasteryPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("裂空精研","消耗卡牌时施加圣痕。","每当你消耗一张牌，随机一名敌人获得 {Amount} 层裂空圣痕。包含状态牌、诅咒和打出后自行消耗的牌。");
    public override async Task AfterCardExhausted(PlayerChoiceContext context,CardModel card,bool causedByEthereal)
    {
        if(card.Owner!=Owner.Player||Owner.IsDead)return;
        var enemies=Owner.CombatState!.HittableEnemies;
        if(enemies.Count==0)return;
        var enemy=Owner.Player!.RunState.Rng.CombatTargets.NextItem(enemies);
        if(enemy!=null)await PowerCmd.Apply<RiftBurnPower>(context,enemy,Amount,Owner,card);
    }
}
public sealed class PrismaticSkinPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("棱镜护身","消耗卡牌转化为格挡。","每当你消耗一张牌，获得 {Amount} 点格挡。若消耗的是状态牌或诅咒，额外获得 3 点格挡。");
    public override async Task AfterCardExhausted(PlayerChoiceContext context,CardModel card,bool causedByEthereal)
    { if(card.Owner==Owner.Player&&!Owner.IsDead)await GainBlock(Amount+(card.Type is CardType.Status or CardType.Curse?3:0)); }
}
public sealed class EchoingLightPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("光回响","首次单体攻击施加圣痕。","每回合首次打出单体攻击牌后，其目标获得 {Amount} 层裂空圣痕。");
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if(!IsOwnerPlay(play)||Triggered||play.Card.Type!=CardType.Attack||play.Card.TargetType!=TargetType.AnyEnemy)return;
        Triggered=true;
        if(play.Target is { IsDead:false } enemy)await PowerCmd.Apply<RiftBurnPower>(context,enemy,Amount,Owner,play.Card);
    }
}
public sealed class GatheringGlowPower : TurnPuniPower, IImprintListener
{
    public override List<(string,string)> Localization => new PowerLoc("圣印长鸣","每回合首次刻印额外重复。","每回合首次触发刻印时，额外重复该刻印效果 {Amount} 次。额外重复不会再次触发刻印联动。");
    public async Task AfterImprint(PlayerChoiceContext context,CardPlay play,PuniCard card,SealImprint imprint)
    {
        if(!IsOwnerPlay(play)||Triggered)return;
        Triggered=true;
        for(int i=0;i<Amount;i++)await card.RepeatImprint(context,play,imprint);
    }
}
public sealed class SolarHeartPower : TurnPuniPower, IImprintListener
{
    // Keep collection state for the whole combat, including power removal/replay.
    private static readonly ConditionalWeakTable<ICombatState,Dictionary<Creature,int>> _collections=new();
    public override List<(string,string)> Localization => new PowerLoc("烈阳之心","收集不同刻印获得力量。","本场战斗，每种封印刻印首次触发后，获得 {Amount} 点力量。七种分别计数，跨回合不重置。重复打出不叠加、不重置收集记录。");
    public async Task AfterImprint(PlayerChoiceContext context,CardPlay play,PuniCard card,SealImprint imprint)
    {
        if(!IsOwnerPlay(play)||Owner.IsDead||imprint.Seal<1||imprint.Seal>7)return;
        var combat=Owner.CombatState;if(combat==null)return;
        var collection=_collections.GetOrCreateValue(combat);
        collection.TryGetValue(Owner,out int triggered);
        int flag=1<<(imprint.Seal-1);
        if((triggered&flag)!=0)return;
        collection[Owner]=triggered|flag;
        await PowerCmd.Apply<StrengthPower>(context,Owner,Amount,Owner,play.Card);
    }
}
public sealed class SealSovereignPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("封印主宰","每次出牌额外推进封印。","每当你打出一张牌，额外获得 {Amount} 点圣灵。");
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if(!IsOwnerPlay(play))return;
        await SealProgressPower.Advance(context,Owner,Amount,play.Card);
    }
}
public sealed class CycleOfStarsPower : TurnPuniPower
{
    private bool _resolving;
    public override List<(string,string)> Localization => new PowerLoc("生息圣裁","实际回复生命时造成等量伤害。","每当你实际回复生命，对随机一名敌人造成实际回复量 {Amount} 倍的固定伤害。满血溢出不计伤害，不受力量或攻击伤害翻倍影响。");
    public async Task AfterActualHealing(int healed)
    {
        if(healed<=0||_resolving||Owner.IsDead||!CombatManager.Instance.IsInProgress)return;
        var enemies=Owner.CombatState?.HittableEnemies;
        if(enemies==null||enemies.Count==0)return;
        var enemy=Owner.Player!.RunState.Rng.CombatTargets.NextItem(enemies);
        if(enemy==null)return;
        _resolving=true;
        try { await CreatureCmd.Damage(new BlockingPlayerChoiceContext(),enemy,healed*Amount,ValueProp.Unpowered|ValueProp.Unblockable,Owner); }
        finally {_resolving=false;}
    }
}
public sealed class DivineBulwarkPower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("神圣壁垒","获得圣灵时提供格挡。","每实际获得 1 点圣灵，获得 {Amount} 点格挡。一次获得多点时，按实际获得量计算。");
    public Task AfterSpiritGained(int gained)
        => gained>0&&!Owner.IsDead?GainBlock(gained*Amount):Task.CompletedTask;
}
public sealed class PowerResonancePower : TurnPuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("神辉回路","打出其他能力时抽牌并回能。","每当你打出除神辉回路以外的能力牌，获得 {Amount} 能量并抽 {Amount} 张牌。");
    public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay play)
    {
        if(!IsOwnerPlay(play)||play.Card.Type!=CardType.Power||play.Card is PowerResonance)return;
        await PlayerCmd.GainEnergy(Amount,Owner.Player!); await CardPileCmd.Draw(context,Amount,Owner.Player!);
    }
}

public sealed class HolyThornsPower : TurnPuniPower, ISealUnlockListener
{
    public override List<(string,string)> Localization => new PowerLoc("圣芒","解封时对所有敌人施加圣痕。","每新解封一道封印，给予所有敌人 {Amount} 层裂空圣痕。不追溯已解封部分，21点后的圣灵脉冲不触发。");
    public async Task AfterSealUnlocked(PlayerChoiceContext context,int seal,CardModel? source)
    {
        foreach(var target in Owner.CombatState!.HittableEnemies.ToArray())
            await PowerCmd.Apply<RiftBurnPower>(context,target,Amount,Owner,source);
    }
}

public sealed class LifeSealPower : TurnPuniPower, ISealUnlockListener
{
    public override string CustomPackedIconPath=>Main.Res+"/images/powers/LivingLightPower.png";
    public override List<(string,string)> Localization => new PowerLoc("生命","解封时获得能量并抽牌。","每新解封一道封印，获得 {Amount} 点能量，抽 {Amount} 张牌。不追溯已解封部分，21点后的圣灵脉冲不触发。");
    public async Task AfterSealUnlocked(PlayerChoiceContext context,int seal,CardModel? source)
    {
        await PlayerCmd.GainEnergy(Amount,Owner.Player!);
        await CardPileCmd.Draw(context,Amount,Owner.Player!);
    }
}


