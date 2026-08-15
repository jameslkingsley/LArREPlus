# Contributing

Bug reports and focused pull requests are welcome.

## Development setup

1. Install Stationeers, BepInEx 5.4, and StationeersLaunchPad.
2. Clone this repository into a convenient development directory.
3. Build with `dotnet build --configuration Release`, overriding
   `StationeersPath` when necessary.
4. Copy or link the LaunchPad payload into
   `Documents\My Games\Stationeers\mods` if the checkout is elsewhere.

Stationeers and dependency assemblies must not be committed.

## Before submitting a change

- Run a Release build with zero warnings and errors.
- Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ValidateRepository.ps1`.
- Test Cargo Large Arm insertion, extraction, swapping, and stack merging.
- Test valid and out-of-range AIMeE slot indices, including hidden cargo slots.
- Test whole-AIMeE pickup, rail transport, release, moving-target cancellation,
  save/reload while carried, and preservation of all nested inventory.
- Test rail, bypass, extension, retraction, and cargo-cycle speed changes.
- Test arm movement beside walls with collision and face obstruction removed.
- Test save/reload, a listen server, and a dedicated server with a connected
  client using the same mod version.

## Bug reports

Include the Stationeers build, Larre Plus version, BepInEx and LaunchPad
versions, multiplayer environment, reproduction steps, and the smallest useful
`[LarrePlus]` log excerpt.
