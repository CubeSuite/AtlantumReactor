using AtlantumReactor.Patches;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EquinoxsModUtils;
using FMOD.Studio;
using FMODUnity;
using HarmonyLib;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Windows;

namespace AtlantumReactor
{
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    public class AtlantumReactorPlugin : BaseUnityPlugin
    {
        private const string MyGUID = "com.equinox.AtlantumReactor";
        private const string PluginName = "AtlantumReactor";
        private const string VersionString = "1.0.1";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log = new ManualLogSource(PluginName);

        // Objects & Variables

        public static SchematicsRecipeData reactorRecipe;

        private static AudioSource audioSource;
        private static bool audioLoaded = false;

        public static GameObject reactorPrefab;

        // Unity Functions

        private void Awake() {
            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loading...");
            Harmony.PatchAll();

            ApplyPatches();
            ReactorGUI.LoadAssets();
            LoadPrefabs();

            ModUtils.AddNewUnlock(new NewUnlockDetails() {
                category = Unlock.TechCategory.Energy,
                coreTypeNeeded = ResearchCoreDefinition.CoreType.Green,
                coreCountNeeded = 1000,
                description = $"Consumes {ResourceNames.AtlantumMixtureBrick} and {ResourceNames.ShiverthornCoolant} to produce {ReactorSettings.basePowerMW} -> {(ReactorSettings.basePower + 500 * ReactorSettings.perCoolantBoost) / 1000f}MW",
                displayName = ReactorSettings.name,
                numScansNeeded = 0,
                requiredTier = TechTreeState.ResearchTier.Tier0,
                treePosition = 0,
                sprite = ModUtils.LoadSpriteFromFile("AtlantumReactor.Assets.Images.Reactor.png")
            });

            ModUtils.GameDefinesLoaded += OnGameDefinesLoaded;
            ModUtils.TechTreeStateLoaded += OnTechTreeStateLoaded;
            ModUtils.GameLoaded += OnGameLoaded;

            byte[] wavBytes = GetEmbeddedResourceBytes("AtlantumReactor.Assets.Audio.Ignition.wav");
            AudioClip loadedClip = AudioHelper.WAVToAudioClip(wavBytes, "Ignition");
            if (loadedClip == null) {
                Log.LogError("Failed to load audio clip.");
                return;
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = loadedClip;
            audioSource.volume = 0.1f;
            audioLoaded = true;

            Logger.LogInfo($"PluginName: {PluginName}, VersionString: {VersionString} is loaded.");
            Log = Logger;
        }

        private void Update() {
            ReactorGUI.timeSinceClose += Time.deltaTime;
            ReactorGUI.timeSinceIgnition += Time.deltaTime;

            if(ReactorGUI.showGUI && 
              (UnityInput.Current.GetKey(KeyCode.Escape) ||
               UnityInput.Current.GetKey(KeyCode.Tab) || 
               UnityInput.Current.GetKey(KeyCode.Q) ||
               UnityInput.Current.GetKey(KeyCode.E))){
                ReactorGUI.showGUI = false;
                ReactorGUI.timeSinceClose = 0;
                ModUtils.FreeCursor(false);
            }

        }

        private void OnGUI() {
            if (ReactorGUI.showGUI) {
                ReactorGUI.DrawGUI();
            }
        }

        // Events

        private void OnGameDefinesLoaded(object sender, EventArgs e) {
            ResourceInfo reactor = ModUtils.GetResourceInfoByName(ReactorSettings.name);
            ResourceInfo crankGen2 = ModUtils.GetResourceInfoByName(ResourceNames.CrankGeneratorMKII);
            reactor.headerType = crankGen2.headerType;
            reactor.unlock = ModUtils.GetUnlockByName(ReactorSettings.name);
            reactorRecipe.unlock = ModUtils.GetUnlockByName(ReactorSettings.name);
        }

        private void OnTechTreeStateLoaded(object sender, EventArgs e) {
            Unlock crankGen2 = ModUtils.GetUnlockByName(UnlockNames.CrankGeneratorMKII);
            Unlock hvcReach3 = ModUtils.GetUnlockByName(UnlockNames.HVCReachIV);
            Unlock reactor = ModUtils.GetUnlockByName(ReactorSettings.name);

            reactor.treePosition = crankGen2.treePosition;
            reactor.requiredTier = hvcReach3.requiredTier;
            reactor.unlockedRecipes.Add(reactorRecipe);
        }
        
        private void OnGameLoaded(object sender, EventArgs e) {
            MachineInstanceList<MemoryTreeInstance, MemoryTreeDefinition> treeList = MachineManager.instance.GetMachineList<MemoryTreeInstance, MemoryTreeDefinition>(MachineTypeEnum.MemoryTree);
            for (int i = 0; i < treeList.myArray.Length; i++) {
                MemoryTreeInstance instance = treeList.myArray[i];
                if (instance.myDef == null) return;
                if(instance.myDef.displayName == ReactorSettings.name) {
                    ReactorStage stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(instance.commonInfo.instanceId, ReactorProperties.stage);
                    if (!ModUtils.NullCheck(stage, "stage")) continue;
                    MemoryTreeInstancePatch.PlayReactorSound(ref instance, stage);
                }
            }
        }

        // Public Functions

        public static void PlayReactorIgnition() {
            if (!audioLoaded) {
                Log.LogError("Cannot play ignite audio, not loaded");
                return;
            }

            if (audioSource.isPlaying) {
                audioSource.Pause();
                return;
            }

            audioSource.time = 0;
            audioSource.Play();
        }

        // Private Functions

        private void ApplyPatches() {
            Harmony.CreateAndPatchAll(typeof(GameDefinesPatch));
            Harmony.CreateAndPatchAll(typeof(InspectorUIPatch));
            Harmony.CreateAndPatchAll(typeof(MemoryTreeDefinitionPatch));
            Harmony.CreateAndPatchAll(typeof(MemoryTreeInstancePatch));
            Harmony.CreateAndPatchAll(typeof(PlayerInspectorPatch));
            Harmony.CreateAndPatchAll(typeof(PlayerInteractionPatch));
        }

        private void LoadPrefabs() {
            AssetBundle bundle = LoadAssetBundle("reactorcore");
            reactorPrefab = bundle.LoadAsset<GameObject>("assets/atlantium_power.prefab");
        }

        private static AssetBundle LoadAssetBundle(string filename) {
            Assembly assembly = Assembly.GetCallingAssembly();
            AssetBundle assetBundle = AssetBundle.LoadFromStream(assembly.GetManifestResourceStream($"AtlantumReactor.Assets.{filename}"));
            return assetBundle;
        }

        private static byte[] GetEmbeddedResourceBytes(string resourceName) {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) {
                if (stream == null) {
                    Log.LogError("Failed to find embedded resource: " + resourceName);
                    return null;
                }
                byte[] buffer = new byte[stream.Length];
                stream.Read(buffer, 0, buffer.Length);
                return buffer;
            }
        }
    }
}
