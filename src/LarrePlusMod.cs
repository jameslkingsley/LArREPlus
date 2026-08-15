using System;
using BepInEx.Configuration;
using HarmonyLib;
using StationeersMods.Interface;
using UnityEngine;

namespace LarrePlus;

[StationeersMod(ModId, DisplayName, Version)]
public sealed class LarrePlusMod : ModBehaviour
{
    public const string ModId = "com.james.larreplus";
    public const string DisplayName = "LArRE+";
    public const string Version = "0.3.2";

    private Harmony _harmony;
    private readonly ConfigEntry<float> _movementSpeed;

    public LarrePlusMod()
    {
        _movementSpeed = Config.Bind(
            "LArRE Arms",
            "MovementSpeedMultiplier",
            1f,
            new ConfigDescription(
                "Global LArRE rail, bypass, extension, retraction, and cargo-cycle speed. " +
                "The authoritative server value is sent to clients when arms join the world. " +
                "Restart the server after changing this setting.",
                new AcceptableValueRange<float>(0.1f, 10f)));
    }

    public override void OnLoaded(ContentHandler contentHandler)
    {
        base.OnLoaded(contentHandler);

        try
        {
            // LaunchPad's pre-launch configuration UI writes the selected values
            // after it has constructed this ModBehaviour. Reload the shared file
            // so the entries bound in the constructor reflect those selections.
            Config.Reload();
            ArmEnhancements.ConfigureSpeed(_movementSpeed.Value);
            Log($"Configured LArRE movement speed multiplier: {_movementSpeed.Value:0.###}x.");
            AimeeCargoArmCompatibility.ValidateGameApi();
            ArmEnhancements.ValidateGameApi();
            _harmony = new Harmony(ModId);
            _harmony.PatchAll(typeof(LarrePlusMod).Assembly);
            Log("Enabled Cargo Large Arm access to AIMeE slots and whole-bot transport.");
        }
        catch (Exception exception)
        {
            LogError("Compatibility installation failed", exception);
        }
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    internal static void Log(string message)
    {
        Debug.unityLogger.Log(LogType.Log, $"[LarrePlus] {message}");
    }

    internal static void LogWarning(string message)
    {
        Debug.unityLogger.Log(LogType.Warning, $"[LarrePlus] {message}");
    }

    internal static void LogError(string message, Exception exception = null)
    {
        string details = exception == null ? message : $"{message}: {exception}";
        Debug.unityLogger.Log(LogType.Error, $"[LarrePlus] {details}");
    }
}
