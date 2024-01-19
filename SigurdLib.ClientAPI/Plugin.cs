using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Sigurd.ClientAPI.ChatCommands;

namespace Sigurd.ClientAPI;

/// <summary>
/// The main Plugin class.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    /// <summary>
    /// This Plugin's GUID.
    /// </summary>
    public const string Guid = MyPluginInfo.PLUGIN_GUID;

    internal static Plugin Instance { get; private set; } = null!;

    internal static ManualLogSource Log { get; private set; } = null!;

    internal static Harmony Harmony { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;

        Log = Logger;

        CommandRegistry.CommandPrefix = Config.Bind("Commands", "Prefix", "/", "Command prefix");

        Harmony = new Harmony($"{MyPluginInfo.PLUGIN_GUID}-{DateTime.Now.Ticks}");
        Harmony.PatchAll();

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} ({MyPluginInfo.PLUGIN_VERSION}) has awoken.");
    }
}
