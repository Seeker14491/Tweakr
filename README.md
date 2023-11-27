# Tweakr

A mod for the game [Distance](http://survivethedistance.com/) that provides useful tweaks and cheats. It requires the [BepInEx 5](https://github.com/BepInEx/BepInEx) plugin framework.

**A note on cheats:** Cheats are disabled in multiplayer, and enabling them during a track stops you from setting a time on the leaderboards. Not all options are cheats; I've marked below which plugin options are considered cheats.

### Installation

The plugin requires BepInEx 5 to be installed in Distance (BepInEx cannot be installed together with Centrifuge). Downloads for that are [on this page](https://github.com/BepInEx/BepInEx/releases). You need the file named like `BepInEx_x86_5.x.x.x.zip`. Then just extract the contents next to `Distance.exe`.

Download the plugin from [the releases](https://github.com/Seeker14491/Tweakr/releases) page, and extract the `Tweakr` folder from the archive to `<game_dir>\BepInEx\plugins` such that the plugin dll ends up at `<game_dir>\BepInEx\plugins\Tweakr\Tweakr.dll`. Start the game to generate the plugin configuration file at `<game_dir>\BepInEx\config\pw.seekr.plugins.tweakr.cfg`, and then close the game and edit it. Most things are disabled by default, and there are no hotkeys set up. The effects of most hotkeys last until death.

| Setting | Description |
| --- | --- |
| carScreenDeclutter | Hides the large arrow that appears on the back of the car when you go off-track, as well as the placement widget that appears in multiplayer sprint that shows what position you are in the race. The compass is also visible during the initial countdown. |
| disableWingGripControlModifier | Stops the grip button from affecting wing controls. |
| disableWingSelfRightening | Stops wings from leveling out when not pressing a roll input. It disables Flight Landing Assist as a side-effect. |
| showLocalLeaderboardResultTimestamps | Enables showing the date and time of each result in your local leaderboard. These timestamps are viewable both from the level select leaderboard menu, as well as the end-of-level results menu. |
| disableLocalLeaderboardResultLimit | Normally the game only keeps up to 20 results on the local leaderboard per level, deleting the slowest time to make room. This setting disables the limit. Note that if this setting is not enabled (or this plugin is not loaded), and you have more than 20 results on a level, the game will delete all the extras when you set a time on that level. |
| enableShiftClickMultiselectLeaderboardEntries | Lets you shift-click to select or deselect a range of leaderboard entries. |
| removeReplayPlaybackLimit | Lets you view or race against more than 20 ghosts/replays. Expect increasing lag as you enable more replays. 600 replays seems to be about the safe limit before risking a crash (though you'll have single-digit FPS). See also the `carLevelOfDetailCap` setting, which you can use to make sure cars further away remain visible. |
| downloadAllLeaderboardEntries | When viewing global leaderboards, downloads all entries instead of just the top 1000. |
| checkpointHotkey | **(cheat)** A hotkey to set the car's respawn position. It currently gets overwritten if you hit a course checkpoint. |
| infiniteCooldownHotkey | **(cheat)** A hotkey to give infinite cooldown. |
| allAbilitiesHotkey | **(cheat)** A hotkey that enables all car abilities (boost, jump, wings, and jets). |
| noclipHotkey | **(cheat)** A hotkey that lets you drive through anything. It also stops you from triggering things like portals. If you want to trigger something, such as to go through a portal, you can set a checkpoint with checkpointHotkey, then reset. |
| disableJetRampdownHotkey | **(cheat)** A hotkey that stops the car jets from losing power. |

Each rendered car has a "level of detail" (LOD) that controls the quality of the car's visuals. As the camera gets further away from a particular car, the LOD of that car will decrease. If the camera gets far enough away, the car won't be rendered at all. The following setting lets you set a higher minimum LOD, so you can keep cars visible and with more detail.

| Setting             | Description               |
| ------------------- | ------------------------- |
| carLevelOfDetailCap | Caps the min LOD of cars. |

These are the valid options, in order from most detail to least detail:

```
"InFocusFP"
"InFocus"
"Near"
"Medium"
"Far"
"VeryFar"
"Speck"
```

`"Speck"` is the default and has no effect. Use `"VeryFar"` if you just want to keep cars visible regardless of how far away they are.

---

The following is a pair of hotkeys for saving and restoring the car state. It's a work-in-progress; a list of what currently is and isn't is below.

| Setting | Description |
| --- | --- |
| carStateSaveHotkey | A hotkey that stores various aspects of the car, to be later restored by `carStateLoadHotkey`. |
| carStateLoadHotkey | **(cheat)** A hotkey that loads the car state last saved by `carStateSaveHotkey`. |

The following is currently saved:

- Position
- Velocity
- Angular velocity
- Has infinite cooldown
- Heat level
- Is gravity enabled
- Jets falloff

Things that are still missing:

- Are wings out
- Respawn point
- What checkpoints have been hit
- Everything trick-related
- Camera state
- Shape of the car after being sliced
- The load hotkey doesn't work during the death animation

### Defining Hotkeys

Hotkeys are defined in terms of ingame controls.

#### Examples of valid hotkey definitions:

` `: hotkey disabled

`Horn`: active when Horn is input

`Special+Horn`: active when both Special and Horn inputs are active

Here's a whole config line as an example:

```
carStateSaveHotkey = Special+ShowScore
```

You can chain as many inputs as you want using `+`. You can repeat the same combination for multiple settings, and they will all activate together.

#### Valid Inputs

Note: I have not tested all of these; some may not work.

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
