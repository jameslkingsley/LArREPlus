# Larre Plus

Larre Plus is a [StationeersLaunchPad](https://stationeerslaunchpad.github.io/)
plugin mod that expands the capabilities of the game's LArRE robotic arms.

Cargo Large Arms can access an AIMeE parked beneath their interaction position,
carry the complete live robot, merge matching stacks instead of swapping them,
and use AIMeE's hidden cargo slots. Every LArRE arm also gains configurable
movement speed and unrestricted movement beside obstructions.

## Features

- Lets Cargo Large Arms insert into, extract from, and swap items with AIMeE.
- Uses the arm's selected slot index as AIMeE's actual inventory index.
- Preserves vanilla stationary-device priority over nearby AIMeE robots.
- Safely denies indices outside AIMeE's available slot range.
- Allows access to AIMeE's hidden cargo slots.
- Picks up, transports, and releases a complete constructed AIMeE without
  replacing or deconstructing it.
- Preserves the carried robot's battery, IC chip, cargo, reference ID, name,
  configuration, and power state.
- Merges matching target stacks into the item held by a Cargo Large Arm; overflow
  remains in the target slot when the held stack reaches its capacity.
- Removes rail collision and extension-face obstruction limits from all LArRE
  arms.
- Applies a configurable `0.1`-to-`10` multiplier to rail travel, bypass
  movement, extension, retraction, and the Cargo Large Arm transfer delay.
- Synchronizes the server's speed multiplier to connected clients.

## Requirements

- Stationeers
- BepInEx 5.4
- StationeersLaunchPad

The server and every connecting client must install the same Larre Plus version.
The mod extends the arm join-data format to synchronize movement speed, so mixed
versions are not supported.

## Installation

1. Install BepInEx and StationeersLaunchPad.
2. Extract the release archive into `Documents\My Games\Stationeers\mods`.
3. Confirm the layout contains `LarrePlus\About\About.xml` and
   `LarrePlus\LarrePlus.dll`.
4. Enable Larre Plus in LaunchPad and restart Stationeers.

Successful startup writes messages beginning with `[LarrePlus]` to `Player.log`.

## Cargo Large Arm and AIMeE

Leave the Cargo Large Arm's ordinary target position free and park AIMeE beneath
the arm. Select an AIMeE inventory slot using the arm's slot-index control or
logic, then activate normally:

- An empty arm extracts from the selected AIMeE slot.
- An occupied arm inserts, swaps, or merges with the selected slot.
- An invalid or inaccessible slot is denied safely.

The target is checked again when the arm reaches the transfer point. Moving
AIMeE out of reach cancels the transfer.

### Transporting the complete robot

Set the Cargo Large Arm's `TargetSlotIndex` to `50`. This upper vanilla index is
reserved by Larre Plus for whole-AIMeE transport:

- With an empty hand and an AIMeE beneath the arm, activation picks up the live
  robot.
- Move the arm along its rail while AIMeE remains in the hand slot.
- Activate again at another position to release AIMeE beneath the arm.

```ic10
s d0 TargetSlotIndex 50
s d0 Activate 1
```

Indices `0` through `49` retain their existing meaning as AIMeE inventory-slot
indices. Pickup is revalidated at the transfer point, so an AIMeE that drives
out of reach before the arm arrives is not collected. Version 0.3.0 deliberately
does not check whether the release position is obstructed.

## Movement speed

Open LaunchPad's pre-launch **Mod Configuration** screen and set:

`LArRE Arms / MovementSpeedMultiplier`

The range is `0.1` to `10`; the default is `1`. Restart after changing it. On a
dedicated server, the server's setting is authoritative and is sent to clients
when each arm joins the world. The value is also stored in
`BepInEx/config/com.james.larreplus.cfg` for server administration.

## Multiplayer

Inventory changes use Stationeers' server-authoritative operations. AIMeE
targeting and transfer execution occur on the simulation authority, while the
server's configured arm speed is serialized to clients so they observe matching
animations.

Use the same Larre Plus DLL on the server and every client. Test new releases on
a disposable save before using them on an important multiplayer world.

## Building

Build against an installed copy of Stationeers:

```powershell
dotnet build .\LarrePlus.csproj --configuration Release
```

Override the default installation path when necessary:

```powershell
dotnet build .\LarrePlus.csproj --configuration Release `
  -p:StationeersPath="D:\Games\Stationeers"
```

A successful build copies the loadable DLL into the mod root. Create a minimal
release archive with:

```powershell
.\scripts\Package.ps1
```

## License

Larre Plus is available under the [MIT License](LICENSE).
