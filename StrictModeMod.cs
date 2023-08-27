using HarmonyLib;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace StrictModeModNS
{
    public class StrictModeMod : Mod
    {
        public ConfigEntry<bool> IsStrict;
        private void Awake()
        {
            IsStrict = Config.GetEntry<bool>("strictmodemod_config_enable", false,
                new ConfigUI() { NameTerm = "strictmodemod_config_enable",
                    TooltipTerm = "strictmodemod_config_enable_tooltip",
                    RestartAfterChange = true 
                });
        }
        public override void Ready()
        {
            if (IsStrict.Value)
            {
                foreach (Blueprint blueprint in WorldManager.instance.BlueprintPrefabs)
                {
                    blueprint.IsInvention = true;
                }
            }
            Logger.Log($"Strict Mode is {(IsStrict.Value ? "Enabled" : "Disabled")}");
        }
    }
}