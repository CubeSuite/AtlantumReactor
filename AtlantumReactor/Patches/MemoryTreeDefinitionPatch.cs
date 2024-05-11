using EquinoxsModUtils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;

namespace AtlantumReactor.Patches
{
    internal class MemoryTreeDefinitionPatch
    {
        public static Dictionary<Vector3, GameObject> myVisualsMap = new Dictionary<Vector3, GameObject>();

        [HarmonyPatch(typeof(MemoryTreeDefinition), "InitInstance")]
        [HarmonyPostfix]
        static void SetToGenerator(ref MemoryTreeInstance newInstance) {
            if (newInstance.myDef.displayName == ReactorSettings.name) {
                newInstance.powerInfo.isGenerator = true;
                newInstance.powerInfo.powerSatisfaction = 1f;
                newInstance.powerInfo.curPowerConsumption = 0;
                newInstance.powerInfo.maxPowerConsumption = 0;
                newInstance.powerInfo.usesPowerSystem = true;

                newInstance.GetInventory(0).numSlots = 3;
                newInstance.GetInventory(0).myStacks = new ResourceStack[3];
                bool flag = newInstance.GetInventory(0).slotSizeMode != Inventory.SlotSizeSettings.FixedOverride;
                for (int j = 0; j < 3; j++) {
                    if (!flag) {
                        newInstance.GetInventory(0).myStacks[j].maxStack = newInstance.GetInventory(0).maxStackSize;
                    }
                    newInstance.GetInventory(0).myStacks[j].InitAsEmpty(flag);
                }
            }
        }

        [HarmonyPatch(typeof(MemoryTreeDefinition), "OnBuild")]
        [HarmonyPostfix]
        static void AddNewMembers(MachineInstanceRef<MemoryTreeInstance> instRef) {
            if (instRef.Get().myDef.displayName != ReactorSettings.name) return;

            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.stage, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.numShiver, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.numKindle, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.capacitorCharge, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.numCharges, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.cycleTimeRemaining, 60f);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.currentPowerGen, 0);
            ModUtils.AddCustomDataForMachine(instRef.instanceId, ReactorProperties.startedRunningSound, false);

            GameObject myVisuals = GameObject.Instantiate(AtlantumReactorPlugin.reactorPrefab, instRef.gridInfo.BottomCenter, Quaternion.Euler(0, instRef.gridInfo.yawRot, 0));
            myVisualsMap.Add(instRef.gridInfo.BottomCenter, myVisuals);
        }

        [HarmonyPatch(typeof(MachineDefinition<MemoryTreeInstance, MemoryTreeDefinition>), "OnDeconstruct")]
        [HarmonyPostfix]
        static void RemoveAudioPlayer(ref MemoryTreeInstance erasedInstance) {
            if (erasedInstance.myDef.displayName != ReactorSettings.name) return;

            if (MemoryTreeInstancePatch.reactorAudioPlayers.ContainsKey(erasedInstance.commonInfo.instanceId)) {
                MemoryTreeInstancePatch.reactorAudioPlayers[erasedInstance.commonInfo.instanceId].stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                MemoryTreeInstancePatch.reactorAudioPlayers.Remove(erasedInstance.commonInfo.instanceId);
            }

            if(myVisualsMap.ContainsKey(erasedInstance.gridInfo.BottomCenter)) {
                GameObject.Destroy(myVisualsMap[erasedInstance.gridInfo.BottomCenter]);
                myVisualsMap.Remove(erasedInstance.gridInfo.BottomCenter);
            }
        }
    }
}
