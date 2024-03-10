using HarmonyLib;

namespace Sigurd.Common.Features.Patches;

[HarmonyPatch(typeof(HUDManager), nameof(HUDManager.DisplayTip))]
class DisplayTipPatch
{
    // Normally we wouldn't prefix return false, however, we need all uses of `DisplayTip` to go through
    // our system in order to not cause improper timing. All uses of base game's `DisplayTip` will bypass queue
    // and immediately be shown, but preserving the currently showing tip, if there is one, and continuing it after
    // the tip is complete.
    private static bool Prefix(string headerText, string bodyText, bool isWarning, bool useSave, string prefsKey)
    {
        SPlayer player = SPlayer.LocalPlayer;

        if (player == null) return true;

        player.ShowTip(headerText, bodyText, 5, isWarning, useSave, prefsKey);

        return false;
    }
}
