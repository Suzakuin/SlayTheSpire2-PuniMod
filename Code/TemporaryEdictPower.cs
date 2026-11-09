using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace PuniMod;

public sealed class TemporaryEdictPower:PuniPower
{
    public override string CustomPackedIconPath=>Main.Res+"/images/powers/SealProgressPower.png";
    public override List<(string,string)> Localization=>new PowerLoc("敕令·临时解封","临时激活下一张牌的刻印。","接下来 {Amount} 张带刻印的谱尼牌，无视刻印解封条件，刻印独立触发两次。基础效果只执行一次。敕令本身不增加圣灵或实际解封数量，牌上的圣灵效果正常结算。没有刻印的牌不消耗次数；可跨回合保留。");
    internal static void RefreshHand(Creature owner)
    {
        if(owner.Player is not {} player)return;
        foreach(var card in CardPile.Get(PileType.Hand,player)?.Cards.OfType<PuniCard>().ToArray()??[])
            MegaCrit.Sts2.Core.Nodes.Cards.NCard.FindOnTable(card,PileType.Hand)?.UpdateVisuals(PileType.Hand,CardPreviewMode.Normal);
    }
    public override Task AfterApplied(Creature? applier,MegaCrit.Sts2.Core.Models.CardModel? cardSource)
    {RefreshHand(Owner);return Task.CompletedTask;}
    public override Task AfterRemoved(Creature target)
    {RefreshHand(target);return Task.CompletedTask;}
}
