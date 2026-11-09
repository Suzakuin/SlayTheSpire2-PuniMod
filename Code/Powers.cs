using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace PuniMod;

public interface ISealUnlockListener
{
    Task AfterSealUnlocked(PlayerChoiceContext context,int seal,CardModel? source);
}

public abstract class PuniPower : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override string CustomPackedIconPath => Main.Res + "/images/powers/" + GetType().Name + ".png";
    public override string CustomBigIconPath => CustomPackedIconPath;
}

public sealed class SealProgressPower : PuniPower
{
    public static int Unlocked(Creature c) => Math.Min(7, c.GetPowerAmount<SealProgressPower>() / 3);
    public override List<(string,string)> Localization => new PowerLoc("七重封印·圣灵", "每 3 点圣灵依次解放一道封印，21 点全部解放。之后圣灵继续累积，每额外 7 点触发一次圣灵脉冲：对所有敌人造成 7 点固定伤害，获得 7 点格挡。", "当前圣灵：{Amount}。\n3：虚无，1 层无实体。\n6：元素，4 点力量。\n9：能量，2 能量并抽 2 张。\n12：生命，回复 20 生命并每回合回复 5。\n15：轮回，抵挡死亡并回复最大生命的 10%。\n18：永恒，每回合多 1 能量并抽 1 张。\n21：圣洁，清除减益、3 层人工制品、攻击伤害翻倍。\n28、35、42……：圣灵脉冲，对所有敌人造成 7 点无视格挡的固定伤害，并获得 7 点格挡；不受力量、攻击翻倍或敏捷加成。圣灵不会被消耗。");
    private static int PulsesAt(int spirit) => Math.Max(0,spirit-21)/7;
    public static async Task Advance(PlayerChoiceContext context, Creature owner, int amount, CardModel? source)
    {
        int old = owner.GetPowerAmount<SealProgressPower>();
        int gain = Math.Clamp(amount, 0, int.MaxValue - old);
        if (gain == 0) return;
        await PowerCmd.Apply<SealProgressPower>(context, owner, gain, owner, source);
        int next = owner.GetPowerAmount<SealProgressPower>();
        if(owner.GetPower<DivineBulwarkPower>() is {} bulwark)
            await bulwark.AfterSpiritGained(next-old);
        for (int seal = Math.Min(7,old / 3) + 1; seal <= Math.Min(7,next / 3); seal++)
        {
            switch (seal)
            {
                case 1: await PowerCmd.Apply<IntangiblePower>(context, owner, 1, owner, source); break;
                case 2: await PowerCmd.Apply<StrengthPower>(context, owner, 4, owner, source); break;
                case 3:
                    await PlayerCmd.GainEnergy(2, owner.Player!);
                    await CardPileCmd.Draw(context, 2, owner.Player!);
                    break;
                case 4:
                    await CreatureCmd.Heal(owner, 20);
                    await PowerCmd.Apply<LivingLightPower>(context, owner, 5, owner, source);
                    break;
                case 5: await PowerCmd.Apply<RebirthPower>(context, owner, 1, owner, source); break;
                case 6: await PowerCmd.Apply<SealEternalPower>(context, owner, 1, owner, source); break;
                case 7:
                    await Cleanse(owner);
                    await PowerCmd.Apply<ArtifactPower>(context, owner, 3, owner, source);
                    await PowerCmd.Apply<TrueFormPower>(context, owner, 1, owner, source);
                    break;
            }
            foreach(var listener in owner.Powers.OfType<ISealUnlockListener>().ToArray())
                await listener.AfterSealUnlocked(context,seal,source);
            if(owner.Player?.Relics.OfType<UnboundSealCore>().FirstOrDefault() is {} awakened)
                await awakened.AfterSealUnlocked(context,seal);
        }
        // Compare the committed Spirit snapshots: a large gain can cross several
        // pulse boundaries, while nested Spirit gains handle their own interval.
        int pulses=PulsesAt(next)-PulsesAt(old);
        for(int i=0;i<pulses&&!owner.IsDead;i++)
        {
            await CreatureCmd.GainBlock(owner,7,ValueProp.Unpowered,null);
            foreach(var enemy in owner.CombatState?.HittableEnemies.ToArray()??[])
                if(!enemy.IsDead)await CreatureCmd.Damage(context,enemy,7,ValueProp.Unblockable|ValueProp.Unpowered,owner,source,null);
        }
        if(owner.Player is {} player)
            foreach(var card in CardPile.Get(PileType.Hand,player)?.Cards.OfType<PuniCard>().ToArray()??[])
                MegaCrit.Sts2.Core.Nodes.Cards.NCard.FindOnTable(card,PileType.Hand)?.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
    }
    public static async Task Cleanse(Creature target)
    {
        foreach (var power in target.Powers.Where(p => p.TypeForCurrentAmount == PowerType.Debuff).ToArray())
            await PowerCmd.Remove(power);
    }
}

public sealed class LivingLightPower : PuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("生命封印", "每个回合开始时回复生命。", "你的回合开始时，回复 {Amount} 点生命。");
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player == Owner.Player && !Owner.IsDead) await CreatureCmd.Heal(Owner, Amount);
    }
}

public sealed class EternalPower : PuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("永恒封印", "每个回合开始时获得额外能量。", "你的回合开始时，获得 {Amount} 点能量。");
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if (player != Owner.Player || Owner.IsDead) return;
        await PlayerCmd.GainEnergy(Amount, player);
    }
}

public sealed class RebirthPower : PuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("轮回封印", "抵挡死亡并回复最大生命值的 10%。", "下 {Amount} 次受到致命伤害时，消耗 1 层轮回，回复最大生命值的 10%（向上取整，至少 1 点）。");
    public override bool ShouldDieLate(Creature creature) => creature != Owner || Amount <= 0;
    public override async Task AfterPreventingDeath(Creature creature)
    {
        if (creature != Owner || Amount <= 0) return;
        await PowerCmd.Decrement(this);
        await CreatureCmd.Heal(creature, Math.Max(1,Math.Ceiling(creature.MaxHp/10m)));
    }
}

public sealed class SealEternalPower : PuniPower
{
    public override List<(string,string)> Localization => new PowerLoc("永恒封印·苏醒","每回合获得额外能量并抽牌。","你的回合开始时，获得 {Amount} 点能量，抽 {Amount} 张牌。");
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext context, Player player)
    {
        if(player!=Owner.Player||Owner.IsDead)return;
        await PlayerCmd.GainEnergy(Amount,player);
        await CardPileCmd.Draw(context,Amount,player);
    }
}

public sealed class TrueFormPower : PuniPower
{
    public override PowerStackType StackType => PowerStackType.Single;
    public override List<(string,string)> Localization => new PowerLoc("圣灵真身", "攻击伤害翻倍。", "你造成的攻击伤害翻倍。可与圣光气叠加，不与自身或金卡显圣叠加。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => dealer == Owner && props.IsPoweredAttack() && !Owner.HasPower<ManifestedTrueFormPower>() ? 2m : 1m;
}

public sealed class ManifestedTrueFormPower : PuniPower
{
    public override PowerStackType StackType => PowerStackType.Single;
    public override List<(string,string)> Localization => new PowerLoc("圣灵真身·显圣","攻击伤害翻倍。","你造成的攻击伤害翻倍。不与自身或封印提供的增伤叠加，可与圣光气叠加。");
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? source, CardPlay? play)
        => dealer==Owner && props.IsPoweredAttack()?2m:1m;
}

public sealed class EnergyReflectionPower : PuniPower
{
    public override PowerStackType StackType => PowerStackType.Single;
    public override List<(string,string)> Localization => new PowerLoc("能量封印", "将受到的攻击伤害双倍返还。", "受到敌人的攻击并损失生命后，对攻击者造成损失生命值 2 倍的固定伤害。");
    public override async Task AfterDamageReceived(PlayerChoiceContext context, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? source)
    {
        if (target != Owner || dealer == null || dealer.Side == Owner.Side || dealer.IsDead || result.UnblockedDamage <= 0 || !props.IsPoweredAttack()) return;
        await CreatureCmd.Damage(context, dealer, result.UnblockedDamage*2, ValueProp.Unblockable | ValueProp.Unpowered, Owner);
    }
}

public sealed class RiftBurnPower : PuniPower
{
    public override PowerType Type => PowerType.Debuff;
    public override List<(string,string)> Localization => new PowerLoc("裂空圣痕", "每回合结束时受到固定伤害。", "回合结束时，受到 {Amount} 点无视格挡的固定伤害。");
    public override async Task BeforeSideTurnEndEarly(PlayerChoiceContext context, CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner) && !Owner.IsDead)
            await CreatureCmd.Damage(context, Owner, Amount, ValueProp.Unblockable | ValueProp.Unpowered, null!);
    }
}
