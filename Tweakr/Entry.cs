using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using JetBrains.Annotations;
using Spectrum.API.Configuration;
using Spectrum.API.Interfaces.Plugins;
using Spectrum.API.Interfaces.Systems;

namespace Tweakr
{
    [UsedImplicitly]
    public class Entry : IPlugin, IUpdatable
    {
        public static Settings Settings;
        private static readonly Dictionary<string, InputAction[]> Hotkeys = new Dictionary<string, InputAction[]>();

        private static readonly MethodInfo[] AbilityMethods =
        {
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

        private static InputStates _inputStates;
        private static NetworkingManager _networkingManager;

        [PublicAPI] public static bool AllowGameplayCheatsInMultiplayer;

        public void Initialize(IManager manager, string ipcIdentifier)
        {
            Settings = InitializeSettings();

            var harmony = HarmonyInstance.Create("com.seekr.tweakr");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void Update()
        {
            _networkingManager = G.Sys.NetworkingManager_;
            var playerManager = G.Sys.PlayerManager_;
            var localPlayer = playerManager ? playerManager.Current_ : null;
            var playerDataLocal = localPlayer?.playerData_;
            _inputStates = playerDataLocal ? playerDataLocal.InputStates_ : null;
            var carLogic = playerDataLocal ? playerDataLocal.CarLogic_ : null;
            var carController = carLogic ? carLogic.CarController_ : null;
            var rigidbody = carController ? carController.Rigidbody_ : null;

            if (Settings.GetItem<bool>("carScreenDeclutter"))
            {
                var carScreenLogic = playerDataLocal ? playerDataLocal.CarScreenLogic_ : null;
                var carScreenLogicCarLogic = carScreenLogic ? carScreenLogic.CarLogic_ : null;
                if (carScreenLogicCarLogic && carScreenLogicCarLogic.IsLocalCar_)
                {
                    carScreenLogic.arrow_ = carScreenLogic.compass_;
                    if (Traverse.Create(carScreenLogic.placementText_).Field("renderer_").GetValue() != null)
                    {
                        carScreenLogic.placementText_.IsVisible_ = false;
                    }

                    carScreenLogic.ModeWidgetVisible_ = true;
                }
            }

            if (!GameplayCheatsAllowed())
            {
                return;
            }

            {
                var transform = carLogic ? carLogic.transform : null;
                if (IsTriggered(Settings.GetItem<string>("checkpointHotkey")) && transform)
                {
                    if (playerDataLocal)
                    {
                        playerDataLocal.SetResetTransform(transform.position, transform.rotation);
                    }

                    HasSetCheckpoint = true;
                }
            }

            if (IsTriggered(Settings.GetItem<string>("infiniteCooldownHotkey")))
            {
                if (carLogic)
                {
                    carLogic.SetInfiniteCooldown(true);
                }

                Cheated = true;
            }

            if (IsTriggered(Settings.GetItem<string>("allAbilitiesHotkey")))
            {
                if (playerDataLocal)
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
                if (rigidbody)
                {
                    rigidbody.detectCollisions = false;
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
                .Select(x => _inputStates.GetPressed(x))
                .All(x => x);
        }

        private static bool GameplayCheatsAllowed()
        {
            return AllowGameplayCheatsInMultiplayer || !(_networkingManager && _networkingManager.IsOnline_);
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

    [UsedImplicitly]
    [HarmonyPatch(typeof(CheatsManager))]
    [HarmonyPatch("GameplayCheatsUsedThisLevel_", PropertyMethod.Getter)]
    internal static class BlockLeaderboardUpdatingWhenCheating
    {
        // ReSharper disable once InconsistentNaming
        [UsedImplicitly]
        private static void Postfix(ref bool __result)
        {
            __result |= Entry.Cheated;
        }
    }

    [HarmonyPatch(typeof(CarLogic), "OnEventCarDeath")]
    internal static class PatchCarDeath
    {
        [UsedImplicitly]
        private static void Postfix()
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
    internal static class PatchSceneLoaded
    {
        [UsedImplicitly]
        private static void Postfix()
        {
            Entry.Cheated = false;
            Entry.HasSetCheckpoint = false;

            Entry.JetRampdownDisabled = false;
            Entry.AllAbilitiesEnabled = false;
            Entry.Noclip = false;
        }
    }

    [HarmonyPatch(typeof(WingsGadget), "GadgetUpdateLocal")]
    internal static class DisableWingGripControlModifier
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableWingGripControlModifier");
        }

        [UsedImplicitly]
        private static bool Prefix(InputStates inputStates)
        {
            inputStates.Clear(InputAction.Grip);
            return true;
        }
    }

    [HarmonyPatch(typeof(JetsGadget), "GadgetFixedUpdate")]
    internal static class DisableJetsRampdown
    {
        // ReSharper disable once InconsistentNaming
        [UsedImplicitly]
        private static bool Prefix(JetsGadget __instance)
        {
            if (Entry.JetRampdownDisabled)
            {
                Traverse.Create(__instance).Field("thrusterBoostTimer_").SetValue(0f);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(Gadget), "SetAbilityEnabled")]
    internal static class BlockAbilityDisabling
    {
        [UsedImplicitly]
        private static bool Prefix(ref bool enable)
        {
            if (Entry.AllAbilitiesEnabled)
            {
                enable = true;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AntiTunneler), "CheckForTunneling")]
    internal static class DisableTunnelingPrevention
    {
        [UsedImplicitly]
        private static bool Prefix()
        {
            return !Entry.Noclip;
        }
    }

    [HarmonyPatch(typeof(WingsGadget), "GadgetUpdateLocal")]
    internal static class DisableWingSelfRightening
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableWingSelfRightening");
        }

        // Ensures this branch is not taken:
        //
        // if (Mathf.Abs(carDirectives_.Roll_) < 0.25f)
        //
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
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