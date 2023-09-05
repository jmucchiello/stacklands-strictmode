using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace StrictModeModNS
{
    public class StrictModeMod : Mod
    {
        public static StrictModeMod Instance;
        public ConfigEntry<bool> IsStrict;

        public void Log(string msg) => Logger.Log(msg);

        private void Awake()
        {
            Instance = this;
            IsStrict = Config.GetEntry<bool>("strictmodemod_config_enable", false,
                new ConfigUI() { NameTerm = "strictmodemod_config_enable",
                    TooltipTerm = "strictmodemod_config_enable_tooltip",
                    RestartAfterChange = true 
                });
//            Harmony.PatchAll();
        }

        public override void Ready()
        {
            List<string> skipIds = new List<string>() { "blueprint_happiness", "blueprint_greed_curse_fix" };
            int count = 0;
            Logger.Log($"Strict Mode is {(IsStrict.Value ? "Enabled" : "Disabled")}");
            if (IsStrict.Value)
            {
                Log("Updating blueprints...");
                foreach (Blueprint blueprint in WorldManager.instance.BlueprintPrefabs)
                {
                    if (!blueprint.Id.StartsWith("ideas_") && !skipIds.Any(x => x == blueprint.Id) && !blueprint.IsInvention)
                    {
                        ++count;
                        blueprint.IsInvention = true;
                    }
                    else if (!blueprint.IsInvention)
                    {
                        Log($"...Skipping {blueprint.Id}");
                    }
                }
                AddSetCardBagIdea(SetCardBagType.CookingIdea, "blueprint_cooked_fish");
                AddSetCardBagIdea(SetCardBagType.Island_AdvancedFood, "blueprint_cooked_fish");
                AddSetCardBagIdea(SetCardBagType.Island_AdvancedFood, "blueprint_cooked_crab_meat");
                AddSetCardBagIdea(SetCardBagType.Death_AdvancedIdea, "blueprint_fabric_2");
            }
            Log($"Ready! {count} blueprints updated.");
        }

        private void AddSetCardBagIdea(SetCardBagType type, string blueprintId, int chance = 1)
        {
            SetCardBagData cardBagData = WorldManager.instance.GameDataLoader.SetCardBags.Find(x => x.SetCardBagType == type);
            if (cardBagData != null)
            {
                if (!cardBagData.Chances.Any(x => x.CardId == blueprintId))
                {
                    cardBagData.Chances.Add(new SimpleCardChance { CardId = blueprintId, Chance = chance });
                    Log($"...Added {blueprintId} to {type}");
                }
                else
                {
                    Log($"...{blueprintId} already found in {type}");
                }
            }
            else
            {
                Log($"...{type} not found.");
            }
        }
    }
/**
    [HarmonyPatch(typeof(WorldManager),nameof(WorldManager.HasFoundCard))]
    public class HasFoundCard
    {
        public static void Postfix(WorldManager __instance, bool __result, string cardId)
        {
            if (!__result && cardId != "blueprint_conveyor")
            {
                StrictModeMod.Instance.Log($"HasFoundCard Not {cardId}");
            }
        }
    }
**/
}