using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtlantumReactor.Patches
{
    internal class PlayerInspectorPatch
    {
        [HarmonyPatch(typeof(PlayerInspector), "LateUpdate")]
        [HarmonyPrefix]
        static void SetLookingAtReactor() {
            ReactorGUI.lookingAtReactor = false;
        }
    }
}
