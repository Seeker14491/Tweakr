using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Spectrum.API;
using Spectrum.API.Configuration;
using Spectrum.API.Interfaces.Plugins;
using Spectrum.API.Interfaces.Systems;
using Harmony;

namespace Tweakr
{
    public class Entry : IPlugin, IUpdatable
    {
        public string FriendlyName => "Tweakr";
        public string Author => "Seekr";
        public string Contact => "Discord: Seekr#3274; Steam: Seeker14491";
        public APILevel CompatibleAPILevel => APILevel.XRay;

        private static Settings _settings;
        private static readonly Dictionary<string, InputAction[]> Hotkeys = new Dictionary<string, InputAction[]>();
        private static readonly MethodInfo[] AbilityMethods = new []
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

        private const bool AllowGameplayCheatsInMultiplayer = false;

        public void Initialize(IManager manager)
        {
            _settings = InitializeSettings();

            var harmony = HarmonyInstance.Create("com.seekr.tweakr");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            if (_settings.GetItem<bool>("disableWingGripControlModifier"))
            {
                var original = typeof(WingsGadget).GetMethod("GadgetUpdateLocal", BindingFlags.Public | BindingFlags.Instance);
                var prefix = typeof(DisableWingGripControlModifier).GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(original, new HarmonyMethod(prefix), null);
            }

            if (_settings.GetItem<bool>("enableCheatMenu"))
            {
                var original = typeof(CheatsManager).GetProperty("EnabledInThisBuild_", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
                var postfix = typeof(EnableCheatMenu).GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
                harmony.Patch(original, null, new HarmonyMethod(postfix));
            }
        }

        public void Update()
        {
            if (_settings.GetItem<bool>("carScreenDeclutter"))
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
                if (IsTriggered(_settings.GetItem<string>("checkpointHotkey")) && transform != null)
                {
                    G.Sys.PlayerManager_?.Current_?.playerData_?.SetResetTransform(transform.position, transform.rotation);
                    HasSetCheckpoint = true;
                }
            }

            if (IsTriggered(_settings.GetItem<string>("infiniteCooldownHotkey")))
            {
                G.Sys.PlayerManager_?.Current_?.playerData_?.CarLogic_?.SetInfiniteCooldown(true);
                Cheated = true;
            }

            if (IsTriggered(_settings.GetItem<string>("allAbilitiesHotkey")))
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

            if (IsTriggered(_settings.GetItem<string>("disableJetRampdownHotkey")))
            {
                JetRampdownDisabled = true;
                Cheated = true;
            }

            if (IsTriggered(_settings.GetItem<string>("noclipHotkey")))
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

        public void Shutdown()
        {
        }

        private static Settings InitializeSettings()
        {
            var settings = new Settings(typeof(Entry));

            var entries = new[]
            {
                new SettingsEntry("enableCheatMenu", true),
                new SettingsEntry("carScreenDeclutter", false),
                new SettingsEntry("disableWingGripControlModifier", false),
                new SettingsEntry("checkpointHotkey", ""),
                new SettingsEntry("infiniteCooldownHotkey", ""),
                new SettingsEntry("allAbilitiesHotkey", ""),
                new SettingsEntry("noclipHotkey", ""),
                new SettingsEntry("disableJetRampdownHotkey", ""),
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
                return new InputAction[] {};
            }

            return keys
                .Split('+')
                .Select(x => (InputAction)Enum.Parse(typeof(InputAction), x, true))
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

    // For some reason using the built-in cheats in multiplayer will not block leaderboard updating; this fixes that.
    [HarmonyPatch(typeof(CheatsManager))]
    [HarmonyPatch("AnyGameplayCheatEnabled_", PropertyMethod.Getter)]
    internal class FixMultiplayerBuiltinCheats
    {
        static bool Prefix(CheatsManager __instance, ref bool __result)
        {
            __result = Traverse.Create(__instance).Field("anyGameplayCheatsUsedThisLevel_").GetValue<bool>();

            return false;
        }
    }

    [HarmonyPatch(typeof(CarCheatBase), "AddCheatsToCarBlueprint")]
    internal class RegulateMultiplayerBuiltinCheats
    {
        static bool Prefix(ref uint cheatFlags)
        {
            if (!Entry.MultiplayerCheatShouldContinue())
            {
                cheatFlags = 0;
                Traverse.Create(G.Sys.CheatsManager_).Field("anyGameplayCheatsUsedThisLevel_").SetValue(false);
            }

            return true;
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

    internal class DisableWingGripControlModifier
    {
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

    internal class EnableCheatMenu
    {
        static void Postfix(ref bool __result)
        {
            __result = true;
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
}
