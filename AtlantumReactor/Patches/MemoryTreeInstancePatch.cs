using EquinoxsModUtils;
using FIMSpace.GroundFitter;
using FMOD.Studio;
using FMODUnity;
using Gamekit3D.GameCommands;
using HarmonyLib;
using RootMotion.FinalIK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Management.Instrumentation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AtlantumReactor.Patches
{
    internal class MemoryTreeInstancePatch
    {
        // Objects & Variables

        public static Dictionary<uint, EventInstance> reactorAudioPlayers = new Dictionary<uint, EventInstance>();

        // Patches

        [HarmonyPatch(typeof(MemoryTreeInstance), "SimUpdate")]
        [HarmonyPrefix]
        static bool UpdateReactor(MemoryTreeInstance __instance, float dt) {
            if (__instance.myDef.displayName != ReactorSettings.name) return true;

            RotateCenter(ref __instance);

            ReactorStage stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(__instance.commonInfo.instanceId, ReactorProperties.stage);
            if(stage != ReactorStage.Charging && stage != ReactorStage.Ignited) {
                ConsumeFromInventory(ref __instance);
            }

            if(stage == ReactorStage.Charging) {
                ConsumePower(ref __instance);
            }

            if(stage != ReactorStage.Ready && stage != ReactorStage.Ignited) {
                CheckForStageUpdate(ref __instance, stage);
                stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(__instance.commonInfo.instanceId, ReactorProperties.stage);
            }

            PLAYBACK_STATE playbackState = PLAYBACK_STATE.STOPPED;
            if (reactorAudioPlayers.ContainsKey(__instance.commonInfo.instanceId)) {
                reactorAudioPlayers[__instance.commonInfo.instanceId].getPlaybackState(out playbackState);
            }

            if (stage == ReactorStage.Kickstarting &&  playbackState == PLAYBACK_STATE.STOPPED) {
                reactorAudioPlayers[__instance.commonInfo.instanceId].start();
            }

            if(stage == ReactorStage.Ignited) {
                SetToProducePower(ref __instance);
                float currentCycleTime = ModUtils.GetCustomDataForMachine<float>(__instance.commonInfo.instanceId, ReactorProperties.cycleTimeRemaining);
                currentCycleTime -= Time.deltaTime;
                if (currentCycleTime < 0) {
                    currentCycleTime = 60f;
                    OnCycleStart(ref __instance);
                }

                ModUtils.UpdateCustomDataForMachine(__instance.commonInfo.instanceId, ReactorProperties.cycleTimeRemaining, currentCycleTime);
                bool hasNuclearStarted = ModUtils.GetCustomDataForMachine<bool>(__instance.commonInfo.instanceId, ReactorProperties.startedRunningSound);
                if((ReactorGUI.timeSinceIgnition > 4.0f && !hasNuclearStarted) || 
                   (playbackState == PLAYBACK_STATE.STOPPED && hasNuclearStarted)) {
                    PlayReactorSound(ref __instance, stage);
                }
            }

            stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(__instance.commonInfo.instanceId, ReactorProperties.stage);
            if (stage > ReactorStage.Idle && stage < ReactorStage.Ignited) {
                SetToConsumePower(ref __instance);
            }

            MachineManager.instance.GetMachineList<MemoryTreeInstance, MemoryTreeDefinition>(MachineTypeEnum.MemoryTree).myArray[__instance.commonInfo.index].powerInfo = __instance.powerInfo;

            return false;
        }

        [HarmonyPatch(typeof(MemoryTreeInstance), "ValidateAddResources", new Type[] { 
            typeof(int),
            typeof(int),
            typeof(int),
            typeof(AddResourceValidationType)
        }, new ArgumentType[] {
            ArgumentType.Normal,
            ArgumentType.Normal,
            ArgumentType.Out,
            ArgumentType.Normal
        })]
        [HarmonyPrefix]
        static bool CanAddItem(MemoryTreeInstance __instance, ref bool __result, int resId, int inventoryIndex, out int slotNum, AddResourceValidationType validationType) {
            slotNum = -1;
            if (__instance.myDef.displayName != ReactorSettings.name) return true;

            if (ModUtils.GetCustomDataForMachine<int>(__instance.commonInfo.instanceId, ReactorProperties.stage) < (int)ReactorStage.Ignited) {
                int currentResourceNeededId = GetCurrentNeededResId(__instance.commonInfo.instanceId);

                if (currentResourceNeededId == -1 || resId != currentResourceNeededId) {
                    __result = false;
                    return false;
                }

                if (validationType == AddResourceValidationType.InserterReady) {
                    ref Inventory ptr = ref __instance.commonInfo.inventories[inventoryIndex];
                    __result = ptr.CanAddResources(resId);
                }
            }
            else {
                int fuelID = ModUtils.GetResourceIDByName(ResourceNames.AtlantumMixtureBrick);
                int coolantID = ModUtils.GetResourceIDByName(ResourceNames.ShiverthornCoolant);

                if (resId != fuelID && resId != coolantID) {
                    __result = false;
                    return false;
                }

                int numInInventory = __instance.GetInventory(0).GetResourceCount(resId);
                if (numInInventory >= 500) {
                    __result = false;
                    return false;
                }

                __result = true;
            }

            return false;
        }

        // Private Functions

        private static void RotateCenter(ref MemoryTreeInstance reactor) {
            GameObject reactorModel = MemoryTreeDefinitionPatch.myVisualsMap[reactor.gridInfo.BottomCenter];
            GameObject center = reactorModel.transform.Find("reactornew3/centrespin")?.gameObject;
            if(center != null) {
                float maxRotation = 200;
                float ratio = ModUtils.GetCustomDataForMachine<float>(reactor.commonInfo.instanceId, ReactorProperties.cycleTimeRemaining) / 60.0f;
                ratio = Mathf.Max(0.2f, ratio);
                center.transform.Rotate(Vector3.forward, maxRotation * ratio * Time.deltaTime, Space.Self);
            }
        }

        private static void ConsumeFromInventory(ref MemoryTreeInstance reactor) {
            ModUtils.NullCheck(reactor, "reactor");
            ModUtils.NullCheck(reactor.commonInfo, "reactor.commonInfo");

            int currentNeededResId = GetCurrentNeededResId(reactor.commonInfo.instanceId);

            ref Inventory inventory = ref reactor.commonInfo.inventories[0];
            foreach(ResourceStack stack in inventory.myStacks) {
                if (stack.isEmpty) continue;
                if(stack.info.uniqueId == currentNeededResId) {
                    string dataName = "";
                    switch (stack.info.displayName) {
                        case ResourceNames.ShiverthornExtract: dataName = ReactorProperties.numShiver; break;
                        case ResourceNames.KindlevineExtract: dataName = ReactorProperties.numKindle; break;
                        case ResourceNames.MiningCharge: dataName = ReactorProperties.numCharges; break;
                    }

                    AddToResCount(reactor.commonInfo.instanceId, dataName, stack.count);
                    inventory.TryRemoveResources(stack.info.uniqueId, stack.count);
                }
            }
        }

        private static int GetCurrentNeededResId(uint instanceId) {
            ReactorStage stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(instanceId, ReactorProperties.stage);
            switch (stage) {
                case ReactorStage.Idle: return ModUtils.GetResourceIDByName(ResourceNames.ShiverthornExtract);
                case ReactorStage.Cooling: return ModUtils.GetResourceIDByName(ResourceNames.ShiverthornExtract);
                case ReactorStage.Heating: return ModUtils.GetResourceIDByName(ResourceNames.KindlevineExtract);
                case ReactorStage.Kickstarting: return ModUtils.GetResourceIDByName(ResourceNames.MiningCharge);
            }

            return -1;
        }

        private static void AddToResCount(uint instanceId, string customDataName, int amount) {
            int currentAmount = ModUtils.GetCustomDataForMachine<int>(instanceId, customDataName);
            currentAmount += amount;
            ModUtils.UpdateCustomDataForMachine(instanceId, customDataName, currentAmount);
        }

        private static float timeSinceDrain = 0;
        private static void ConsumePower(ref MemoryTreeInstance reactor) {
            timeSinceDrain += Time.deltaTime;
            if (timeSinceDrain <= 1f) return;
            timeSinceDrain = 0;
            
            PowerNetwork network = PowerNetwork.GetNetwork(reactor.powerInfo.powerNetwork);
            int excess = network.totalGenerated - network.totalUsage;

            int currentCharge = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.capacitorCharge);
            currentCharge += excess;

            ModUtils.UpdateCustomDataForMachine(reactor.commonInfo.instanceId, ReactorProperties.capacitorCharge, currentCharge);
        }

        private static void CheckForStageUpdate(ref MemoryTreeInstance reactor, ReactorStage currentStage) {
            int numShiver = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.numShiver);
            int numKindle = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.numKindle);
            int capacitorCharge = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.capacitorCharge);
            int numCharges = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.numCharges);
            bool runningSoundStarted = ModUtils.GetCustomDataForMachine<bool>(reactor.commonInfo.instanceId, ReactorProperties.startedRunningSound);

            switch (currentStage) {
                case ReactorStage.Idle: if(numShiver != 0) SetStage(ref reactor, ReactorStage.Cooling); break;
                case ReactorStage.Cooling: if (numShiver >= ReactorSettings.itemLimit) SetStage(ref reactor, ReactorStage.Heating); break;
                case ReactorStage.Heating: if (numKindle >= ReactorSettings.itemLimit) SetStage(ref reactor, ReactorStage.Charging); break;
                case ReactorStage.Charging: if (capacitorCharge >= ReactorSettings.energyLimit) SetStage(ref reactor, ReactorStage.Kickstarting); break;
                case ReactorStage.Kickstarting: if (numCharges >= ReactorSettings.itemLimit) SetStage(ref reactor, ReactorStage.Ready); break;
                case ReactorStage.Ignited: if (ReactorGUI.timeSinceIgnition > 4.0f && !runningSoundStarted) PlayReactorSound(ref reactor, currentStage); break;
            }
        }

        private static void SetStage(ref MemoryTreeInstance reactor, ReactorStage stage) {
            ModUtils.UpdateCustomDataForMachine(reactor.commonInfo.instanceId, ReactorProperties.stage, (int)stage);
            PlayReactorSound(ref reactor, stage);
        }

        public static void PlayReactorSound(ref MemoryTreeInstance reactor, ReactorStage stage) {
            if (!reactorAudioPlayers.ContainsKey(reactor.commonInfo.instanceId)) {
                EventInstance audioPlayer = RuntimeManager.CreateInstance("event:/Silence");
                reactorAudioPlayers.Add(reactor.commonInfo.instanceId, audioPlayer);
            }

            string eventName = "event:/Silence";
            switch (stage) {
                case ReactorStage.Cooling: eventName = "event:/SFX/Machine SFX/Placeholders/Water Wheel Placeholder"; break;
                case ReactorStage.Heating: eventName = "event:/SFX/Machine SFX/Smelter Active"; break;
                case ReactorStage.Charging: eventName = "event:/SFX/Machine SFX/Accumulator"; break;
                case ReactorStage.Kickstarting: eventName = "event:/SFX/Machine SFX/Blast Smelter/Blast Smelter Explosion"; break;
                case ReactorStage.Ready: eventName = "event:/SFX/Machine SFX/Power Generator"; break;
                case ReactorStage.Ignited: 
                    eventName = "event:/SFX/Tool SFX/Multiplayer Tools/Multiplayer MOLE/Multiplayer MOLE BH";
                    ModUtils.UpdateCustomDataForMachine(reactor.commonInfo.instanceId, ReactorProperties.startedRunningSound, true);
                    break;
            }

            reactorAudioPlayers[reactor.commonInfo.instanceId].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            reactorAudioPlayers[reactor.commonInfo.instanceId] = RuntimeManager.CreateInstance(eventName);
            reactorAudioPlayers[reactor.commonInfo.instanceId].set3DAttributes(RuntimeUtils.To3DAttributes(reactor.gridInfo.Center));
            reactorAudioPlayers[reactor.commonInfo.instanceId].setParameterByName("Volume", 0.2f);
            reactorAudioPlayers[reactor.commonInfo.instanceId].start();
        }

        public static void PlayReactorSound(int index, ReactorStage stage) {
            MachineInstanceList<MemoryTreeInstance, MemoryTreeDefinition> treeList = MachineManager.instance.GetMachineList<MemoryTreeInstance, MemoryTreeDefinition>(MachineTypeEnum.MemoryTree);
            PlayReactorSound(ref treeList.GetIndex(index), stage);
        }

        private static void SetToConsumePower(ref MemoryTreeInstance reactor) {
            reactor.powerInfo.isGenerator = false;
            reactor.powerInfo.powerSatisfaction = 1f;
            reactor.powerInfo.curPowerConsumption = 5000;
            reactor.powerInfo.maxPowerConsumption = 5000;
            reactor.powerInfo.usesPowerSystem = true;
        }

        private static void SetToProducePower(ref MemoryTreeInstance reactor) {
            if (reactor.powerInfo.isGenerator == true) return;
            reactor.powerInfo.isGenerator = true;
            reactor.powerInfo.powerSatisfaction = 1f;
            reactor.powerInfo.curPowerConsumption = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.currentPowerGen);
            reactor.powerInfo.maxPowerConsumption = ModUtils.GetCustomDataForMachine<int>(reactor.commonInfo.instanceId, ReactorProperties.currentPowerGen);
            reactor.powerInfo.usesPowerSystem = true;
        }

        private static void OnCycleStart(ref MemoryTreeInstance reactor) {
            int fuelID = ModUtils.GetResourceIDByName(ResourceNames.AtlantumMixtureBrick);
            int coolantID = ModUtils.GetResourceIDByName(ResourceNames.ShiverthornCoolant);

            if (reactor.GetInventory(0).HasAnyOfResource(fuelID)) {
                reactor.GetInventory(0).TryRemoveResources(fuelID, 1);

                int numCoolant = Mathf.Min(reactor.GetInventory(0).GetResourceCount(coolantID), 500);
                int bonusPower = Mathf.CeilToInt(numCoolant * ReactorSettings.perCoolantBoost);
                reactor.GetInventory(0).TryRemoveResources(coolantID, numCoolant);
                int newPowerGen = ReactorSettings.basePower + bonusPower;
                ModUtils.UpdateCustomDataForMachine(reactor.commonInfo.instanceId, ReactorProperties.currentPowerGen, newPowerGen);

                reactor.powerInfo.curPowerConsumption = -newPowerGen;
                reactor.powerInfo.maxPowerConsumption = -newPowerGen;
            }
            else {
                reactor.powerInfo.curPowerConsumption = 0;
                reactor.powerInfo.maxPowerConsumption = 0;
            }
        }
    }
}
