# Changelog

All notable changes to LArRE+ are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project uses [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## 0.4.0 - 2026-08-15

### Added

- Placement of the vanilla Linear Rail Door in every grid orientation, including
  wall, floor, and ceiling planes.

## 0.3.2 - 2026-08-15

### Fixed

- Prevented the Cargo Large Arm tooltip from indexing beyond AIMeE's inventory
  while `TargetSlotIndex` is set to the whole-robot value of `50`.

## 0.3.1 - 2026-08-15

### Fixed

- Whole-AIMeE pickup now overrides the robot's actual draggable-object slot
  eligibility check, allowing it to enter the Cargo Large Arm hand.

### Changed

- Renamed the public-facing mod from Larre Plus to LArRE+ and refreshed its
  Workshop, LaunchPad, and in-game descriptions.

## 0.3.0 - 2026-08-15

### Added

- Whole-AIMeE pickup and release with a Cargo Large Arm using
  `TargetSlotIndex = 50`.
- Native server-authoritative transport of the live robot, preserving its
  battery, IC chip, cargo, identity, configuration, and power state.

## 0.2.2 - 2026-08-14

### Fixed

- Reloaded LaunchPad's saved configuration before applying the movement-speed
  multiplier, so pre-launch selections take effect at runtime.

## 0.2.1 - 2026-08-14

### Changed

- Exposed `MovementSpeedMultiplier` in LaunchPad's pre-launch Mod Configuration
  screen.

## 0.2.0 - 2026-08-14

### Added

- Matching-stack merging for Cargo Large Arms.
- A server-authoritative movement-speed multiplier for every LArRE arm.
- Unrestricted rail movement and extension beside obstructions for every LArRE
  arm.

## 0.1.1 - 2026-08-14

### Fixed

- Safely handled Cargo Large Arm target indices beyond AIMeE's slot count.

## 0.1.0 - 2026-08-14

### Added

- Cargo Large Arm access to AIMeE inventory slots.
- Server-authoritative AIMeE insertion, extraction, and swapping.
