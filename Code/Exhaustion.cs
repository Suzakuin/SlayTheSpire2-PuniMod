using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace PuniMod;

public abstract partial class PuniCard
{
    private async Task<int> ExhaustChosen(PlayerChoiceContext ctx,PileType type,int maximum)
    {
        var pile=CardPile.Get(type,Owner);
        if(pile==null||maximum<=0||!pile.Cards.Any(c=>c!=this))return 0;
        int limit=Math.Min(maximum,pile.Cards.Count(c=>c!=this));
        var prefs=new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt,0,limit);
        var selected=type==PileType.Hand
            ?await CardSelectCmd.FromHand(ctx,Owner,prefs,c=>c!=this,this)
            :await CardSelectCmd.FromCombatPile(ctx,pile,Owner,prefs,c=>c!=this);
        int exhausted=0;
        foreach(var card in selected.Distinct().Take(limit).ToArray())
        {
            if(card==this||card.Owner!=Owner||card.Pile!=pile)continue;
            // Successful native exhaust remains a success even if a later hook moves it.
            var result=await CardCmd.Exhaust(ctx,card);
            if(result.HasValue&&result.Value.success&&result.Value.targetPile==PileType.Exhaust)exhausted++;
        }
        return exhausted;
    }

}
