# Changelog

All notable changes to Fast Grenade Throw are documented here. Format follows [Keep a Changelog](https://keepachangelog.com/); versions follow [SemVer](https://semver.org/).

## [Unreleased]
### Fixed
- Debug log lines (F12 toggle) now emit at Info level so they are visible at default BepInEx console/log settings

## [1.0.4] - 2026-07-01
### Fixed
- Phantom grenade throws and inventory desync when throwing from rig, pockets, armband, or backpack
- Grenades not being removed from inventory after throwing
- Wrong grenade being selected (HUD showing one grenade, another being thrown)
### Added
- Support for grenades stored in the armband slot (belt equipment mods) and backpack
- Automatic detection of Use Items Anywhere with conflict-free hooking
- Debug logging toggle in F12 menu

## [1.0.3] - 2026-06-29
### Fixed
- Model 7290 Flash Bang releasing from waist level during quick throw — now releases from hand height like all other grenades

## [1.0.2] - 2026-06-28
### Fixed
- Weapon not returning after spamming the throw key
- Version display mismatch in the F12 menu

## [1.0.1] - 2026-06-28
### Fixed
- Quick throw not working while walking (WASD)
- Grenade search now works with modded equipment slots (belts, custom rigs, etc.)
### Added
- Guard against double-trigger conflicts

## [1.0.0] - 2026-06-27
### Added
- Initial release — instant grenade throws with dedicated overhand and underhand keybinds
