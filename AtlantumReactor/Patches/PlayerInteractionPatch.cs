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
    internal class PlayerInteractionPatch
    {
        [HarmonyPatch(typeof(PlayerInteraction), "UpdateUI")]
        [HarmonyPostfix]
        static void ShowInspectButton() {
            if (UIManager.instance == null) return;
            if (UIManager.instance.inspectorUI == null) return;

            bool inspecting = (bool)ModUtils.GetPrivateField("inspecting", UIManager.instance.inspectorUI);
            bool showInspect = ReactorGUI.lookingAtReactor && inspecting;
            string text = showInspect ? "Inspect" : "";
            UIManager.instance.hud.SetOverrideButtonSetVisible(showInspect, text);
        }
    }
}
