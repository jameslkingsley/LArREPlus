using System;
using System.Reflection;
using Assets.Scripts.GridSystem;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Cysharp.Threading.Tasks;
using HarmonyLib;
using Objects.RoboticArm;
using UnityEngine;

namespace LarrePlus;

internal static class AimeeCargoArmCompatibility
{
    // The vanilla arm probes 1.75 metres along the arm's downward axis.
    private const float InteractionDistance = 1.75f;
    private const float LateralTolerance = 0.8f;
    private const float AxialTolerance = 1.0f;

    private static readonly FieldInfo ArmField =
        AccessTools.Field(typeof(RoboticArmDock), "_arm");

    private static readonly FieldInfo TargetLogicableField =
        AccessTools.Field(typeof(RoboticArmDockCargo), "_targetLogicable");

    private static readonly FieldInfo CurrentSlotIndexField =
        AccessTools.Field(typeof(RoboticArmDockCargo), "_currentSlotIndex");

    private static readonly MethodInfo GetArmInteractionCellMethod =
        AccessTools.Method(typeof(RoboticArmDock), "GetArmInteractionCell");

    private static readonly MethodInfo SetTargetLogicableMethod =
        AccessTools.PropertySetter(typeof(RoboticArmDockCargo), "TargetLogicable");

    private static readonly MethodInfo GetHandSlotMethod =
        AccessTools.PropertyGetter(typeof(RoboticArmDockCargo), "HandSlot");

    private static readonly MethodInfo DoHandOccupiedMethod =
        AccessTools.Method(typeof(RoboticArmDockCargo), "DoHandOccupied");

    private static readonly MethodInfo DoHandEmptyMethod =
        AccessTools.Method(typeof(RoboticArmDockCargo), "DoHandEmpty");

    private static readonly MethodInfo WaitThenSetActivateMethod =
        AccessTools.Method(typeof(RoboticArmDockCargo), "WaitThenSetActivate");

    internal static void ValidateGameApi()
    {
        if (ArmField == null || TargetLogicableField == null || CurrentSlotIndexField == null ||
            GetArmInteractionCellMethod == null || SetTargetLogicableMethod == null ||
            GetHandSlotMethod == null || DoHandOccupiedMethod == null ||
            DoHandEmptyMethod == null || WaitThenSetActivateMethod == null)
        {
            throw new MissingMemberException(
                "The installed Stationeers build does not expose the expected Cargo Large Arm members.");
        }
    }

    internal static bool TryTargetAimee(RoboticArmDockCargo cargoArm)
    {
        if (cargoArm.CurrentBypass != null)
        {
            return false;
        }

        SmallCell cell = (SmallCell)GetArmInteractionCellMethod.Invoke(cargoArm, null);
        if (cell?.Device != null)
        {
            return false;
        }

        RobotMining robot = FindNearestAimee(cargoArm);
        if (robot == null)
        {
            return false;
        }

        ILogicable currentTarget = (ILogicable)TargetLogicableField.GetValue(cargoArm);
        if (!ReferenceEquals(currentTarget, robot))
        {
            LarrePlusMod.Log(
                $"Cargo arm {cargoArm.ReferenceId} targeted AIMeE {robot.ReferenceId}.");
        }

        // Vanilla refreshes this target every server tick. Keep that behaviour so
        // clients joining after acquisition receive the target reference as well.
        SetTargetLogicableMethod.Invoke(cargoArm, new object[] { robot });

        return true;
    }

    internal static bool TryHandleAimeeTransfer(RoboticArmDockCargo cargoArm)
    {
        if (TargetLogicableField.GetValue(cargoArm) is not RobotMining robot)
        {
            return false;
        }

        try
        {
            if (!IsAimeeInReach(cargoArm, robot))
            {
                SetTargetLogicableMethod.Invoke(cargoArm, new object[] { null });
                return true;
            }

            int slotIndex = (int)CurrentSlotIndexField.GetValue(cargoArm);
            if (slotIndex < 0 || robot.Slots == null || slotIndex >= robot.Slots.Count)
            {
                return true;
            }

            Slot targetSlot = robot.Slots[slotIndex];
            if (!CanAccessAimeeSlot(targetSlot))
            {
                return true;
            }

            Slot handSlot = (Slot)GetHandSlotMethod.Invoke(cargoArm, null);
            if (handSlot == null)
            {
                return true;
            }

            if (handSlot.Contains(out DynamicThing heldThing))
            {
                DoHandOccupiedMethod.Invoke(cargoArm, new object[] { targetSlot, heldThing });
            }
            else
            {
                DoHandEmptyMethod.Invoke(cargoArm, new object[] { targetSlot });
            }

            return true;
        }
        catch (Exception exception)
        {
            LarrePlusMod.LogError("AIMeE cargo transfer failed", Unwrap(exception));
            return true;
        }
        finally
        {
            FinishActivation(cargoArm);
        }
    }

    internal static bool CanAccessAimeeSlot(Slot slot)
    {
        if (slot == null || slot.Parent is not RobotMining || slot.IsLocked ||
            slot.Type == Slot.Class.Plant)
        {
            return false;
        }

        // AIMeE cargo slots deliberately hide their occupants behind the contents
        // panel. Cargo automation needs those slots even when they are not directly
        // interactable by the player's cursor.
        return slot.IsInteractable || slot.HidesOccupant;
    }

    internal static bool IsUnsupportedAimeeProxySlot(
        RoboticArmDockCargo cargoArm,
        int requestedSlotIndex)
    {
        if (requestedSlotIndex != RoboticArmDockCargo.PROXY_SLOT_ID ||
            TargetLogicableField.GetValue(cargoArm) is not RobotMining robot)
        {
            return false;
        }

        int targetSlotIndex = (int)CurrentSlotIndexField.GetValue(cargoArm);
        return targetSlotIndex < 0 || robot.Slots == null ||
               targetSlotIndex >= robot.Slots.Count;
    }

    private static RobotMining FindNearestAimee(RoboticArmDockCargo cargoArm)
    {
        RobotMining nearest = null;
        float nearestScore = float.MaxValue;

        foreach (RobotMining robot in RobotMining.AllRobots)
        {
            if (robot == null || !robot.gameObject.activeInHierarchy ||
                !TryGetReachScore(cargoArm, robot, out float score) || score >= nearestScore)
            {
                continue;
            }

            nearest = robot;
            nearestScore = score;
        }

        return nearest;
    }

    private static bool IsAimeeInReach(RoboticArmDockCargo cargoArm, RobotMining robot)
    {
        return robot != null && robot.gameObject.activeInHierarchy &&
               TryGetReachScore(cargoArm, robot, out _);
    }

    private static bool TryGetReachScore(
        RoboticArmDockCargo cargoArm,
        RobotMining robot,
        out float score)
    {
        score = float.MaxValue;
        RoboticArm arm = (RoboticArm)ArmField.GetValue(cargoArm);
        if (arm == null)
        {
            return false;
        }

        Vector3 direction = -arm.transform.up.normalized;
        Vector3 interactionPoint = arm.transform.position + direction * InteractionDistance;
        Vector3 delta = robot.ThingTransformPosition - interactionPoint;
        float signedAxialDistance = Vector3.Dot(delta, direction);
        float axialDistance = Mathf.Abs(signedAxialDistance);
        Vector3 lateralDelta = delta - direction * signedAxialDistance;
        float lateralDistance = lateralDelta.magnitude;

        if (axialDistance > AxialTolerance || lateralDistance > LateralTolerance)
        {
            return false;
        }

        score = lateralDistance * lateralDistance + axialDistance * axialDistance;
        return true;
    }

    private static void FinishActivation(RoboticArmDockCargo cargoArm)
    {
        try
        {
            object result = WaitThenSetActivateMethod.Invoke(cargoArm, null);
            if (result is UniTaskVoid task)
            {
                task.Forget();
            }
        }
        catch (Exception exception)
        {
            LarrePlusMod.LogError("Could not reset the Cargo Large Arm activation", Unwrap(exception));
        }
    }

    private static Exception Unwrap(Exception exception)
    {
        return exception is TargetInvocationException { InnerException: not null } invocation
            ? invocation.InnerException
            : exception;
    }
}

[HarmonyPatch(typeof(RoboticArmDockCargo), "SetTargetSmallGrid")]
internal static class CargoArmAimeeTargetPatch
{
    private static bool Prefix(RoboticArmDockCargo __instance)
    {
        try
        {
            // Skip vanilla target discovery only while an AIMeE is occupying an
            // otherwise empty interaction position. Vanilla devices keep priority.
            return !AimeeCargoArmCompatibility.TryTargetAimee(__instance);
        }
        catch (Exception exception)
        {
            LarrePlusMod.LogError("AIMeE target discovery failed", exception);
            return true;
        }
    }
}

[HarmonyPatch(typeof(RoboticArmDockCargo), "DoContextualAction")]
internal static class CargoArmAimeeTransferPatch
{
    private static bool Prefix(RoboticArmDockCargo __instance)
    {
        // A true result means this was an AIMeE operation and vanilla must not run.
        return !AimeeCargoArmCompatibility.TryHandleAimeeTransfer(__instance);
    }
}

[HarmonyPatch(typeof(RoboticArmDockCargo), "CanAccessSlot")]
internal static class CargoArmAimeeSlotPatch
{
    private static void Postfix(Slot __0, ref bool __result)
    {
        if (!__result && AimeeCargoArmCompatibility.CanAccessAimeeSlot(__0))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(RoboticArmDockCargo), "GetSlot")]
internal static class CargoArmAimeeProxySlotBoundsPatch
{
    private static bool Prefix(
        RoboticArmDockCargo __instance,
        int __0,
        ref Slot __result)
    {
        if (!AimeeCargoArmCompatibility.IsUnsupportedAimeeProxySlot(__instance, __0))
        {
            return true;
        }

        // Vanilla forwards proxy slot 255 to the selected target index without a
        // bounds check. Returning null matches its normal inaccessible-slot result
        // and lets SetIcon hide the preview safely.
        __result = null;
        return false;
    }
}
