using EquinoxsModUtils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AtlantumReactor.Patches
{
    public class InspectorUIPatch 
    {
        [HarmonyPatch(typeof(InspectorUI), "SetInspectingMachine")]
        [HarmonyPrefix]
        static bool ShowReactorGUI(GenericMachineInstanceRef machineHit) {
            if(machineHit.typeIndex != MachineTypeEnum.MemoryTree || 
               machineHit.Get<MemoryTreeInstance>().myDef.displayName != ReactorSettings.name) {
                return true;
            }

            ModUtils.SetPrivateField("inspecting", UIManager.instance.inspectorUI, true);
            ReactorGUI.lookingAtReactor = true;

            if (InputHandler.instance.InteractShortUp && !ReactorGUI.showGUI && ReactorGUI.timeSinceClose >= 0.2f) {
                ModUtils.FreeCursor(true);
                ReactorGUI.currentReactor = machineHit.instanceId;
                ReactorGUI.currentReactorIndex = machineHit.index;
                ReactorGUI.ResetSafetyPanelHeight();
                ReactorGUI.showGUI = true;
            }

            return false;
        }
    }
}
