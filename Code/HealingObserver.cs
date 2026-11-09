using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace PuniMod;

// v0.111.0's HP-change hook receives the requested heal, including overheal.
// Observe the synchronous HealInternal change instead, then resolve after the
// native Heal task. Separate frames avoid counting nested heals twice.
[HarmonyPatch]
public static class PuniHealingObserver
{
    private sealed class Frame(Creature creature)
    { public Creature Creature=creature;public int Actual; }
    [ThreadStatic] private static Stack<Frame>? _frames;

    [HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.Heal))]
    [HarmonyPrefix]
    private static void BeforeHeal(Creature creature,out Frame? __state)
    {
        __state=null;
        if(creature?.HasPower<CycleOfStarsPower>()!=true)return;
        __state=new Frame(creature);(_frames??=new()).Push(__state);
    }
    [HarmonyPatch(typeof(Creature),"HealInternal")]
    [HarmonyPrefix]
    private static void BeforeInternal(Creature __instance,out int __state)=>__state=__instance.CurrentHp;
    [HarmonyPatch(typeof(Creature),"HealInternal")]
    [HarmonyPostfix]
    private static void AfterInternal(Creature __instance,int __state)
    {
        if(_frames?.TryPeek(out var frame)==true&&frame.Creature==__instance)
            frame.Actual+=Math.Max(0,__instance.CurrentHp-__state);
    }
    [HarmonyPatch(typeof(CreatureCmd),nameof(CreatureCmd.Heal))]
    [HarmonyPostfix]
    private static void AfterHeal(Creature creature,Frame? __state,ref Task __result)
    {
        if(__state==null)return;
        if(_frames?.TryPeek(out var frame)==true&&ReferenceEquals(frame,__state))_frames.Pop();
        __result=Finish(__result,creature,__state.Actual);
    }
    private static async Task Finish(Task native,Creature creature,int healed)
    {
        await native;
        var power=creature.GetPower<CycleOfStarsPower>();
        if(power!=null&&healed>0)await power.AfterActualHealing(healed);
    }
}
