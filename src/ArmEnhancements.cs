using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Assets.Scripts;
using Assets.Scripts.Networking;
using Assets.Scripts.Objects;
using HarmonyLib;
using Objects.RoboticArm;
using UnityEngine;

namespace LarrePlus;

internal static class ArmEnhancements
{
    private const float VanillaBypassSpeed = 0.5f;
    private const int VanillaCargoDelayMilliseconds = 200;
    private const float MinimumSpeed = 0.1f;
    private const float MaximumSpeed = 10f;

    private static readonly FieldInfo ArmField =
        AccessTools.Field(typeof(RoboticArmDock), "_arm");

    private static readonly FieldInfo MoveSpeedField =
        AccessTools.Field(typeof(RoboticArmDock), "_moveSpeed");

    private static readonly FieldInfo SkipCollisionCheckField =
        AccessTools.Field(typeof(RoboticArmDock), "<SkipCollisionCheck>k__BackingField");

    private static readonly MethodInfo SetIsFaceBlockedMethod =
        AccessTools.PropertySetter(typeof(RoboticArmDock), "IsFaceBlocked");

    private static readonly ConditionalWeakTable<RoboticArmDock, SpeedState> DockSpeeds = new();
    private static readonly ConditionalWeakTable<RoboticArm, SpeedState> ArmSpeeds = new();

    private static float _configuredSpeed = 1f;

    internal static void ValidateGameApi()
    {
        if (ArmField == null || MoveSpeedField == null || SkipCollisionCheckField == null ||
            SetIsFaceBlockedMethod == null)
        {
            throw new MissingMemberException(
                "The installed Stationeers build does not expose the expected LArRE arm members.");
        }
    }

    internal static void ConfigureSpeed(float multiplier)
    {
        _configuredSpeed = SanitizeSpeed(multiplier);
    }

    internal static void InitializeDock(RoboticArmDock dock)
    {
        ApplySpeed(dock, _configuredSpeed);
        SkipCollisionCheckField.SetValue(dock, true);
        SetIsFaceBlockedMethod.Invoke(dock, new object[] { false });
    }

    internal static void DisableFaceObstruction(RoboticArmDock dock)
    {
        SetIsFaceBlockedMethod.Invoke(dock, new object[] { false });
    }

    internal static float GetDockSpeed(RoboticArmDock dock)
    {
        if (!DockSpeeds.TryGetValue(dock, out SpeedState state))
        {
            ApplySpeed(dock, _configuredSpeed);
            DockSpeeds.TryGetValue(dock, out state);
        }

        return state?.Multiplier ?? _configuredSpeed;
    }

    internal static float GetAnimationDelta(float deltaTime, RoboticArm arm)
    {
        return deltaTime * GetArmSpeed(arm);
    }

    internal static float GetBypassStepSpeed(RoboticArmDock dock)
    {
        return VanillaBypassSpeed * GetDockSpeed(dock);
    }

    internal static int GetCargoDelayMilliseconds(RoboticArmDockCargo cargoArm)
    {
        return Math.Max(
            1,
            (int)Math.Round(VanillaCargoDelayMilliseconds / GetDockSpeed(cargoArm)));
    }

    internal static void SerializeSpeed(RoboticArmDock dock, RocketBinaryWriter writer)
    {
        writer.WriteSingle(GetDockSpeed(dock));
    }

    internal static void DeserializeSpeed(RoboticArmDock dock, RocketBinaryReader reader)
    {
        ApplySpeed(dock, reader.ReadSingle());
    }

    private static void ApplySpeed(RoboticArmDock dock, float multiplier)
    {
        if (dock == null)
        {
            return;
        }

        float sanitized = SanitizeSpeed(multiplier);
        SpeedState state = DockSpeeds.GetOrCreateValue(dock);
        state.Multiplier = sanitized;
        MoveSpeedField.SetValue(dock, sanitized);

        if (ArmField.GetValue(dock) is not RoboticArm arm)
        {
            return;
        }

        ArmSpeeds.Remove(arm);
        ArmSpeeds.Add(arm, state);
    }

    private static float GetArmSpeed(RoboticArm arm)
    {
        return arm != null && ArmSpeeds.TryGetValue(arm, out SpeedState state)
            ? state.Multiplier
            : _configuredSpeed;
    }

    private static float SanitizeSpeed(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
        {
            return 1f;
        }

        return Mathf.Clamp(multiplier, MinimumSpeed, MaximumSpeed);
    }

    private sealed class SpeedState
    {
        internal float Multiplier = 1f;
    }
}

[HarmonyPatch(typeof(RoboticArmDock), "Awake")]
internal static class ArmInitializationPatch
{
    private static void Postfix(RoboticArmDock __instance)
    {
        ArmEnhancements.InitializeDock(__instance);
    }
}

[HarmonyPatch(typeof(RoboticArmDock), "CheckFaceBlocked")]
internal static class ArmFaceObstructionPatch
{
    private static bool Prefix(RoboticArmDock __instance)
    {
        ArmEnhancements.DisableFaceObstruction(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(RoboticArmDock), "SerializeOnJoin")]
internal static class ArmSpeedSerializePatch
{
    private static void Postfix(RoboticArmDock __instance, RocketBinaryWriter __0)
    {
        ArmEnhancements.SerializeSpeed(__instance, __0);
    }
}

[HarmonyPatch(typeof(RoboticArmDock), "DeserializeOnJoin")]
internal static class ArmSpeedDeserializePatch
{
    private static void Postfix(RoboticArmDock __instance, RocketBinaryReader __0)
    {
        ArmEnhancements.DeserializeSpeed(__instance, __0);
    }
}

[HarmonyPatch(typeof(RoboticArmDock), "MoveToBypass")]
internal static class ArmBypassSpeedPatch
{
    private static readonly MethodInfo GetBypassSpeedMethod =
        AccessTools.Method(typeof(ArmEnhancements), nameof(ArmEnhancements.GetBypassStepSpeed));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        bool replaced = false;
        foreach (CodeInstruction instruction in instructions)
        {
            if (!replaced && instruction.opcode == OpCodes.Ldc_R4 &&
                instruction.operand is float value && Math.Abs(value - 0.5f) < 0.0001f)
            {
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, GetBypassSpeedMethod);
                replaced = true;
                continue;
            }

            yield return instruction;
        }

        if (!replaced)
        {
            throw new InvalidOperationException("Could not locate the vanilla LArRE bypass speed constant.");
        }
    }
}

[HarmonyPatch]
internal static class ArmExtensionSpeedPatch
{
    private static readonly MethodInfo DeltaTimeGetter =
        AccessTools.PropertyGetter(typeof(Time), nameof(Time.deltaTime));

    private static readonly MethodInfo ScaleDeltaMethod =
        AccessTools.Method(typeof(ArmEnhancements), nameof(ArmEnhancements.GetAnimationDelta));

    private static MethodBase TargetMethod()
    {
        Type stateMachine = typeof(RoboticArm)
            .GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(type => type.Name.StartsWith("<AnimateAsync>", StringComparison.Ordinal));
        return stateMachine == null ? null : AccessTools.Method(stateMachine, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        FieldInfo ownerField = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            yield return instruction;
            if (!instruction.Calls(DeltaTimeGetter))
            {
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldfld, ownerField);
            yield return new CodeInstruction(OpCodes.Call, ScaleDeltaMethod);
            replaced = true;
        }

        if (!replaced || ownerField == null)
        {
            throw new InvalidOperationException("Could not locate the vanilla LArRE extension animation delta.");
        }
    }
}

[HarmonyPatch]
internal static class CargoArmCycleDelayPatch
{
    private static readonly MethodInfo GetDelayMethod =
        AccessTools.Method(typeof(ArmEnhancements), nameof(ArmEnhancements.GetCargoDelayMilliseconds));

    private static MethodBase TargetMethod()
    {
        Type stateMachine = typeof(RoboticArmDockCargo)
            .GetNestedTypes(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .SingleOrDefault(type => type.Name.StartsWith("<WaitThenSetActivate>", StringComparison.Ordinal));
        return stateMachine == null ? null : AccessTools.Method(stateMachine, "MoveNext");
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        FieldInfo ownerField = AccessTools.Field(__originalMethod.DeclaringType, "<>4__this");
        bool replaced = false;

        foreach (CodeInstruction instruction in instructions)
        {
            if (!replaced && instruction.opcode == OpCodes.Ldc_I4 &&
                instruction.operand is int value && value == 200)
            {
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldfld, ownerField);
                yield return new CodeInstruction(OpCodes.Call, GetDelayMethod);
                replaced = true;
                continue;
            }

            yield return instruction;
        }

        if (!replaced || ownerField == null)
        {
            throw new InvalidOperationException("Could not locate the vanilla Cargo Arm cycle delay.");
        }
    }
}

[HarmonyPatch(typeof(RoboticArmDockCargo), "DoHandOccupied")]
internal static class CargoArmStackingPatch
{
    private static bool Prefix(Slot __0, DynamicThing __1)
    {
        if (__0?.Get() is not IMergeable targetStack ||
            __1 is not IMergeable heldStack ||
            heldStack.IsStackFull ||
            !heldStack.CanStack(targetStack))
        {
            return true;
        }

        // Merge into the arm's held item, matching the requested direction. Any
        // overflow remains in the target slot when the held stack reaches its cap.
        OnServer.Merge(heldStack, targetStack);
        return false;
    }
}
