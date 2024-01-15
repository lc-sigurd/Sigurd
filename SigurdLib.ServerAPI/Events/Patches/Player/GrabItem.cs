using System.Reflection.Emit;
using GameNetcodeStuff;
using HarmonyLib;
using Sigurd.Common.Features;
using Sigurd.ServerAPI.Events.EventArgs.Player;
using Sigurd.ServerAPI.Extensions;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace Sigurd.ServerAPI.Events.Patches.Player
{
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.BeginGrabObject))]
    internal class GrabbingItem
    {
        internal static StartGrabbingItemEventArgs CallStartGrabbingItem(PlayerControllerB playerController, GrabbableObject grabbableObject)
        {
            StartGrabbingItemEventArgs ev = new StartGrabbingItemEventArgs(Common.Features.SPlayer.GetOrAdd(playerController),
                Common.Features.SItem.Get(grabbableObject)!);

        Handlers.Player.OnStartGrabbingItem(ev);

        return ev;
    }

        internal static GrabbingItemEventArgs CallGrabbingItem(PlayerControllerB playerController, GrabbableObject grabbableObject)
        {
            SPlayer player = SPlayer.GetOrAdd(playerController);
            SItem item = SItem.Get(grabbableObject)!;
            GrabbingItemEventArgs ev = new GrabbingItemEventArgs(player, item);

        Handlers.Player.OnGrabbingItem(ev);

            player.GetNetworking().CallGrabbingItemOnOtherClients(item);

            return ev;
        }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

        {
            const int offset = 15;

            int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Stfld &&
                                                           i.OperandIs(AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject)))) + offset;

            Label notAllowedLabel = generator.DefineLabel();
            Label skipLabel = generator.DefineLabel();

            CodeInstruction[] inst = new CodeInstruction[]
            {
                // StartGrabbingItemEventArgs ev = GrabbingItem.CallStartGrabbingItem(PlayerControllerB, GrabbableObject)
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbingItem), nameof(GrabbingItem.CallStartGrabbingItem))),

                // if (!ev.IsAllowed) return
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(StartGrabbingItemEventArgs), nameof(StartGrabbingItemEventArgs.IsAllowed))),
                new CodeInstruction(OpCodes.Brfalse_S, notAllowedLabel),

                new CodeInstruction(OpCodes.Br, skipLabel),
                new CodeInstruction(OpCodes.Ret).WithLabels(notAllowedLabel)
            };

            newInstructions.InsertRange(index, inst);

            newInstructions[index + inst.Length].labels.Add(skipLabel);
        }

        {
            const int offset = -2;

            int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Ldstr &&
                                                           i.OperandIs("GrabInvalidated")) + offset;

            Label notAllowedLabel = generator.DefineLabel();
            Label skipLabel = generator.DefineLabel();

            CodeInstruction[] inst = new CodeInstruction[]
            {
                // GrabbingItemEventArgs ev = GrabbingItem.CallGrabbingItem(PlayerControllerB, GrabbableObject)
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlayerControllerB), nameof(PlayerControllerB.currentlyGrabbingObject))),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbingItem), nameof(GrabbingItem.CallGrabbingItem))),

                // if (!ev.IsAllowed) return
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(StartGrabbingItemEventArgs), nameof(StartGrabbingItemEventArgs.IsAllowed))),
                new CodeInstruction(OpCodes.Brfalse_S, notAllowedLabel),

                new CodeInstruction(OpCodes.Br, skipLabel),
                new CodeInstruction(OpCodes.Ret).WithLabels(notAllowedLabel)
            };

            newInstructions.InsertRange(index, inst);

            newInstructions[index + inst.Length].labels.Add(skipLabel);
        }

        for (int i = 0; i < newInstructions.Count; i++) yield return newInstructions[i];
    }
}

    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.GrabObjectClientRpc))]
    internal class GrabbedItem
    {
        internal static void CallEvent(PlayerControllerB player)
        {
            Handlers.Player.OnGrabbedItem(new GrabbedItemEventArgs(Common.Features.SPlayer.GetOrAdd(player),
                Common.Features.SItem.Get(player.currentlyHeldObjectServer)!));
        }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> newInstructions = new List<CodeInstruction>(instructions);

        {
            const int offset = 1;

            int index = newInstructions.FindLastIndex(i => i.opcode == OpCodes.Callvirt &&
                                                           i.OperandIs(AccessTools.Method(typeof(AudioSource),
                                                               nameof(AudioSource.PlayOneShot), new[] { typeof(AudioClip), typeof(float) }))) + offset;

            CodeInstruction[] inst = new CodeInstruction[]
            {
                // GrabbedItem.CallEvent(PlayerControllerB)
                new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(newInstructions[index]),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbedItem), nameof(GrabbedItem.CallEvent))),
            };

            newInstructions.InsertRange(index, inst);
        }

        {
            const int offset = 1;

            int index = newInstructions.FindIndex(i => i.opcode == OpCodes.Call &&
                                                       i.OperandIs(AccessTools.PropertyGetter(typeof(NetworkBehaviour), nameof(NetworkBehaviour.IsOwner)))) + offset;

            Label skipToLabel = generator.DefineLabel();

            newInstructions[index].operand = skipToLabel;

            CodeInstruction[] inst = new CodeInstruction[]
            {
                new CodeInstruction(OpCodes.Br, newInstructions[newInstructions.Count - 1].labels[0]),

                // GrabbedItem.CallEvent(PlayerControllerB)
                new CodeInstruction(OpCodes.Ldarg_0).WithLabels(skipToLabel),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GrabbedItem), nameof(GrabbedItem.CallEvent))),
            };

            newInstructions.InsertRange(newInstructions.Count - 1, inst);
        }

        for (int i = 0; i < newInstructions.Count; i++) yield return newInstructions[i];
    }
}