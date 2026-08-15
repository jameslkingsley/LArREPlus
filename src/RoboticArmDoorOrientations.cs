using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Pipes;
using Assets.Scripts.Objects.Structures;
using Assets.Scripts.Util;
using HarmonyLib;

namespace LarrePlus;

internal static class RoboticArmDoorOrientations
{
    private static readonly int[] AllAxisPermutation = { 0, 1, 2, 3, 4, 5 };

    internal static void EnableAllOrientations(RoboticArmDoor door)
    {
        if (door == null)
        {
            return;
        }

        door.RotationAxis = RotationAxis.All;
        door.AllowedRotations = AllowedRotations.All;
        door.ConnectionType = SmartRotate.ConnectionType.Exhaustive;

        if (!HasAllAxisPermutation(door.OpenEndsPermutation))
        {
            door.OpenEndsPermutation = (int[])AllAxisPermutation.Clone();
        }
    }

    private static bool HasAllAxisPermutation(int[] permutation)
    {
        if (permutation == null || permutation.Length != AllAxisPermutation.Length)
        {
            return false;
        }

        for (int index = 0; index < AllAxisPermutation.Length; index++)
        {
            if (permutation[index] != AllAxisPermutation[index])
            {
                return false;
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(Device), "Awake")]
internal static class RoboticArmDoorAwakePatch
{
    private static void Postfix(Device __instance)
    {
        if (__instance is RoboticArmDoor door)
        {
            RoboticArmDoorOrientations.EnableAllOrientations(door);
        }
    }
}

[HarmonyPatch(typeof(RoboticArmDoor), nameof(RoboticArmDoor.GetConnectionType))]
internal static class RoboticArmDoorConnectionTypePatch
{
    private static void Prefix(RoboticArmDoor __instance)
    {
        // GetConnectionType is the first operation performed by SmartRotate.
        // Reapply the setup here so prefabs or instances awakened before Harmony
        // installation are upgraded before their first placement rotation.
        RoboticArmDoorOrientations.EnableAllOrientations(__instance);
    }
}

[HarmonyPatch(typeof(RoboticArmDoor), nameof(RoboticArmDoor.GetOpenEndsPermutation))]
internal static class RoboticArmDoorPermutationPatch
{
    private static void Prefix(RoboticArmDoor __instance)
    {
        RoboticArmDoorOrientations.EnableAllOrientations(__instance);
    }
}
