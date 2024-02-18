using HarmonyLib;
using OdinSerializer;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sigurd.Networking;

/// <summary>
/// Provies an easy to use interface for UnityNetcode custom network messages.
/// </summary>
public static class Network
{
    internal const string MESSAGE_RELAY_UNIQUE_NAME = "SIGURD_NETWORK_RELAY_MESSAGE";
    internal static Dictionary<string, NetworkMessageFinalizerBase> NetworkMessageFinalizers { get; } = new();

    internal static byte[] ToBytes<T>(this T @object)
    {
        return SerializationUtility.SerializeValue(@object, DataFormat.Binary);
    }

    internal static T? ToObject<T>(this byte[] bytes) where T : class
    {
        return SerializationUtility.DeserializeValue<T>(bytes, DataFormat.Binary);
    }

    internal static bool StartedNetworking { get; set; } = false;

    private static MethodInfo? _registerInfo = null;
    private static MethodInfo? _registerInfoGeneric = null;

    internal static MethodInfo RegisterInfo
    {
        get
        {
            if (_registerInfo is not null) return _registerInfo;

            _registerInfo = typeof(Network)
                .GetMethods()
                .First(methodInfo => methodInfo is { Name: nameof(RegisterMessage), IsGenericMethod: false });

            return _registerInfo;
        }
    }

    internal static MethodInfo RegisterInfoGeneric
    {
        get
        {
            if (_registerInfoGeneric is not null) return _registerInfoGeneric;

            _registerInfoGeneric = typeof(Network)
                .GetMethods()
                .First(methodInfo => methodInfo is { Name: nameof(RegisterMessage), IsGenericMethod: true });

            return _registerInfoGeneric;
        }
    }

    internal static void RegisterAllMessages()
    {
        foreach (NetworkMessageFinalizerBase handler in NetworkMessageFinalizers.Values)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(handler.UniqueName, handler.Read);
        }
    }

    internal static void UnregisterAllMessages()
    {

        foreach (string name in NetworkMessageFinalizers.Keys)
        {
            UnregisterMessage(name, false);
        }
    }

    /// <summary>
    /// Registers all network messages contained in your assembly.
    /// </summary>
    public static void RegisterAll()
    {
        // This cursed line of code comes from Harmony's PatchAll method. Thanks, Harmony
        var m = new StackTrace().GetFrame(1).GetMethod();
        var assembly = m.ReflectedType!.Assembly;
        AccessTools.GetTypesFromAssembly(assembly)
            .Do(RegisterAll);
    }

    /// <summary>
    /// Registers all network messages contained in the provided <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to register network messages from.</param>
    public static void RegisterAll(Type type)
    {
        if (!type.IsClass) return;

        NetworkMessageAttribute networkMessageAttribute = type.GetCustomAttribute<NetworkMessageAttribute>();

        if (networkMessageAttribute is not null)
        {
            RegisterClassPattern(type);
            return;
        }

        RegisterAttributePattern(type);
    }

    /// <summary>
    /// Registers a 'class pattern' network message.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to register as a network message.</param>
    private static void RegisterClassPattern(Type type)
    {
        NetworkMessageAttribute networkMessageAttribute = type.GetCustomAttribute<NetworkMessageAttribute>();

        if (type.BaseType?.Name is "NetworkMessageHandler`1")
        {
            Type messageType = type.BaseType.GetGenericArguments()[0];
            RegisterInfoGeneric
                .MakeGenericMethod(messageType)
                .Invoke(null, [
                    networkMessageAttribute.UniqueName,
                    networkMessageAttribute.RelayToSelf,
                    type.GetMethod("Handler")!.CreateDelegate(typeof(Action<,>)
                        .MakeGenericType(typeof(ulong), messageType), Activator.CreateInstance(type))
                ]);
        }
        else if (type.BaseType?.Name is "NetworkMessageHandler")
        {
            RegisterInfo
                .Invoke(null, [
                    networkMessageAttribute.UniqueName,
                    networkMessageAttribute.RelayToSelf,
                    type.GetMethod("Handler")!.CreateDelegate(typeof(Action<>)
                        .MakeGenericType(typeof(ulong)), Activator.CreateInstance(type))
                ]);
        }
    }

    /// <summary>
    /// Registers network messages from an 'attribute pattern' type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to register network messages from.</param>
    private static void RegisterAttributePattern(Type type)
    {
        type
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
            .Do(TryRegisterAttributePatternMethod);
    }

    /// <summary>
    /// Registers an 'attribute pattern' network message.
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> to register as a network message.</param>
    private static void TryRegisterAttributePatternMethod(MethodInfo method)
    {
        var networkMessage = method.GetCustomAttribute<NetworkMessageAttribute>();
        if (networkMessage is null) return;

        if (!method.IsStatic) throw new Exception($"Detected NetworkMessage attribute on non-static method '{method.Name}'. All NetworkMessages on methods must be static.");

        if (method.GetParameters() is { Length: 1 })
        {
            RegisterInfo
                .Invoke(null, [
                    networkMessage.UniqueName,
                    networkMessage.RelayToSelf,
                    method.CreateDelegate(typeof(Action<>)
                        .MakeGenericType(typeof(ulong)))
                ]);
            return;
        }

        if (method.GetParameters() is { Length: > 1 })
        {
            Type messageType = method.GetParameters()[1].ParameterType;
            RegisterInfoGeneric
                .MakeGenericMethod(messageType)
                .Invoke(null, [
                    networkMessage.UniqueName,
                    networkMessage.RelayToSelf,
                    method.CreateDelegate(typeof(Action<,>)
                        .MakeGenericType(typeof(ulong), messageType))
                ]);
            return;
        }

        throw new Exception($"Detected NetworkMessage attribute on a method with no parameters '{method.Name}'.");
    }

    /// <summary>
    /// Unregisters all network messages contained in your assembly.
    /// </summary>
    /// <param name="andRemoveHandler">Wheter or not to prevent the handler from being re-registered when a new game is joined.</param>
    public static void UnregisterAll(bool andRemoveHandler = true)
    {
        // This cursed line of code comes from Harmony's PatchAll method. Thanks, Harmony
        var m = new StackTrace().GetFrame(1).GetMethod();
        var assembly = m.ReflectedType!.Assembly;
        AccessTools.GetTypesFromAssembly(assembly)
            .Do(type => UnregisterAll(type, andRemoveHandler));
    }

    /// <summary>
    /// Unregisters all network messages contained in the provided <paramref name="type"/>.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to unregister all network messages of.</param>
    /// <param name="andRemoveHandler">Whether or not to prevent the handler from being re-registered when a new game is joined.</param>
    public static void UnregisterAll(Type type, bool andRemoveHandler = true)
    {
        if (!type.IsClass) return;
        NetworkMessageAttribute networkMessageAttribute = type.GetCustomAttribute<NetworkMessageAttribute>();

        if (networkMessageAttribute is not null)
        {
            UnregisterClassPattern(type, andRemoveHandler);
            return;
        }

        UnregisterAttributePattern(type, andRemoveHandler);
    }

    /// <summary>
    /// Unregisters a 'class pattern' network message.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to unregister as a network message.</param>
    /// <param name="andRemoveHandler">Whether or not to prevent the handler from being re-registered when a new game is joined.</param>
    private static void UnregisterClassPattern(Type type, bool andRemoveHandler = true)
    {
        NetworkMessageAttribute networkMessageAttribute = type.GetCustomAttribute<NetworkMessageAttribute>();
        UnregisterMessage(networkMessageAttribute.UniqueName, andRemoveHandler);
    }

    /// <summary>
    /// Unregisters network messages from an 'attribute pattern' type.
    /// </summary>
    /// <param name="type">The <see cref="Type"/> to unregister network messages from.</param>
    /// <param name="andRemoveHandler">Whether or not to prevent the handler from being re-registered when a new game is joined.</param>
    private static void UnregisterAttributePattern(Type type, bool andRemoveHandler = true)
    {
        type
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
            .Do(method => TryUnregisterAttributePatternMethod(method, andRemoveHandler));
    }

    /// <summary>
    /// Unregisters an 'attribute pattern' network message.
    /// </summary>
    /// <param name="method">The <see cref="MethodInfo"/> to unregister as a network message.</param>
    /// <param name="andRemoveHandler">Whether or not to prevent the handler from being re-registered when a new game is joined.</param>
    private static void TryUnregisterAttributePatternMethod(MethodInfo method, bool andRemoveHandler = true)
    {
        var networkMessageAttribute = method.GetCustomAttribute<NetworkMessageAttribute>();
        if (networkMessageAttribute is null) return;
        UnregisterMessage(networkMessageAttribute.UniqueName, andRemoveHandler);
    }

    /// <summary>
    /// Registers a network message with a name and handler.
    /// </summary>
    /// <typeparam name="T">The type of the network message.</typeparam>
    /// <param name="uniqueName">The name of the network message.</param>
    /// <param name="relayToSelf">Whether or not this message should be relayed to the sender.</param>
    /// <param name="onReceived">The handler to use for the message.</param>
    /// <exception cref="Exception">Thrown when T is not serializable, or if the name is already taken.</exception>
    public static void RegisterMessage<T>(string uniqueName, bool relayToSelf, Action<ulong, T> onReceived) where T : class
    {
        if (NetworkMessageFinalizers.ContainsKey(uniqueName))
            throw new Exception($"{uniqueName} already registered");

        NetworkMessageFinalizer<T> networkMessageHandler = new NetworkMessageFinalizer<T>(uniqueName, relayToSelf, onReceived);

        NetworkMessageFinalizers.Add(uniqueName, networkMessageHandler);

        if (StartedNetworking)
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(uniqueName, networkMessageHandler.Read);
    }

    /// <summary>
    /// Registers a network message with a name and handler.
    /// </summary>
    /// <param name="uniqueName">The name of the network message.</param>
    /// <param name="relayToSelf">Whether or not this message should be relayed to the sender.</param>
    /// <param name="onReceived">The handler to use for the message.</param>
    /// <exception cref="Exception">Thrown when the name is already taken.</exception>
    public static void RegisterMessage(string uniqueName, bool relayToSelf, Action<ulong> onReceived)
    {
        if (NetworkMessageFinalizers.ContainsKey(uniqueName))
            throw new Exception($"{uniqueName} already registered");

        NetworkMessageFinalizer networkMessageHandler = new NetworkMessageFinalizer(uniqueName, relayToSelf, onReceived);

        NetworkMessageFinalizers.Add(uniqueName, networkMessageHandler);

        if (StartedNetworking)
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(uniqueName, networkMessageHandler.Read);
    }

    /// <summary>
    /// Unregisters a network message.
    /// </summary>
    /// <param name="uniqueName">The name of the message to unregister.</param>
    /// <param name="andRemoveHandler">Whether or not to prevent the handler from being re-registered when a new game is joined.</param>
    public static void UnregisterMessage(string uniqueName, bool andRemoveHandler = true)
    {
        if (!NetworkMessageFinalizers.ContainsKey(uniqueName)) return;
        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(uniqueName);

        if (!andRemoveHandler) return;
        NetworkMessageFinalizers.Remove(uniqueName);
    }

    /// <summary>
    /// Sends a network message.
    /// </summary>
    /// <typeparam name="T">The type of the network message.</typeparam>
    /// <param name="uniqueName">The name of the network message.</param>
    /// <param name="object">The network message to send.</param>
    /// <exception cref="Exception">Thrown when the registered message with the name is not of the same type as the network message.</exception>
    public static void Broadcast<T>(string uniqueName, T @object) where T : class
    {
        if (!NetworkMessageFinalizers.TryGetValue(uniqueName, out NetworkMessageFinalizerBase handler)) return;
        if (handler is not NetworkMessageFinalizer<T> genericHandler)
        {
            throw new Exception($"Network handler for {uniqueName} was not broadcast with the right type!");
        }

        genericHandler.Send(@object);
    }

    /// <summary>
    /// Sends a network message that has no body.
    /// </summary>
    /// <param name="uniqueName">The name of the network message.</param>
    /// <exception cref="Exception">Thrown when the registered message with the name is not of the same type as the network message.</exception>
    public static void Broadcast(string uniqueName)
    {
        if (!NetworkMessageFinalizers.TryGetValue(uniqueName, out NetworkMessageFinalizerBase handler)) return;
        if (handler is not NetworkMessageFinalizer finalizer)
            throw new Exception($"Network handler for {uniqueName} was not broadcast with the right type!");

        finalizer.Send();
    }

    internal static void RegisterMessages()
    {
        StartedNetworking = true;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(MESSAGE_RELAY_UNIQUE_NAME,
                (ulong senderClientId, FastBufferReader reader) =>
                {
                    reader.ReadValueSafe(out byte[] data);

                    NetworkMessageWrapper wrapped = data.ToObject<NetworkMessageWrapper>()!;

                    wrapped.Sender = senderClientId;

                    byte[] serialized = wrapped.ToBytes();

                    using (FastBufferWriter writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(serialized), Unity.Collections.Allocator.Temp))
                    {
                        writer.WriteValueSafe(serialized);

                        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(wrapped.UniqueName, writer, NetworkDelivery.ReliableFragmentedSequenced);
                    }
                });
        }

        RegisterAllMessages();
    }

    internal static void UnregisterMessages()
    {
        StartedNetworking = false;

        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(MESSAGE_RELAY_UNIQUE_NAME);

        UnregisterAllMessages();
    }

    internal static void Init()
    {
        RegisterAll();
    }
}

/// <summary>
/// Allows a method/class to act as a network message.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public class NetworkMessageAttribute : Attribute
{
    /// <summary>
    /// The name of the message.
    /// </summary>
    public string UniqueName { get; }

    /// <summary>
    /// Whether or not to relay this message back to the sender.
    /// </summary>
    public bool RelayToSelf { get; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

    public NetworkMessageAttribute(string uniqueName, bool relayToSelf = false)
    {
        UniqueName = uniqueName;
        RelayToSelf = relayToSelf;
    }
#pragma warning restore
}

/// <summary>
/// For use when decorating a class with the <see cref="NetworkMessageAttribute"/> attribute.
/// </summary>
/// <typeparam name="T">The type of the message. Must be Serializable.</typeparam>
public abstract class NetworkMessageHandler<T> where T : class
{
    /// <summary>
    /// The message handler.
    /// </summary>
    /// <param name="sender">The sender's client id.</param>
    /// <param name="message">The network message.</param>
    public abstract void Handler(ulong sender, T message);
}

/// <summary>
/// For use when decorating a class with the <see cref="NetworkMessageAttribute"/> attribute.
/// </summary>
public abstract class NetworkMessageHandler
{
    /// <summary>
    /// The message handler.
    /// </summary>
    /// <param name="sender">The sender's client id.</param>
    public abstract void Handler(ulong sender);
}

internal abstract class NetworkMessageFinalizerBase
{
    internal abstract string UniqueName { get; }

    internal abstract bool RelayToSelf { get; }

    public abstract void Read(ulong sender, FastBufferReader reader);
}

internal class NetworkMessageFinalizer : NetworkMessageFinalizerBase
{
    internal override string UniqueName { get; }

    internal override bool RelayToSelf { get; }

    internal Action<ulong> OnReceived { get; }

    public NetworkMessageFinalizer(string uniqueName, bool relayToSelf, Action<ulong> onReceived)
    {
        UniqueName = uniqueName;
        RelayToSelf = relayToSelf;
        OnReceived = onReceived;
    }

    public void Send()
    {
        if (StartOfRound.Instance.localPlayerController == null)
        {
            NetworkManager.Singleton.StartCoroutine(SendLater());
            return;
        }

        NetworkMessageWrapper wrapped = new NetworkMessageWrapper(UniqueName, StartOfRound.Instance.localPlayerController.actualClientId);
        byte[] serialized = wrapped.ToBytes();

        using (FastBufferWriter writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(serialized), Unity.Collections.Allocator.Temp))
        {
            writer.WriteValueSafe(serialized);

            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(UniqueName, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
            else
            {
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(Network.MESSAGE_RELAY_UNIQUE_NAME, StartOfRound.Instance.allPlayerScripts[0].actualClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
    }

    public override void Read(ulong fakeSender, FastBufferReader reader)
    {
        if (StartOfRound.Instance.localPlayerController == null)
        {
            NetworkManager.Singleton.StartCoroutine(ReadLater(fakeSender, reader));
            return;
        }

        byte[] data;

        reader.ReadValueSafe(out data);

        NetworkMessageWrapper wrapped = data.ToObject<NetworkMessageWrapper>()!;

        if (!RelayToSelf && StartOfRound.Instance.localPlayerController.actualClientId == wrapped.Sender) return;
        OnReceived.Invoke(wrapped.Sender);
    }

    private IEnumerator SendLater()
    {
        int timesWaited = 0;
        while (StartOfRound.Instance.localPlayerController == null)
        {
            yield return new WaitForSeconds(0.1f);
            timesWaited++;
            if (timesWaited % 20 == 0)
            {
                Plugin.Log.LogWarning($"Waiting to send network message.");
            }

            if (timesWaited >= 100)
            {
                Plugin.Log.LogError("Dropping network message");
                yield return null;
            }
        }

        Send();
    }

    private IEnumerator ReadLater(ulong fakeSender, FastBufferReader reader)
    {
        int timesWaited = 0;
        while (StartOfRound.Instance.localPlayerController == null)
        {
            yield return new WaitForSeconds(0.1f);
            timesWaited++;

            if (timesWaited % 20 == 0)
            {
                Plugin.Log.LogWarning($"Waiting to read network message.");
            }

            if (timesWaited >= 100)
            {
                Plugin.Log.LogError("Dropping network message");
                yield return null;
            }
        }

        Read(fakeSender, reader);
    }
}

internal class NetworkMessageFinalizer<T> : NetworkMessageFinalizerBase where T : class
{
    internal override string UniqueName { get; }

    internal override bool RelayToSelf { get; }

    internal Action<ulong, T> OnReceived { get; }

    public NetworkMessageFinalizer(string uniqueName, bool relayToSelf, Action<ulong, T> onReceived)
    {
        UniqueName = uniqueName;
        RelayToSelf = relayToSelf;
        OnReceived = onReceived;
    }

    public void Send(T obj)
    {
        if (StartOfRound.Instance.localPlayerController == null)
        {
            NetworkManager.Singleton.StartCoroutine(SendLater(obj));
            return;
        }

        NetworkMessageWrapper wrapped = new NetworkMessageWrapper(UniqueName, StartOfRound.Instance.localPlayerController.actualClientId, obj.ToBytes());

        byte[] serialized = wrapped.ToBytes();

        using FastBufferWriter writer = new FastBufferWriter(FastBufferWriter.GetWriteSize(serialized), Unity.Collections.Allocator.Temp);
        writer.WriteValueSafe(serialized);

        if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(UniqueName, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
        else
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(Network.MESSAGE_RELAY_UNIQUE_NAME, StartOfRound.Instance.allPlayerScripts[0].actualClientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
        }
    }

    public override void Read(ulong fakeSender, FastBufferReader reader)
    {
        if (StartOfRound.Instance.localPlayerController == null)
        {
            NetworkManager.Singleton.StartCoroutine(ReadLater(fakeSender, reader));
            return;
        }

        reader.ReadValueSafe(out byte[] data);

        NetworkMessageWrapper wrapped = data.ToObject<NetworkMessageWrapper>()!;

        if (!RelayToSelf && StartOfRound.Instance.localPlayerController.actualClientId == wrapped.Sender) return;

        OnReceived.Invoke(wrapped.Sender, wrapped.Message!.ToObject<T>()!);
    }

    private IEnumerator SendLater(T obj)
    {
        int timesWaited = 0;

        while (StartOfRound.Instance.localPlayerController == null)
        {
            yield return new WaitForSeconds(0.1f);

            timesWaited++;

            if (timesWaited % 20 == 0)
            {
                Plugin.Log.LogWarning($"Waiting to send network message.");
            }

            if (timesWaited >= 100)
            {
                Plugin.Log.LogError("Dropping network message");
                yield return null;
            }
        }

        Send(obj);
    }

    private IEnumerator ReadLater(ulong fakeSender, FastBufferReader reader)
    {
        int timesWaited = 0;
        while (StartOfRound.Instance.localPlayerController == null)
        {
            yield return new WaitForSeconds(0.1f);
            timesWaited++;

            if (timesWaited % 20 == 0)
            {
                Plugin.Log.LogWarning($"Waiting to read network message.");
            }

            if (timesWaited >= 100)
            {
                Plugin.Log.LogError("Dropping network message");
                yield return null;
            }
        }

        Read(fakeSender, reader);
    }
}

internal class NetworkMessageWrapper
{
    public string UniqueName = null!;

    public ulong Sender;

    public byte[]? Message;

    internal NetworkMessageWrapper(string uniqueName, ulong sender)
    {
        UniqueName = uniqueName;
        Sender = sender;
    }

    internal NetworkMessageWrapper(string uniqueName, ulong sender, byte[] message)
    {
        UniqueName = uniqueName;
        Sender = sender;
        Message = message;
    }

    internal NetworkMessageWrapper() { }
}

[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
internal static class RegisterPatch
{
    internal static void Postfix()
    {
        Network.RegisterMessages();
    }
}

[HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Shutdown))]
internal static class UnregisterPatch
{
    internal static void Postfix()
    {
        Network.UnregisterMessages();
    }
}
