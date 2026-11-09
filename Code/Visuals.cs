using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace PuniMod;

// v0.111.0's merchant initializer assumes a Spine child. This character uses a
// native Godot AnimationPlayer; scope the adapter to our tagged scene only.
[HarmonyPatch]
public static class PuniMerchantAnimation
{
    private static AnimationPlayer? FindPlayer(Node root)
    {
        var player=root.FindChild("AnimationPlayer",true,false) as AnimationPlayer;
        return player!=null && player.HasMeta("puni_mod") ? player : null;
    }
    [HarmonyPatch(typeof(NMerchantCharacter),nameof(NMerchantCharacter._Ready))]
    [HarmonyPrefix]
    public static bool Ready(NMerchantCharacter __instance)
    {
        var player=FindPlayer(__instance);
        if(player==null)return true;
        player.Play("Idle");
        return false;
    }
    [HarmonyPatch(typeof(NMerchantCharacter),nameof(NMerchantCharacter.PlayAnimation))]
    [HarmonyPrefix]
    public static bool Play(NMerchantCharacter __instance)
    {
        var player=FindPlayer(__instance);
        if(player==null)return true;
        if(!player.IsPlaying())player.Play("Idle");
        return false;
    }
}
