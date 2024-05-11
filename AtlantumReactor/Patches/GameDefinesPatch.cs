using EquinoxsModUtils;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AtlantumReactor.Patches
{
    internal class GameDefinesPatch
    {
        // Objects & Variables
        private static bool hasAdded = false;
        private static MemoryTreeDefinition coreComposerDefinition;
        private static MemoryTreeDefinition reactorDefinition;

        // Fixes

        [HarmonyPatch(typeof(GameDefines), "GetMaxResId")]
        [HarmonyPrefix]
        static void AddReactorToGame() {
            if(hasAdded) return;
            hasAdded = true;

            coreComposerDefinition = (MemoryTreeDefinition)ModUtils.GetResourceInfoByNameUnsafe(ResourceNames.CoreComposer);

            AddReactor();
            AddReactorRecipe();

            ModUtils.SetPrivateStaticField("_topResId", GameDefines.instance, -1);
        }

        // Private Functions

        private static void AddReactor() {
            reactorDefinition = (MemoryTreeDefinition)ScriptableObject.CreateInstance(typeof(MemoryTreeDefinition));
            ModUtils.CloneObject(coreComposerDefinition, ref reactorDefinition);

            reactorDefinition.description = $"Consumes {ResourceNames.AtlantumMixtureBrick} and {ResourceNames.ShiverthornCoolant} to produce {ReactorSettings.basePowerMW} -> {(ReactorSettings.basePower + 500 * ReactorSettings.perCoolantBoost) / 1000f}MW";
            reactorDefinition.rawName = ReactorSettings.name;
            reactorDefinition.rawSprite = ModUtils.LoadSpriteFromFile("AtlantumReactor.Assets.Images.Reactor.png");
            reactorDefinition.uniqueId = ModUtils.GetNewResourceID();
            reactorDefinition.kWPowerConsumption = -ReactorSettings.basePower;

            GameDefines.instance.resources.Add(reactorDefinition);
            GameDefines.instance.buildableResources.Add(reactorDefinition);
            ResourceNames.SafeResources.Add(ReactorSettings.name);
        }

        private static void AddReactorRecipe() {
            NewRecipeDetails details = new NewRecipeDetails() {
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 120,
                ingredients = new List<RecipeResourceInfo>() {
                    new RecipeResourceInfo(ResourceNames.CoreComposer, 1),
                    new RecipeResourceInfo(ResourceNames.CrankGeneratorMKII, 5),
                    new RecipeResourceInfo(ResourceNames.SteelSlab, 10)
                },
                outputs = new List<RecipeResourceInfo>() {
                    new RecipeResourceInfo(ReactorSettings.name, 1)
                },
                sortPriority = 10
            };
            AtlantumReactorPlugin.reactorRecipe = details.ConvertToRecipe();
            AtlantumReactorPlugin.reactorRecipe.name = ReactorSettings.name;
            AtlantumReactorPlugin.reactorRecipe.uniqueId = ModUtils.GetNewRecipeID();
            AtlantumReactorPlugin.reactorRecipe.unlock = coreComposerDefinition.unlock;
            GameDefines.instance.schematicsRecipeEntries.Add(AtlantumReactorPlugin.reactorRecipe);
        }
    }
}
