using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Utils;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Powers;

namespace PuniMod;

public record SealImprint(int Seal,string Effect,int Amount,int Upgrade,string Text)
{
    public static readonly string[] Names=["","虚无","元素","能量","生命","轮回","永恒","圣洁"];
    public int Value(bool upgraded)=>Amount+(upgraded?Upgrade:0);
    public string Describe(bool upgraded)
    {
        return Text.Replace("{n}",Value(upgraded).ToString());
    }
}

public interface IImprintListener
{
    Task AfterImprint(PlayerChoiceContext context,CardPlay play,PuniCard card,SealImprint imprint);
}

public abstract partial class PuniCard
{
    private int _imprintVolleyHits;
    private bool _resolvingEdict;
    private bool EdictPreviewActive=>_resolvingEdict||(IsMutable&&Spec.Imprints is {Length:>0}&&Owner?.Creature.GetPowerAmount<TemporaryEdictPower>()>0);
    public int ActiveSealCount => IsMutable && Owner?.Creature is {} creature ? SealProgressPower.Unlocked(creature) : 0;
    public string ImprintText(SealImprint imprint)
    {
        bool inCombat=IsMutable&&CombatState!=null;
        bool active=ActiveSealCount>=imprint.Seal||EdictPreviewActive||_forceImprint;
        string colour=inCombat?(active?"gold":"color=#808893"):"gold";
        string status=inCombat?(_forceImprint?"加速激活":EdictPreviewActive?"敕令激活":active?"已激活":"未解封"):"需 "+imprint.Seal*3+" 圣灵";
        string close=colour=="gold"?"gold":"color";
        int index=Array.IndexOf(Spec.Imprints!,imprint);
        string effect=imprint.Text.Replace("{n}",DynamicVars["ImprintValue"+index].ToHighlightedString(false));
        return $"[{colour}]刻印·{SealImprint.Names[imprint.Seal]}（{status}）：{effect}[/{close}]";
    }
    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        if(Spec.HasX&&DynamicVars["Magic"].WasJustUpgraded)description.Add("XSpirit","[green]X+1[/green]");
        var imprints=Spec.Imprints??[];
        for(int i=0;i<imprints.Length;i++)description.Add("Imprint"+i,ImprintText(imprints[i]));
    }
    protected override async Task OnPlay(PlayerChoiceContext context,CardPlay play)
    {
        // Lock activation before base effects or the starter relic advance Spirit.
        int seals=ActiveSealCount;
        var edict=Spec.Imprints is {Length:>0}?Owner.Creature.GetPower<TemporaryEdictPower>():null;
        bool boosted=edict?.Amount>0;
        bool previous=_resolvingEdict;
        var acceleration=Spec.Rule=="XSealBurst"?_accelerationDepths.GetOrCreateValue(Owner):null;
        if(acceleration!=null)acceleration.Depth++;
        bool completed=false;
        _resolvingEdict=boosted;
        _imprintVolleyHits=0;
        try
        {
            if(boosted) {
                await PowerCmd.Decrement(edict!);
                TemporaryEdictPower.RefreshHand(Owner.Creature);
            }
            await PlayBase(context,play);
            foreach(var imprint in Spec.Imprints??[])
            {
                if((!boosted&&!_forceImprint&&imprint.Seal>seals)||Owner.Creature.IsDead)continue;
                for(int trigger=0;trigger<(boosted?2:1);trigger++) {
                    if(imprint.Effect!="VolleyMarks")await RepeatImprint(context,play,imprint);
                    foreach(var listener in Owner.Creature.Powers.OfType<IImprintListener>().ToArray())
                        await listener.AfterImprint(context,play,this,imprint);
                }
            }
            completed=true;
        }
        finally {
            _resolvingEdict=previous;
            if(acceleration!=null) {
                acceleration.Depth--;
                if(completed&&acceleration.Depth==0&&play.PlayIndex==play.PlayCount-1
                    &&!Owner.Creature.IsDead&&!MegaCrit.Sts2.Core.Combat.CombatManager.Instance.IsOverOrEnding)
                    PlayerCmd.EndTurn(Owner,false);
            }
        }
    }

    // Repeats only the bonus, without paying/playing the card or invoking listeners.
    public async Task RepeatImprint(PlayerChoiceContext ctx,CardPlay play,SealImprint imprint)
    {
        int n=imprint.Value(IsUpgraded);
        switch(imprint.Effect)
        {
            case "Strength": await Power<StrengthPower>(ctx,n);break;
            case "ExhaustBlast":
                var damage=CardPile.Get(PileType.Exhaust,Owner)!.Cards.Count*n;
                foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Fixed(ctx,enemy,damage);
                break;
            case "Block": await BlockAmount(play,n); break;
            case "Mark": await Debuff<RiftBurnPower>(ctx,play.Target,n); break;
            case "AllMark": foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Debuff<RiftBurnPower>(ctx,enemy,n); break;
            case "Spirit": await Spirit(ctx,n); break;
            case "SpiritX": await Spirit(ctx,ResolveEnergyXValue()); break;
            case "NextBlock": await Power<BlockNextTurnPower>(ctx,n); break;
            case "Energy": await PlayerCmd.GainEnergy(n,Owner); break;
            case "Draw": await Draw(ctx,n); break;
            case "Heal": await CreatureCmd.Heal(Owner.Creature,n); break;
            case "Intangible": await Power<IntangiblePower>(ctx,n); break;
            case "Rebirth": await Power<RebirthPower>(ctx,n); break;
            case "Artifact": await Power<ArtifactPower>(ctx,n); break;
            case "Weak": await Debuff<WeakPower>(ctx,play.Target,n); break;
            case "Attack": await Attack(ctx,play,damage:n); break;
            case "AllAttack": await Attack(ctx,play,true,n); break;
            case "Fixed": await Fixed(ctx,play.Target,n); break;
            case "AllFixed": foreach(var enemy in CombatState!.HittableEnemies.ToArray())await Fixed(ctx,enemy,n); break;
            case "SpiritStrike": await Attack(ctx,play,damage:DynamicVars.Damage.BaseValue+Owner.Creature.GetPowerAmount<SealProgressPower>()); break;
            case "CleanseArtifact": await SealProgressPower.Cleanse(Owner.Creature); await Power<ArtifactPower>(ctx,n); break;
            case "BlockArtifact": await BlockAmount(play,n); await Power<ArtifactPower>(ctx,2); break;
            case "ArtifactDraw": await Power<ArtifactPower>(ctx,n); await Draw(ctx,2); break;
            case "EnergyDraw": await PlayerCmd.GainEnergy(n,Owner); await Draw(ctx,n); break;
            case "StrengthBlock": await Power<StrengthPower>(ctx,n); await BlockAmount(play,16); break;
            case "RebirthDraw": await Power<RebirthPower>(ctx,n); await Draw(ctx,2); break;
            // Volley marks are applied per hit in PlayBase; repeats add one full volley of marks.
            case "VolleyMarks": await Debuff<RiftBurnPower>(ctx,play.Target,n*_imprintVolleyHits); break;
            default: throw new InvalidOperationException("Unknown imprint: "+imprint.Effect);
        }
    }
}

public sealed class ImprintGuidePower : PuniPower
{
    public override string CustomPackedIconPath=>Main.Res+"/images/powers/SealProgressPower.png";
    public override List<(string,string)> Localization => new PowerLoc("封印刻印","解封后激活卡牌上的额外效果。","打出时已解封对应封印，便触发该牌的刻印。刻印不消耗圣灵或封印。\n3 虚无、6 元素、9 能量、12 生命、15 轮回、18 永恒、21 圣洁。\n本张牌新解封的刻印从下一次打出开始生效。灰色未激活，金色已激活。圣印长鸣的额外重复不触发联动；敕令让刻印独立触发两次。");
}
