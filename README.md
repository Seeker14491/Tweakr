# Tweakr

A [Spectrum](https://github.com/Ciastex/Spectrum) plugin that provides useful tweaks and cheats.

Cheats are disabled in multiplayer, and enabling them during a track stops you from getting a time on the leaderboards. Not all options are cheats; I've marked below which plugin options are considered cheats. All options in the in-game cheat menu are cheats except Bubble Car and Max Damage.

### Installation

This plugin requires Harmony; download the release [from here](https://github.com/pardeike/Harmony/releases) and put `0Harmony.dll` in `<game_dir>\Distance_Data\Managed`. Then download this plugin from [the releases](https://github.com/Seeker14491/Tweakr/releases) page, and put it in `<game_dir>\Distance_Data\Spectrum\Plugins`. Start the game to generate the plugin configuration file at `<game_dir>\Distance_Data\Spectrum\Settings\Tweakr.json`, and then close the game and edit it. Most things are disabled by default, and there are no hotkeys set up. The effects of most hotkeys last until death.

| Setting                        | Description                                                  |
| ------------------------------ | ------------------------------------------------------------ |
| enableCheatMenu                | Enables a built-in cheat menu that's normally hidden. It's accessible from the options menu. **Warning:** The Monster Truck cheat is buggy and leads to crashes. To prevent this, only turn it on while playing a track, and turn it back off before leaving the level. |
| carScreenDeclutter             | Hides the large arrow that appears on the back of the car when you go off-track, as well as the placement widget that appears in multiplayer sprint that shows what position you are in the race. The compass is also visible during the initial countdown. |
| disableWingGripControlModifier | Stops the grip button from affecting wing controls.          |
| disableWingSelfRightening | Stops wings from leveling out when not pressing a roll input. |
| checkpointHotkey               | **(cheat)** A hotkey to set the car's respawn position. It currently gets overwritten if you hit a course checkpoint. |
| infiniteCooldownHotkey         | **(cheat)** A hotkey to give infinite cooldown.              |
| allAbilitiesHotkey             | **(cheat)** A hotkey that enables all car abilities (boost, jump, wings, and jets). |
| noclipHotkey                   | **(cheat)** A hotkey that lets you drive through anything. It also stops you from triggering things like portals. If you want to trigger something, such as to go through a portal, you can set a checkpoint with checkpointHotkey, then reset. |
| disableJetRampdownHotkey       | **(cheat)** A hotkey that stops the car jets from losing power. |

### Defining Hotkeys

Hotkeys are defined in terms of ingame controls.

#### Examples of valid hotkey definitions:

`""`: hotkey disabled

`"Horn"`: active when Horn is input

`"Special+Horn"`: active when both Special and Horn inputs are active

You can chain as many inputs as you want using `+`. You can repeat the same combination for multiple settings, and they will all activate together.

#### Valid Inputs

```
Gas
Brake
SteerLeft
SteerRight
Boost
Jump
Wings
Special
WingRollLeft
WingRollRight
WingPitchDown
WingPitchUp
WingYawLeft
WingYawRight
JetRollLeft
JetRollRight
JetPitchDown
JetPitchUp
Grip
AirRollLeft
AirRollRight
AirPitchDown
AirPitchUp
Pause
Reset
ShowScore
Horn
ChangeCameraView
CenterCamera
CameraYawLeft
CameraYawRight
CameraPitchDown
CameraPitchUp
SpectateNextPlayer
MenuLeft
MenuRight
MenuDown
MenuUp
MenuPageLeft
MenuPageRight
MenuPageDown
MenuPageUp
MenuConfirm
MenuCancel
MenuDelete
MenuStart
Chat
MenuCreatePlaylist
MenuRateLevel
MenuSorting
CameraLookBehind
ToggleMenuVisibility
```
