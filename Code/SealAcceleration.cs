using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace PuniMod;

public abstract partial class PuniCard
{
    private bool _forceImprint;
    private sealed class AccelerationDepth { public int Depth; }
    private static readonly ConditionalWeakTable<Player,AccelerationDepth> _accelerationDepths=new();

    private async Task AccelerateSeals(PlayerChoiceContext context)
    {
        int x=ResolveEnergyXValue();
        var drawn=(await CardPileCmd.Draw(context,x+M,Owner)).Distinct().ToHashSet();
        bool Eligible(CardModel card)
        {
            if(!drawn.Contains(card)||card==this||card.Pile?.Type!=PileType.Hand)return false;
            card.CanPlay(out var reason,out _);
            return (reason&~(UnplayableReason.EnergyCostTooHigh|UnplayableReason.StarCostTooHigh))==UnplayableReason.None;
        }
        int maximum=Math.Min(x/2,drawn.Count(Eligible));
        if(maximum<=0||CombatManager.Instance.IsOverOrEnding)return;
        var prefs=new CardSelectorPrefs(new LocString("card_selection","PUNIMOD-SEAL_ACCELERATION"),0,maximum)
            {PretendCardsCanBePlayed=true};
        var selected=await CardSelectCmd.FromHand(context,Owner,prefs,Eligible,this);
        foreach(var card in selected.Distinct().Take(maximum).ToArray())
        {
            if(CombatManager.Instance.IsOverOrEnding||Owner.Creature.IsDead)break;
            if(card.Owner!=Owner||!Eligible(card))continue;
            if(card is PuniCard puni)
            {
                bool previous=puni._forceImprint;
                puni._forceImprint=true;
                try {await CardCmd.AutoPlay(context,card,null);}
                finally {puni._forceImprint=previous;}
            }
            else await CardCmd.AutoPlay(context,card,null);
        }
    }

    private async Task BurnForSpirit(PlayerChoiceContext context)
    {
        var hand=CardPile.Get(PileType.Hand,Owner);
        if(hand==null||!hand.Cards.Any(c=>c!=this))return;
        var prefs=new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt,0,1);
        var selected=await CardSelectCmd.FromHand(context,Owner,prefs,c=>c!=this,this);
        var card=selected.FirstOrDefault(c=>c!=this&&c.Owner==Owner&&c.Pile==hand);
        if(card==null)return;
        int cost=card.EnergyCost.CostsX?0:Math.Max(0,card.EnergyCost.GetWithModifiers(CostModifiers.Local));
        var result=await CardCmd.Exhaust(context,card);
        if(result.HasValue&&result.Value.success&&result.Value.targetPile==PileType.Exhaust)
            await Spirit(context,cost+M);
    }
}
