using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Events;
using Events.Car;
using Harmony;
using JetBrains.Annotations;
using Spectrum.API.Configuration;
using Spectrum.API.Interfaces.Plugins;
using Spectrum.API.Interfaces.Systems;
using UnityEngine;
using Object = UnityEngine.Object;

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

        internal static List<ResultInfo> ResultInfos;

        private static InputStates _inputStates;
        private static NetworkingManager _networkingManager;

        [PublicAPI] public static bool AllowGameplayCheatsInMultiplayer;

        private static CarState _carState;

        public void Initialize(IManager manager, string ipcIdentifier)
        {
            Settings = InitializeSettings();

            CapCarLevelOfDetail.Init();

            var harmony = HarmonyInstance.Create("com.seekr.tweakr");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public void Update()
        {
            _networkingManager = G.Sys.NetworkingManager_;
            var playerManager = G.Sys.PlayerManager_;
            var localPlayer = playerManager ? playerManager.Current_ : null;
            var playerDataLocal = localPlayer?.playerData_;
            var carGameObject = playerDataLocal ? playerDataLocal.Car_ : null;
            _inputStates = playerDataLocal ? playerDataLocal.InputStates_ : null;
            var carLogic = playerDataLocal ? playerDataLocal.CarLogic_ : null;
            var nitronicCarController = carLogic ? carLogic.CarController_ : null;
            var rigidbody = nitronicCarController ? nitronicCarController.Rigidbody_ : null;
            var carStats = carLogic ? carLogic.CarStats_ : null;

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

            if (IsTriggered(Settings.GetItem<string>("carStateSaveHotkey")))
            {
                _carState = CarState.TryGetCarState(carLogic, nitronicCarController, rigidbody, carStats) ?? _carState;
            }

            if (IsTriggered(Settings.GetItem<string>("carStateLoadHotkey")))
            {
                if (rigidbody != null && carStats != null && _carState != null && carGameObject != null &&
                    !carLogic.IsDying_)
                {
                    carGameObject.GetComponent<Teleportable>()
                        .Teleport(rigidbody.transform, _carState.Transform, Teleportable.TeleTransformType.Relative,
                            false);

                    var playerEvents = Traverse.Create(carLogic).Field("playerEvents_").GetValue<PlayerEvents>();
                    var onEventGravityToggled = AccessTools.Method(typeof(CarLogic), "OnEventGravityToggled");
                    playerEvents.Unsubscribe(
                        (InstancedEvent<GravityToggled.Data>.Delegate) Delegate.CreateDelegate(
                            typeof(InstancedEvent<GravityToggled.Data>.Delegate), carLogic, onEventGravityToggled));
                    playerEvents.Broadcast(new GravityToggled.Data(!_carState.GravityEnabled, 0, 0));
                    playerEvents.Subscribe(
                        (InstancedEvent<GravityToggled.Data>.Delegate) Delegate.CreateDelegate(
                            typeof(InstancedEvent<GravityToggled.Data>.Delegate), carLogic, onEventGravityToggled));

                    rigidbody.useGravity = _carState.GravityEnabled;
                    rigidbody.velocity = _carState.Velocity;
                    rigidbody.angularVelocity = _carState.AngularVelocity;
                    rigidbody.angularDrag = _carState.AngularDrag;

                    nitronicCarController.dragMultiplier_ = _carState.DragMultiplier;

                    carLogic.SetInfiniteCooldown(_carState.InfiniteCooldown, false);
                    carLogic.Heat_ = _carState.Heat;

                    carStats.WheelsContacting_ = _carState.WheelsContacting;
                    Traverse.Create(carLogic.Jets_).Field("thrusterBoostTimer_").SetValue(_carState.ThrusterBoostTimer);

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
                new SettingsEntry("showLocalLeaderboardResultTimestamps", false),
                new SettingsEntry("disableLocalLeaderboardResultLimit", false),
                new SettingsEntry("enableShiftClickMultiselectLeaderboardEntries", true),
                new SettingsEntry("removeReplayPlaybackLimit", false),
                new SettingsEntry("downloadAllLeaderboardEntries", false),
                new SettingsEntry("carLevelOfDetailCap", "Speck"),
                new SettingsEntry("checkpointHotkey", ""),
                new SettingsEntry("infiniteCooldownHotkey", ""),
                new SettingsEntry("allAbilitiesHotkey", ""),
                new SettingsEntry("noclipHotkey", ""),
                new SettingsEntry("disableJetRampdownHotkey", ""),
                new SettingsEntry("carStateSaveHotkey", ""),
                new SettingsEntry("carStateLoadHotkey", "")
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
            if (hotkey.Count == 0 || _inputStates == null)
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

    internal class CarState
    {
        public Transform Transform => _gameObject.transform;
        public Vector3 Velocity { get; }
        public Vector3 AngularVelocity { get; }
        public Vector3 DragMultiplier { get; }
        public float AngularDrag { get; }
        public bool InfiniteCooldown { get; }
        public float Heat { get; }
        public bool GravityEnabled { get; }
        public int WheelsContacting { get; }
        public float ThrusterBoostTimer { get; }

        private readonly GameObject _gameObject;

        private CarState(CarLogic carLogic, NitronicCarController carController, Rigidbody rigidbody, CarStats carStats)
        {
            _gameObject = new GameObject();
            Object.DontDestroyOnLoad(_gameObject);
            _gameObject.transform.position = rigidbody.position;
            _gameObject.transform.rotation = rigidbody.rotation;

            Velocity = rigidbody.velocity;
            AngularVelocity = rigidbody.angularVelocity;

            DragMultiplier = carController.dragMultiplier_;
            AngularDrag = rigidbody.angularDrag;

            InfiniteCooldown = carLogic.infiniteCooldown_;
            Heat = carLogic.Heat_;

            GravityEnabled = rigidbody.useGravity;

            WheelsContacting = carStats.WheelsContacting_;
            ThrusterBoostTimer = Traverse.Create(carLogic.Jets_).Field("thrusterBoostTimer_").GetValue<float>();
        }

        public static CarState TryGetCarState(CarLogic carLogic, NitronicCarController nitronicCarController,
            Rigidbody rigidbody, CarStats carStats)
        {
            if (carLogic == null || carLogic.IsDying_ || nitronicCarController == null || rigidbody == null ||
                carStats == null)
            {
                return null;
            }

            return new CarState(carLogic, nitronicCarController, rigidbody, carStats);
        }
    }

    [HarmonyPatch(typeof(CheatsManager))]
    [HarmonyPatch("GameplayCheatsUsedThisLevel_", MethodType.Getter)]
    internal static class BlockLeaderboardUpdatingWhenCheating
    {
        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
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
        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static bool Prefix(ref float ___thrusterBoostTimer_)
        {
            if (Entry.JetRampdownDisabled)
            {
                ___thrusterBoostTimer_ = 0f;
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

    [HarmonyPatch(typeof(LocalLeaderboard), "Load")]
    internal static class StoreLocalLeaderboardResultTimestamps
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("showLocalLeaderboardResultTimestamps");
        }

        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static void Postfix(LocalLeaderboard __result)
        {
            if (__result != null)
            {
                Entry.ResultInfos = __result.Results_;
            }
        }
    }

    [HarmonyPatch(typeof(LevelSelectLeaderboardMenu.Entry), MethodType.Constructor, typeof(int),
        typeof(ModeFinishInfoBase), typeof(string), typeof(OnlineLeaderboard.Entry), typeof(MedalStatus))]
    internal static class ShowLocalLeaderboardResultTimestampsInLevelSelectLeaderboardMenu
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("showLocalLeaderboardResultTimestamps");
        }

        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static void Postfix(LevelSelectLeaderboardMenu.Entry __instance, int index)
        {
            if (__instance.info_.isLocal_ && __instance.leaderboardEntry_ is OfflineLeaderboardEntry)
            {
                __instance.dataText_ +=
                    $"  [c][FF5900]{Entry.ResultInfos[index].TimeOfRecordingInLocalTime_:yyyy-MM-dd HH:mm}";
            }
        }
    }

    [HarmonyPatch(typeof(GameMode), "GenerateResultsScreenInfo")]
    internal static class ShowLocalLeaderboardResultTimestampsInFinishMenu
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("showLocalLeaderboardResultTimestamps");
        }

        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static void Postfix(FinishMenuLogic.PageEntry[] __result)
        {
            var finishMenuLogic = (FinishMenuLogic) Object.FindObjectOfType(typeof(FinishMenuLogic));
            var resultsPage =
                Traverse.Create(finishMenuLogic).Field("resultsPage_").GetValue<FinishMenuLogic.ResultsPage>();
            if (resultsPage == FinishMenuLogic.ResultsPage.LocalLeaderboards)
            {
                for (var i = 0; i < __result.Length; ++i)
                {
                    __result[i].score +=
                        $"  [c][FF5900]{Entry.ResultInfos[i].TimeOfRecordingInLocalTime_:yyyy-MM-dd HH:mm}";
                }
            }
        }
    }

    // Stop results past #20 from being deleted
    [HarmonyPatch(typeof(LocalLeaderboard), "TrimResults")]
    internal static class DisableLocalLeaderboardResultLimit1
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableLocalLeaderboardResultLimit");
        }

        [UsedImplicitly]
        private static bool Prefix()
        {
            return false;
        }
    }

    // Save results, even when they're not in the top 20
    [HarmonyPatch(typeof(LocalLeaderboard), "InsertResult")]
    internal static class DisableLocalLeaderboardResultLimit2
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("disableLocalLeaderboardResultLimit");
        }

        // Ensures this branch is always taken:
        //
        // if (num < 20)
        //
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            foreach (var codeInstruction in instr)
            {
                if (codeInstruction.opcode == OpCodes.Ldc_I4_S && (sbyte) codeInstruction.operand == 20)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue);
                }
                else
                {
                    yield return codeInstruction;
                }
            }
        }
    }

    [HarmonyPatch(typeof(LevelSelectLeaderboardMenu.Entry), "OnClick")]
    internal static class EnableShiftClickMultiselectLeaderboardEntries
    {
        private static int _lastClickedIndex;

        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("enableShiftClickMultiselectLeaderboardEntries");
        }

        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static bool Prefix(LevelSelectLeaderboardMenu.Entry __instance)
        {
            var shiftIsPressed = Input.GetKey("left shift") || Input.GetKey("right shift");
            if (shiftIsPressed)
            {
                var start = _lastClickedIndex;
                var end = __instance.index_;
                if (start > end)
                {
                    var tmp = start;
                    start = end;
                    end = tmp;
                }

                var menu = (LevelSelectLeaderboardMenu) Object.FindObjectOfType(typeof(LevelSelectLeaderboardMenu));
                var entries = Traverse.Create(menu).Property("ScrollableEntries_")
                    .GetValue<List<LevelSelectLeaderboardMenu.Entry>>();
                var startIsToggled = entries[start].IsToggled_;
                for (var i = start; i < end; ++i)
                {
                    entries[i].IsToggled_ = startIsToggled;
                }

                // Hack to fix misbehaving last entry
                entries[end].IsToggled_ = !startIsToggled;
                menu.buttonList_.ReportAllChanged();
                entries[end].IsToggled_ = startIsToggled;
            }
            else
            {
                __instance.IsToggled_ = !__instance.IsToggled_;
                _lastClickedIndex = __instance.index_;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(ReplayManager), "PlayPickedReplays")]
    internal static class RemoveReplayPlaybackLimit1
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("removeReplayPlaybackLimit");
        }

        // Ensures this branch is not taken:
        //
        // if (list.Count + this.pickedReplays_.transform.childCount >= 20)
        //
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            foreach (var codeInstruction in instr)
            {
                if (codeInstruction.opcode == OpCodes.Ldc_I4_S && (sbyte) codeInstruction.operand == 20)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue);
                }
                else
                {
                    yield return codeInstruction;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ReplayManager), "SpawnReplay")]
    internal static class RemoveReplayPlaybackLimit2
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("removeReplayPlaybackLimit");
        }

        // Ensures this branch is not taken:
        //
        // if (PlayerDataReplay.ReplayPlayers_.Count >= 20 || !ReplayManager.SaveLoadReplays_)
        //
        [UsedImplicitly]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
        {
            foreach (var codeInstruction in instr)
            {
                if (codeInstruction.opcode == OpCodes.Ldc_I4_S && (sbyte) codeInstruction.operand == 20)
                {
                    yield return new CodeInstruction(OpCodes.Ldc_I4, int.MaxValue);
                }
                else
                {
                    yield return codeInstruction;
                }
            }
        }
    }

    [HarmonyPatch(typeof(SuperMenu), "TweakInt")]
    internal static class IncreaseGhostSliderLimit
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("removeReplayPlaybackLimit");
        }

        [UsedImplicitly]
        private static void Prefix(string name, ref int max)
        {
            if (name == "GHOSTS IN ARCADE COUNT")
            {
                max = 600;
            }
        }
    }

    [HarmonyPatch(typeof(CarLevelOfDetail), "SetLevelOfDetail")]
    internal static class CapCarLevelOfDetail
    {
        private static CarLevelOfDetail.Level _maxLod = CarLevelOfDetail.Level.Speck;

        internal static void Init()
        {
            var setting = Entry.Settings.GetItem<string>("carLevelOfDetailCap");
            try
            {
                _maxLod = (CarLevelOfDetail.Level) Enum.Parse(typeof(CarLevelOfDetail.Level), setting, true);
            }
            catch (Exception)
            {
                Console.WriteLine("[Tweakr] Setting 'carLevelOfDetailCap' has an invalid value; ignoring.");
            }
        }

        [UsedImplicitly]
        private static void Prefix(ref CarLevelOfDetail.Level newLevel)
        {
            if (newLevel > _maxLod)
            {
                newLevel = _maxLod;
            }
        }
    }

    [HarmonyPatch(typeof(SteamworksLeaderboard), "DownloadLeaderboardInfo")]
    internal static class DownloadAllLeaderboardEntries1
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("downloadAllLeaderboardEntries");
        }

        [UsedImplicitly]
        private static void Prefix(ref int rangeEnd, bool replaysOnly)
        {
            if (!replaysOnly)
            {
                rangeEnd = int.MaxValue;
            }
        }
    }

    [HarmonyPatch(typeof(LevelSelectLeaderboardMenu.Entry), MethodType.Constructor, typeof(int),
        typeof(ModeFinishInfoBase), typeof(string), typeof(OnlineLeaderboard.Entry), typeof(MedalStatus))]
    internal static class DownloadAllLeaderboardEntries2
    {
        [UsedImplicitly]
        private static bool Prepare()
        {
            return Entry.Settings.GetItem<bool>("downloadAllLeaderboardEntries");
        }

        [UsedImplicitly]
        // ReSharper disable once InconsistentNaming
        private static void Postfix(LevelSelectLeaderboardMenu.Entry __instance, int index)
        {
            __instance.sortString_ = index.ToString("D10");
        }
    }
}