using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Models.Events;

namespace PuniMod;

// v0.111.0 permits a missing character dialogue when building the Proceed
// option, but WinRun subsequently dereferences it. Keep the native win flow.
[HarmonyPatch]
public static class PuniArchitectDialogue
{
    private static void EnsureDialogue(TheArchitect architect, ref AncientDialogue? dialogue)
    {
        if (architect.Owner?.Character is Puni && dialogue == null)
            dialogue = new AncientDialogue("")
            {
                StartAttackers = ArchitectAttackers.None,
                EndAttackers = ArchitectAttackers.Architect
            };
    }

    [HarmonyPatch(typeof(TheArchitect), "LoadDialogue")]
    [HarmonyPostfix]
    public static void AfterLoad(TheArchitect __instance, ref AncientDialogue? ____dialogue)
        => EnsureDialogue(__instance, ref ____dialogue);

    // Also covers a resumed event whose options were already generated.
    [HarmonyPatch(typeof(TheArchitect), "WinRun")]
    [HarmonyPrefix]
    public static void BeforeProceed(TheArchitect __instance, ref AncientDialogue? ____dialogue)
        => EnsureDialogue(__instance, ref ____dialogue);
}
