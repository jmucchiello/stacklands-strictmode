using CommonModNS;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace StrictModeModNS
{
    public partial class StrictModeMod
    {
        static void SetupButton(CustomButton button, string text = null, string tooltip = null)
        {
            button.transform.localPosition = Vector3.zero;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one;
            if (!String.IsNullOrEmpty(text)) button.TextMeshPro.text = text;
            if (!String.IsNullOrEmpty(tooltip)) button.TooltipText = tooltip;
        }

        [HarmonyPatch(typeof(RunOptionsScreen), "Start")]
        [HarmonyPostfix]
        static void RunOptionsScreen_Start()
        {
            try
            {
                Transform rosEntriesParent = GameCanvas.instance.transform.Find("RunOptionsScreen/Background/Buttons");
                List<Transform> list = new List<Transform>();
                for (int i = 0; i < rosEntriesParent.childCount; ++i)
                {
                    list.Add(rosEntriesParent.GetChild(i));
                }
                rosEntriesParent.DetachChildren();
                CustomButton strictBtn = UnityEngine.Object.Instantiate(PrefabManager.instance.ButtonPrefab);
                CustomButton clearBtn = UnityEngine.Object.Instantiate(PrefabManager.instance.ButtonPrefab);
                Transform label = null;
                foreach (Transform t in list)
                {
                    t.SetParent(rosEntriesParent);
                    if (t.name == "Label" && label == null)
                    {
                        label = UnityEngine.Object.Instantiate(t);
                    }
                    if (t.gameObject.name == "Spacer" && label != null)
                    {
                        label.transform.SetParent(rosEntriesParent);
                        strictBtn.transform.SetParent(rosEntriesParent);
                        clearBtn.transform.SetParent(rosEntriesParent);
                        Transform spacer2 = UnityEngine.Object.Instantiate(t);
                        spacer2.SetParent(rosEntriesParent);
                    }
                }
                if (label != null)
                {
                    label.GetComponent<TMPro.TextMeshProUGUI>().text = I.Xlat("strictmode_label");
                    label.transform.localPosition = Vector3.zero;
                    label.transform.localRotation = Quaternion.identity;
                    label.transform.localScale = Vector3.one;
                }
                SetupButton(strictBtn, RunOptionsScreen_Text(), SokLoc.Translate("strictmode_tooltip"));
                strictBtn.Clicked += () =>
                {
                    instance.SaveStateEnabled = !instance.SaveStateEnabled;
                    strictBtn.TextMeshPro.text = RunOptionsScreen_Text();
                    strictBtn.TooltipText = I.Xlat($"strictmode_tooltip_{instance.SaveStateEnabled}");
                    clearBtn.enabled = instance.SaveStateEnabled;
                    clearBtn.TextMeshPro.text = RunOptionsScreen_ClearBtnText();
                };
                SetupButton(clearBtn, RunOptionsScreen_ClearBtnText(), SokLoc.Translate("strictmode_clear_tooltip"));
                clearBtn.enabled = instance.SaveStateEnabled;
                clearBtn.Clicked += () =>
                {
                    if (instance.ClearOnStart == ClearState.ALL) instance.ClearOnStart = ClearState.NEVER;
                    else ++instance.ClearOnStart;
                    clearBtn.TextMeshPro.text = RunOptionsScreen_ClearBtnText();
                };
            }
            catch (Exception ex)
            {
                I.Log("Exception caught modifying RunOptionScreen.Start" + ex.Message);
            }
        }

        static string RunOptionsScreen_Text()
        {
            return "<size=25>" + I.Xlat("strictmode_btntext") + ": <color=blue>" +
                   I.Xlat(instance.SaveStateEnabled ? "label_on" : "label_off") + "</color></size>";
        }

        static string RunOptionsScreen_ClearBtnText()
        {
            if (instance.SaveStateEnabled)
            {
                return "<size=25>" + I.Xlat("strictmode_clearbtntext") + ": <color=blue>" + I.Xlat($"strictmode_clear_{instance.ClearOnStart}") + "</color></size>";
            }
            return "<size=25><s>" + I.Xlat("strictmode_clearbtndisabled") + "</s></size>";
        }


    }
}
