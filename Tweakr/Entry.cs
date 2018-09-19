﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Spectrum.API.Configuration;
using Spectrum.API.Interfaces.Plugins;
using Spectrum.API.Interfaces.Systems;
using Harmony;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable ArrangeTypeMemberModifiers
// ReSharper disable InconsistentNaming
// ReSharper disable ClassNeverInstantiated.Global

namespace Tweakr
{
    public class Entry : IPlugin, IUpdatable
    {
        public static Settings Settings;
        private static readonly Dictionary<string, InputAction[]> Hotkeys = new Dictionary<string, InputAction[]>();

        private static readonly MethodInfo[] AbilityMethods = {
            AccessTools.Method(typeof(PlayerDataLocal), "SetAbilityEnabled", null, new[] {typeof(BoostGadget)}),
            AccessTools.Method(typeof(PlayerDataLocal), "SetAbilityEnabled", null, new[] {typeof(JumpGadget)}),
            AccessTools.Method(typeof(PlayerDataLocal), "SetAbilityEnabled", null, new[] {typeof(WingsGadget)}),
            AccessTools.Method(typeof(PlayerDataLocal), "SetAbilityEnabled", null, new[] {typeof(JetsGadget)})
        };

        internal static bool Cheated;
        internal static bool HasSetCheckpoint;
        internal static bool JetRampdownDisabled;
        internal static bool AllAbilitiesEnabled;
        internal static bool Noclip;

        private const bool AllowGameplayCheatsInMultiplayer = false;

        public void Initialize(IManager manager, string ipcIdentifier)
        {
            Settings = InitializeSettings();

            var harmony = HarmonyInstance.Create("com.seekr.tweakr");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void Update()
        {
            if (Settings.GetItem<bool>("carScreenDeclutter"))
            {
                var carScreenLogic = G.Sys.PlayerManager_?.Current_?.playerData_?.CarScreenLogic_;
                if (carScreenLogic?.CarLogic_.IsLocalCar_ ?? false)
                {
                    carScreenLogic.arrow_ = carScreenLogic.compass_;
                    if (Traverse.Create(carScreenLogic.placementText_).Field("renderer_").GetValue() != null)
                    {
                        carScreenLogic.placementText_.IsVisible_ = false;
                    }

                    carScreenLogic.ModeWidgetVisible_ = true;
                }
            }

            if (!MultiplayerCheatShouldContinue())
            {
                return;
            }

            {
                var transform = G.Sys.PlayerManager_?.Current_?.playerData_?.CarLogic_?.transform;
                if (IsTriggered(Settings.GetItem<string>("checkpointHotkey")) && transform != null)
                {
                    G.Sys.PlayerManager_?.Current_?.playerData_?.SetResetTransform(transform.position,
                        transform.rotation);
                    HasSetCheckpoint = true;
                }
            }

            if (IsTriggered(Settings.GetItem<string>("infiniteCooldownHotkey")))
            {
                G.Sys.PlayerManager_?.Current_?.playerData_?.CarLogic_?.SetInfiniteCooldown(true);
                Cheated = true;
            }

            if (IsTriggered(Settings.GetItem<string>("allAbilitiesHotkey")))
            {
                var playerDataLocal = G.Sys.PlayerManager_?.Current_?.playerData_;
                if (playerDataLocal != null)
                {
                    foreach (var methodInfo in AbilityMethods)
                    {
                        methodInfo.Invoke(playerDataLocal, new object[] {true, true});
                    }

                    AllAbilitiesEnabled = true;
                    Cheated = true;
                }
            }

            if (IsTriggered(Settings.GetItem<string>("disableJetRampdownHotkey")))
            {
                JetRampdownDisabled = true;
                Cheated = true;
            }

            if (IsTriggered(Settings.GetItem<string>("noclipHotkey")))
            {
                var carRigidBody = G.Sys.PlayerManager_?.Current_?.playerData_?.CarLogic_?.CarController_?.Rigidbody_;
                if (carRigidBody != null)
                {
                    carRigidBody.detectCollisions = false;
                    Noclip = true;
                    Cheated = true;
                }
            }
        }

        private static Settings InitializeSettings()
        {
            var settings = new Settings("settings");

            var entries = new[]
            {
                new SettingsEntry("carScreenDeclutter", false),
                new SettingsEntry("disableWingGripControlModifier", false),
                new SettingsEntry("disableWingSelfRightening", false),
                new SettingsEntry("checkpointHotkey", ""),
                new SettingsEntry("infiniteCooldownHotkey", ""),
                new SettingsEntry("allAbilitiesHotkey", ""),
                new SettingsEntry("noclipHotkey", ""),
                new SettingsEntry("disableJetRampdownHotkey", "")
            };

            foreach (var s in entries)
            {
                if (!settings.ContainsKey(s.Name))
                {
                    settings.Add(s.Name, s.DefaultVal);
                }
            }

            settings.Save();
            return settings;
        }

        private static InputAction[] ParseHotkey(string keys)
        {
            if (keys == "")
            {
                return new InputAction[] { };
            }

            return keys
                .Split('+')
                .Select(x => (InputAction) Enum.Parse(typeof(InputAction), x, true))
                .ToArray();
        }

        private static bool IsTriggered(string hotkey)
        {
            if (!Hotkeys.TryGetValue(hotkey, out var v))
            {
                v = ParseHotkey(hotkey);
                Hotkeys.Add(hotkey, v);
            }

            return IsTriggered(v);
        }

        private static bool IsTriggered(ICollection<InputAction> hotkey)
        {
            if (hotkey.Count == 0)
            {
                return false;
            }

            return hotkey
                .Select(x =>
                    G.Sys.PlayerManager_?.Current_?.playerData_?.InputStates_.GetPressed(x) ??
                    false)
                .All(x => x);
        }

        internal static bool MultiplayerCheatShouldContinue()
        {
            return AllowGameplayCheatsInMultiplayer || !(G.Sys.NetworkingManager_?.IsOnline_ ?? false);
        }
    }

    internal class SettingsEntry
    {
        public readonly string Name;
        public readonly object DefaultVal;

        public SettingsEntry(string name, object defaultVal)
        {
            Name = name;
            DefaultVal = defaultVal;
        }
    }

    [HarmonyPatch(typeof(CheatsManager))]
    [HarmonyPatch("GameplayCheatsUsedThisLevel_", PropertyMethod.Getter)]
    internal class BlockLeaderboardUpdatingWhenCheating
    {
        static void Postfix(ref bool __result)
        {
            __result |= Entry.Cheated;
        }
    }

    [HarmonyPatch(typeof(CarLogic), "OnEventCarDeath")]
    internal class PatchCarDeath
    {
        static void Postfix()
        {
            if (Entry.HasSetCheckpoint)
            {
                Entry.Cheated = true;
            }

            Entry.JetRampdownDisabled = false;
            Entry.AllAbilitiesEnabled = false;
            Entry.Noclip = false;
        }
    }

    [HarmonyPatch(typeof(GameManager), "SceneLoaded")]
    internal class PatchSceneLoaded
    {
        static void Postfix()
        {
            Entry.Cheated = false;
            Entry.HasSetCheckpoint = false;

            Entry.JetRampdownDisabled = false;
            Entry.AllAbilitiesEnabled = false;
            Entry.Noclip = false;
        }
    }

    [HarmonyPatch(typeof(WingsGadget), "GadgetUpdateLocal")]
    internal class DisableWingGripControlModifier
    {
        static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableWingGripControlModifier");
        }

        static bool Prefix(InputStates inputStates)
        {
            inputStates.Clear(InputAction.Grip);
            return true;
        }
    }

    [HarmonyPatch(typeof(JetsGadget), "GadgetFixedUpdate")]
    internal class DisableJetsRampdown
    {
        static bool Prefix(JetsGadget __instance)
        {
            if (Entry.JetRampdownDisabled)
            {
                Traverse.Create(__instance).Field("thrusterBoostTimer_").SetValue(0f);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Gadget), "SetAbilityEnabled")]
    internal class BlockAbilityDisabling
    {
        static bool Prefix(ref bool enable)
        {
            if (Entry.AllAbilitiesEnabled)
            {
                enable = true;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AntiTunneler), "CheckForTunneling")]
    internal class DisableTunnelingPrevention
    {
        static bool Prefix()
        {
            return !Entry.Noclip;
        }
    }

    [HarmonyPatch(typeof(WingsGadget), "GadgetUpdateLocal")]
    internal class DisableWingSelfRightening
    {
        static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableWingSelfRightening");
        }

        // Ensures this branch is not taken:
        //
        // if (Mathf.Abs(carDirectives_.Roll_) < 0.25f)
        //
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            foreach (var codeInstruction in instr)
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (codeInstruction.opcode == OpCodes.Ldc_R4 && (float) codeInstruction.operand == 0.25)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.0);
                }
                else
                {
                    yield return codeInstruction;
                }
            }
        }
    }
}
