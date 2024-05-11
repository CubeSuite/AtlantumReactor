using AtlantumReactor.Patches;
using BepInEx;
using EquinoxsModUtils;
using FluffyUnderware.Curvy.Generator;
using FMODUnity;
using RootMotion.Demos;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace AtlantumReactor
{
    public static class ReactorGUI
    {
        // Objects & Variables
        public static bool lookingAtReactor;
        public static bool showGUI;
        public static uint currentReactor;
        public static int currentReactorIndex;
        public static float timeSinceClose;
        public static float timeSinceIgnition = 0;

        private static float windowX = -1;
        private static float windowY = -1;
        private static float centerX = -1;
        private static float centerY = -1;

        private static float currentSafetyPanelOffset = 0;
        private static float currentFallSpeed = 2.0f;

        private static float currentCore1Angle;
        private static float currentCore2Angle;
        private static float currentCore3Angle;
        private static float currentCore4Angle;
        private static float currentCore5Angle;
        private static Vector2 pivotPoint = Vector2.zero;

        #region Textures

        private static Texture2D shaderTexture;
        private static Texture2D reactorBackground;
        private static Texture2D reactorButton;
        private static Texture2D safetyPanel;

        private static Texture2D meterCover;

        private static Texture2D shiverMeter;
        private static Texture2D kindleMeter;
        private static Texture2D accumulatorMeter;
        private static Texture2D chargesMeter;

        private static Texture2D shiverBorder;
        private static Texture2D kindleBorder;
        private static Texture2D accumulatorBorder;
        private static Texture2D chargesBorder;

        private static Texture2D genBackground;
        private static Texture2D coreStatic;
        private static Texture2D core1;
        private static Texture2D core2;
        private static Texture2D core3;
        private static Texture2D core4;
        private static Texture2D core5;

        #endregion

        // Public Functions

        public static void LoadAssets() {
            shaderTexture = ModUtils.LoadTexture2DFromFile(GetPath("ShaderTile"));
            reactorBackground = ModUtils.LoadTexture2DFromFile(GetPath("ReactorControlPanel"));
            reactorButton = ModUtils.LoadTexture2DFromFile(GetPath("ReactorButton"));
            safetyPanel = ModUtils.LoadTexture2DFromFile(GetPath("ReactorSafetyPanel"));

            meterCover = ModUtils.LoadTexture2DFromFile(GetPath("MeterCover"));

            shiverMeter = ModUtils.LoadTexture2DFromFile(GetPath("ShiverMeter"));
            kindleMeter = ModUtils.LoadTexture2DFromFile(GetPath("KindleMeter"));
            accumulatorMeter = ModUtils.LoadTexture2DFromFile(GetPath("AccumulatorMeter"));
            chargesMeter = ModUtils.LoadTexture2DFromFile(GetPath("ChargesMeter"));

            shiverBorder = ModUtils.LoadTexture2DFromFile(GetPath("ShiverBorder"));
            kindleBorder = ModUtils.LoadTexture2DFromFile(GetPath("KindleBorder"));
            accumulatorBorder = ModUtils.LoadTexture2DFromFile(GetPath("AccumulatorBorder"));
            chargesBorder = ModUtils.LoadTexture2DFromFile(GetPath("ChargesBorder"));

            genBackground = ModUtils.LoadTexture2DFromFile(GetPath("ReactorGenBackground"));
            coreStatic = ModUtils.LoadTexture2DFromFile(GetPath("CoreStatic"));
            core1 = ModUtils.LoadTexture2DFromFile(GetPath("Core1"));
            core2 = ModUtils.LoadTexture2DFromFile(GetPath("Core2"));
            core3 = ModUtils.LoadTexture2DFromFile(GetPath("Core3"));
            core4 = ModUtils.LoadTexture2DFromFile(GetPath("Core4"));
            core5 = ModUtils.LoadTexture2DFromFile(GetPath("Core5"));
        }

        public static void DrawGUI() {
            DrawTexture2D(0, 0, Screen.width, Screen.height, shaderTexture);
            SetDimensions();

            ReactorStage stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(currentReactor, ReactorProperties.stage);
            if(stage != ReactorStage.Ignited) {
                DrawTexture2D(windowX, windowY, reactorBackground.width, reactorBackground.height, reactorBackground);
                DrawButton();

                DrawMeter(-170, ReactorProperties.numShiver, shiverMeter, shiverBorder);
                DrawMeter(-50, ReactorProperties.numKindle, kindleMeter, kindleBorder);
                DrawMeter(70, ReactorProperties.capacitorCharge, accumulatorMeter, accumulatorBorder);
                DrawMeter(190, ReactorProperties.numCharges, chargesMeter, chargesBorder);

                DrawSafetyPanel();
            }
            else {
                DrawTexture2D(windowX, windowY, genBackground.width, genBackground.height, genBackground);
                DrawTexture2D(windowX + 120, windowY + 180, coreStatic.width, coreStatic.height, coreStatic);
                DrawQuantityLabels();
                DrawPowerInfo();
                DrawCore1();
                DrawCore2();
                DrawCore3();
                DrawCore4();
                DrawCore5();
            }
        }

        public static void ResetSafetyPanelHeight() {
            currentSafetyPanelOffset = 0;
            currentFallSpeed = 2.0f;
        }

        // Private Functions

        private static string GetPath(string imageName) {
            return $"AtlantumReactor.Assets.Images.{imageName}.png";
        }

        private static void DrawTexture2D(Rect rect, Texture2D image) {
            GUIStyle texture = new GUIStyle() {
                normal = { background = image },
                hover = { background = image },
                focused = { background = image },
                active = { background = image },
                onNormal = { background = image },
                onHover = { background = image },
                onFocused = { background = image },
                onActive = { background = image },
                border = new RectOffset(0, 0, 0, 0)
            };
            GUI.Box(rect, "", texture);
        }

        private static void DrawTexture2D(float x, float y, float width, float height, Texture2D image) {
            Rect rect = new Rect(x, y, width, height);
            DrawTexture2D(rect, image);
        }

        private static void SetDimensions() {
            if (windowX < 0) windowX = (Screen.width - reactorBackground.width) / 2.0f;
            if (windowY < 0) windowY = (Screen.height - reactorBackground.height) / 2.0f;

            if (centerX < 0) centerX = Screen.width / 2.0f;
            if (centerY < 0) centerY = Screen.height / 2.0f;

            pivotPoint = new Vector2(windowX + 370, windowY + 430);
        }

        private static int GetNumCoolant() {
            MachineInstanceList<MemoryTreeInstance, MemoryTreeDefinition> treeList = MachineManager.instance.GetMachineList<MemoryTreeInstance, MemoryTreeDefinition>(MachineTypeEnum.MemoryTree);
            return treeList.GetIndex(currentReactorIndex).GetInventory(0).GetResourceCount(ModUtils.GetResourceIDByName(ResourceNames.ShiverthornCoolant));
        }

        // Boot Panel

        private static void DrawButton() {
            GUIStyle buttonStyle = new GUIStyle() {
                normal = { background = null },
                hover = { background = null },
                focused = { background = null },
                active = { background = null },
                onNormal = { background = null },
                onHover = { background = null },
                onFocused = { background = null },
                onActive = { background = null },
            };
            if(GUI.Button(new Rect(windowX + 152, windowY + 620, 100, 100), reactorButton, buttonStyle)) {
                ModUtils.UpdateCustomDataForMachine(currentReactor, ReactorProperties.stage, (int)ReactorStage.Ignited);
                timeSinceIgnition = 0;
                AtlantumReactorPlugin.PlayReactorIgnition();
            }
        }

        private static void DrawMeter(float height, string dataName, Texture2D meterTexture, Texture2D borderTexture) {
            DrawTexture2D(centerX - 466, centerY + height + 2, 1130, 36, meterTexture);

            int temp = ModUtils.GetCustomDataForMachine<int>(currentReactor, dataName);
            float max = dataName == ReactorProperties.capacitorCharge ? ReactorSettings.energyLimit : ReactorSettings.itemLimit;
            float progress = Mathf.Min(temp / max, 1f);
            float offset = 1134 * progress;
            if(progress != 1f) DrawTexture2D(centerX - 468 + offset, centerY + height, 1134 - offset, 40, meterCover);

            DrawTexture2D(centerX - 468, centerY + height, 1134, 40, borderTexture);
        }

        // Gen Panel

        private static void DrawQuantityLabels() {
            GUIStyle labelStyle = new GUIStyle() {
                fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.yellow, background = null },
                hover = { textColor = Color.yellow, background = null },
                active = { textColor = Color.yellow, background = null },
                focused = { textColor = Color.yellow, background = null },
                onNormal = { textColor = Color.yellow, background = null },
                onHover = { textColor = Color.yellow, background = null },
                onActive = { textColor = Color.yellow, background = null },
                onFocused = { textColor = Color.yellow, background = null },
            };

            int fuelID = ModUtils.GetResourceIDByName(ResourceNames.AtlantumMixtureBrick);
            MachineInstanceList<MemoryTreeInstance, MemoryTreeDefinition> treeList = MachineManager.instance.GetMachineList<MemoryTreeInstance, MemoryTreeDefinition>(MachineTypeEnum.MemoryTree);

            GUIContent numFuel = new GUIContent(treeList.GetIndex(currentReactorIndex).GetInventory(0).GetResourceCount(fuelID).ToString());
            GUIContent numCoolant = new GUIContent(GetNumCoolant().ToString());

            GUI.Label(new Rect(pivotPoint.x -50, pivotPoint.y + 20, 100, 100), numFuel, labelStyle);
            GUI.Label(new Rect(centerX - 25, pivotPoint.y + 20, 50, 50), numCoolant, labelStyle);
        }

        private static void DrawPowerInfo() {
            GUIStyle titleStyle = new GUIStyle() {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.yellow, background = null },
                hover = { textColor = Color.yellow, background = null },
                active = { textColor = Color.yellow, background = null },
                focused = { textColor = Color.yellow, background = null },
                onNormal = { textColor = Color.yellow, background = null },
                onHover = { textColor = Color.yellow, background = null },
                onActive = { textColor = Color.yellow, background = null },
                onFocused = { textColor = Color.yellow, background = null },
            };
            GUIStyle dataStyle = new GUIStyle() {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight,
                normal = { textColor = Color.yellow, background = null },
                hover = { textColor = Color.yellow, background = null },
                active = { textColor = Color.yellow, background = null },
                focused = { textColor = Color.yellow, background = null },
                onNormal = { textColor = Color.yellow, background = null },
                onHover = { textColor = Color.yellow, background = null },
                onActive = { textColor = Color.yellow, background = null },
                onFocused = { textColor = Color.yellow, background = null },
            };

            int nextCycleTime = Mathf.FloorToInt(ModUtils.GetCustomDataForMachine<float>(currentReactor, ReactorProperties.cycleTimeRemaining));
            int currentPowerGen = ModUtils.GetCustomDataForMachine<int>(currentReactor, ReactorProperties.currentPowerGen);
            int numCoolant = GetNumCoolant();
            int nextPowerGen = ReactorSettings.basePower + Mathf.CeilToInt(numCoolant * ReactorSettings.perCoolantBoost);

            currentPowerGen = Mathf.CeilToInt(currentPowerGen * PowerState.instance.powerOutputMultiplier);
            nextPowerGen = Mathf.CeilToInt(nextPowerGen * PowerState.instance.powerOutputMultiplier);

            GUI.Label(new Rect(windowX + 973, windowY + 275, 300, 50), "Next Cycle:", titleStyle);
            GUI.Label(new Rect(windowX + 973, windowY + 325, 300, 50), "Power Generation:", titleStyle);
            GUI.Label(new Rect(windowX + 973, windowY + 375, 300, 50), "Next Power Generation:", titleStyle);

            GUI.Label(new Rect(windowX + 973, windowY + 275, 280, 50), $"{nextCycleTime}s", dataStyle);
            GUI.Label(new Rect(windowX + 973, windowY + 325, 280, 50), $"{currentPowerGen / 1000.0f} MW", dataStyle);
            GUI.Label(new Rect(windowX + 973, windowY + 375, 280, 50), $"{nextPowerGen / 1000.0f} MW", dataStyle);
        }

        private static void DrawCore1() {
            currentCore1Angle += 0.1f;
            GUIUtility.RotateAroundPivot(currentCore1Angle, pivotPoint);
            DrawTexture2D(windowX + 259, windowY + 319, core1.width, core1.height, core1);
        }

        private static void DrawCore2() {
            currentCore2Angle -= 0.2f;
            GUIUtility.RotateAroundPivot(currentCore2Angle, pivotPoint);
            DrawTexture2D(windowX + 225, windowY + 285, core2.width, core2.height, core2);
        }

        private static void DrawCore3() {
            currentCore3Angle += 0.3f;
            GUIUtility.RotateAroundPivot(currentCore3Angle, pivotPoint);
            DrawTexture2D(windowX + 168, windowY + 228, core3.width, core3.height, core3);
        }

        private static void DrawCore4() {
            currentCore4Angle -= 0.4f;
            GUIUtility.RotateAroundPivot(currentCore4Angle, pivotPoint);
            DrawTexture2D(windowX + 137, windowY + 197, core4.width, core4.height, core4);
        }

        private static void DrawCore5() {
            currentCore5Angle += 0.5f;
            GUIUtility.RotateAroundPivot(currentCore5Angle, pivotPoint);
            DrawTexture2D(windowX + 100, windowY + 160, core5.width, core5.height, core5);
        }

        // Safety Panel

        private static void DrawSafetyPanel() {
            if (currentSafetyPanelOffset < GetMaxSafetyPanelOffset()) {
                currentSafetyPanelOffset += currentFallSpeed;
                currentFallSpeed *= 1.025f;
            }
            else {
                currentFallSpeed = 2.0f;
            }

            GUIStyle panelStyle = new GUIStyle() { normal = { background = safetyPanel } };
            Rect scrollerRect = new Rect(windowX - 20, windowY - 20, safetyPanel.width, reactorBackground.height + 20);
            GUI.BeginScrollView(scrollerRect, new Vector2(0, 0), scrollerRect, false, false);
            GUI.Box(new Rect(windowX - 20, windowY - 20 + currentSafetyPanelOffset, safetyPanel.width, safetyPanel.height), "", panelStyle);
            GUI.EndScrollView();
        }

        private static int GetMaxSafetyPanelOffset() {
            ReactorStage stage = (ReactorStage)ModUtils.GetCustomDataForMachine<int>(currentReactor, ReactorProperties.stage);
            switch (stage) {
                case ReactorStage.Idle:
                case ReactorStage.Cooling: return 260;
                case ReactorStage.Heating: return 380;
                case ReactorStage.Charging: return 500;
                case ReactorStage.Kickstarting: return 620;
                case ReactorStage.Ready:
                case ReactorStage.Ignited: return 760;
            }

            return 740;
        }
    }
}
