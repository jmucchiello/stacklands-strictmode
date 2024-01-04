using CommonModNS;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace StrictModeModNS
{   
    [HarmonyPatch]
    public partial class StrictModeMod : Mod
    {
        public static StrictModeMod instance;
        public static void Log(string msg) => instance?.Logger.Log(msg);
        public static void LogError(string msg) => instance?.Logger.LogError(msg);

        public bool AllowNotifications => configAllowNotifications?.Value ?? true;

        private ConfigEntryBool ConfigIsStrict;
        private ConfigEntryBool configAllowNotifications;
        public bool SaveSetting = false;
        public bool IsStrict => SaveStateEnabled ? true : ConfigIsStrict.Value;
        public bool ClearOnStart = false;
        private bool? startingStrictness;

        public bool SaveStateEnabled { get; private set; } = false;
        public int IdeasOnSaveStart = -1;

        private readonly List<string> ModifyableBlueprints = new();

        enum RunStrict { DISABLE, ENABLE, ENABLENCLEARALL }
        private RunoptsEnum<RunStrict> runoptsStrict;
        SaveHelper saveHelper = new SaveHelper("StrictMode");

        private readonly string salt = Environment.MachineName + "?strictmode";
        private readonly string SaveKeyName = "strictmode";

        private void Awake()
        {
            instance = this;
            SavePatches();
            SetupConfig();
            SetupRunopts();
            Harmony.PatchAll();
            GameOverScreen_Patch.AddListener(GameOverText);
        }

        public override void Ready()
        {
            ApplySettings();
            Log("Ready");
        }

        private void SavePatches()
        {
            WorldManagerPatches.GetSaveRound += WM_GetSaveRound;  // save
            WorldManagerPatches.LoadSaveRound += WM_LoadSaveRound; // load
            WorldManagerPatches.StartNewRound += WM_StartNewRound; // new
            WorldManagerPatches.Play += WM_Play;
            WorldManagerPatches.ApplyPatches(Harmony);
            saveHelper.Ready(Path);
        }

        private void SetupConfig()
        {
            ConfigIsStrict = new ConfigEntryBool("strictmodemod_config_enable", Config, false, new ConfigUI()
            {
                NameTerm = "strictmodemod_config_enable",
                TooltipTerm = "strictmodemod_config_enable_tooltip"
            })
            {
                currentValueColor = Color.blue
            };

            configAllowNotifications = new ConfigEntryBool("strictmodemod_config_notifications", Config, true, new ConfigUI()
            {
                NameTerm = "strictmodemod_config_notifications",
                TooltipTerm = "strictmodemod_config_notifications_tooltip"
            })
            {
                currentValueColor = Color.blue
            };

        }

        private void SetupRunopts()
        {
            runoptsStrict = new("strictmodemod_runopt", RunStrict.DISABLE)
            {
                NameTerm = "strictmodemod_runopt",
                TooltipTerm = "strictmodemod_runopt_tooltip",
                EnumTermPrefix = "strictmodemod_runopt_",
                FontColor = Color.blue,
                FontSize = 20,
                Value = ConfigIsStrict.Value ? RunStrict.ENABLENCLEARALL : RunStrict.DISABLE
            };
            HookRunOptions.ApplyPatch(Harmony);
        }

        public override object Call(object[] args)
        {
            try
            {
                if (args.Length > 1)
                {
                    switch (args[0].ToString().ToLower())
                    {
                        case "skipblueprint":
                            List<string> strings = (List<string>)(args[1]);
                            break;
                        case "addprinttobag":
                            for (int i = 2; args.Length > i; i += 2)
                            {
                                SetCardBagType type = (SetCardBagType)args[i - 1];
                                AddRemoveSetCardBagIdea(type, args[i].ToString());
                            }
                            break;
                    }
                    
                }
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public void ApplySettings()
        {
            if (startingStrictness.HasValue && startingStrictness.Value == IsStrict) return;
            startingStrictness = IsStrict;

            if (ModifyableBlueprints.Count == 0)
            {
                List<string> skipIds = [Cards.blueprint_happiness, Cards.blueprint_greed_curse_fix, Cards.blueprint_death_curse_fix];
                foreach (Blueprint blueprint in I.WM.CardDataPrefabs.Where(x => x as Blueprint != null))
                {
                    if (!blueprint.Id.StartsWith("ideas_") && !skipIds.Any(x => x == blueprint.Id) && !blueprint.IsInvention)
                    {
                        ModifyableBlueprints.Add(blueprint.Id);
                    }
                }
            }
            Log($"ApplySettings - Strict Mode is {IsStrict}");
            foreach (Blueprint blueprint in I.WM.CardDataPrefabs.Where(b => ModifyableBlueprints.Contains(b.Id)))
            {
                blueprint.IsInvention = IsStrict;
                blueprint.nameOverride = IsStrict ? I.Xlat("label_idea_fullname", LocParam.Create("name", I.Xlat(blueprint.NameTerm))) : null;
            }
            List<string> newIdeas = [Cards.blueprint_cooked_fish, Cards.blueprint_cooked_crab_meat, Cards.blueprint_fill_bottle];
            foreach (string cardId in newIdeas)
            {
                Blueprint bp = I.WM.GetBlueprintWithId(cardId);
                if (bp == null)
                {
                    Log($"bp is null for cardid {cardId}");
                    continue;
                }
                bp.HideFromCardopedia = !IsStrict;
            }

            Call_Cardopedia_CreateEntries(CardopediaScreen.instance);
            AddRemoveSetCardBagIdea(SetCardBagType.CookingIdea, Cards.blueprint_cooked_fish);
            AddRemoveSetCardBagIdea(SetCardBagType.Island_BasicFood, Cards.blueprint_fill_bottle);
            AddRemoveSetCardBagIdea(SetCardBagType.Island_AdvancedFood, Cards.blueprint_cooked_fish);
            AddRemoveSetCardBagIdea(SetCardBagType.Island_AdvancedFood, Cards.blueprint_cooked_crab_meat);
            AddRemoveSetCardBagIdea(SetCardBagType.Death_AdvancedIdea, Cards.blueprint_fabric_2);
        }

        private void Call_Cardopedia_CreateEntries(CardopediaScreen instance)
        {
            if (instance == null)
            {
                I.Log("CardopediaScreen.instance is null");
                return;
            }
            MethodInfo mi = AccessTools.Method(typeof(CardopediaScreen), "CreateEntries");
            if (mi != null)
            {
                mi.Invoke(instance, new object[0]);
            }
            else
            {
                I.Log("Failed to get MI for CardopediaScreen.CreateEntries");
            }
        }

        private void AddRemoveSetCardBagIdea(SetCardBagType type, string blueprintId, int chance = 1)
        {
            SetCardBagData cardBagData = WorldManager.instance.GameDataLoader.SetCardBags.Find(x => x.SetCardBagType == type);
            if (cardBagData != null)
            {
                if (IsStrict)
                {
                    if (!cardBagData.Chances.Any(x => x.CardId == blueprintId))
                    {
                        cardBagData.Chances.Add(new SimpleCardChance { CardId = blueprintId, Chance = chance });
                    }
                }
                else
                {
                    cardBagData.Chances = cardBagData.Chances.Where<SimpleCardChance>(x => x.CardId != blueprintId).ToList();
                }
            }
            else
            {
                Log($"...{type} not found.");
            }
        }

        private string GameOverText()
        {
            string text = "";
            if (SaveStateEnabled)
            {
                text = "<size=25>" + I.Xlat("strictmodemod_gameover", LocParam.Plural("ideas", IdeasOnSaveStart)) + "</size>\n";
            }
            return text;
        }

        private void WM_StartNewRound(WorldManager wm)
        {
            if (!runoptsStrict.Enabled) SaveStateEnabled = false;
            else if (runoptsStrict.Value == RunStrict.ENABLENCLEARALL)
            {
                //I.Log($"NewRound 1 {(wm.CurrentSave == null ? "cursave is null" : wm.CurrentSave.DisabledMods == null ? "disabled mods is null" : wm.CurrentSave.DisabledMods.Count.ToString())}");
                List<string> disabledMods = wm.CurrentSave?.DisabledMods;
                //I.Log($"NewRound 2");
                wm.ClearSaveAndRestart();
                //I.Log($"NewRound 3 {(wm.AllBoosterBoxes == null ? "allboosters is null" : wm.AllBoosterBoxes.Count.ToString())}");
                if (disabledMods != null && wm.CurrentSave != null) wm.CurrentSave.DisabledMods = disabledMods;
                SaveStateEnabled = true;
            }
            else
            {
                SaveStateEnabled = runoptsStrict.Value != RunStrict.DISABLE;
            }
            if (wm.AllBoosterBoxes != null)
            {
                foreach (BuyBoosterBox bbb in wm.AllBoosterBoxes)
                {
                    bbb.StoredCostAmount = 0;
                }
            }

            IdeasOnSaveStart = SaveStateEnabled ? wm.CurrentSave?.FoundCardIds?.Count(x => wm.BlueprintPrefabs.Find(b => x == b.Id && !b.HideFromIdeasTab)) ?? 0 : -1;
            
            I.Log("Calling Notification from StartNewRound");
            OnLoadNotification();
        }

        private string ConstructSaveData(SaveRound round, int ideasCount)
        {
            string ideas = ideasCount.ToString();
            List<string> strings = new List<string>();
            strings.Add(round.SavedCards.Count.ToString());
            strings.Add(round.BoardMonths.MainMonth.ToString());
            strings.Add(round.MonthTimer.ToString());
            strings.Add(ideas);
            string x = String.Join(" ", strings) + " ";
            string hash = (salt + ":" + x).GetHashCode().ToString() + ":" + ideas;
            return hash;
        }

        private void DecodeSaveData(string hash, SaveRound round)
        {
            int pos = hash.IndexOf(":");
            IdeasOnSaveStart = Int32.Parse(hash.Substring(pos + 1));
            if (hash != ConstructSaveData(round, IdeasOnSaveStart))
            {
                SaveStateEnabled = false;
                IdeasOnSaveStart = -1;
                return;
            }
            SaveStateEnabled = true;
        }


        private void WM_LoadSaveRound(WorldManager wm, SaveRound saveRound)
        {
            string save = saveRound.ExtraKeyValues.Find(x => x.Key == SaveKeyName)?.Value;
            if (String.IsNullOrEmpty(save))
            {
                SaveStateEnabled = false;
                IdeasOnSaveStart = -1;
            }
            else
            {
                Log($"Calling decode on save data:\n{save}");
                DecodeSaveData(save, saveRound);
                Log($"SaveEnabled: {(SaveStateEnabled ? "On" : "Off")}, IdeasCount: {IdeasOnSaveStart}");
            }
            Log("Calling Notification from LoadSaveRound");
            OnLoadNotification();
        }

        private void WM_GetSaveRound(WorldManager wm, SaveRound saveRound)
        {
            if (SaveStateEnabled)
            {
                string value = ConstructSaveData(saveRound, IdeasOnSaveStart);
                Log($"Construct Save Data Returned:\n{value}");
                if (value != null)
                {
                    saveRound.ExtraKeyValues.SetOrAdd(SaveKeyName, value);
                }
            }
            else
            {
                var keyvalue = saveRound.ExtraKeyValues.Find(x => x.Key == SaveKeyName);
                if (keyvalue != null) 
                    saveRound.ExtraKeyValues.Remove(keyvalue);
            }
        }

        private void WM_Play(WorldManager wm)
        {
            ApplySettings();
        }

        public void OnLoadNotification()
        {
            if (!AllowNotifications) return;
            if (SaveStateEnabled)
            {
                int currentIdeaCount = I.WM.CurrentSave.FoundCardIds.Count(x => I.WM.BlueprintPrefabs.Find(b => x == b.Id && !b.HideFromIdeasTab));
                I.GS.AddNotification(I.Xlat("strictmode_notify"),
                                     "<size=22>" + I.Xlat("strictmode_setting_ON", LocParam.Create("enabled", I.Xlat("label_on"))
                                                                   , LocParam.Create("count", IdeasOnSaveStart < 0 ? "Unknown" : IdeasOnSaveStart.ToString())
                                                                   , LocParam.Create("current", currentIdeaCount.ToString())) + "</size>");
            }
            else
            {
                I.GS.AddNotification(I.Xlat("strictmode_notify"),
                                     I.Xlat("strictmode_setting_OFF", LocParam.Create("enabled", ConfigIsStrict.Value ? I.Xlat("label_on") : I.Xlat("label_off"))));
            }
        }
    }
}