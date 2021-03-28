using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    [BepInPlugin("dsp.assemblerUI.speedMod", "Assembler UI Speed Info Mod", "1.0.0.0")]
    public class AssemblerSpeedUIMod : BaseUnityPlugin
    {
        #region Main Plugin
        public new static ManualLogSource Logger;

        internal void Awake()
        {

            //Adding the Logger
            Logger = new ManualLogSource("AssemblerSpeedUIMod");
            BepInEx.Logging.Logger.Sources.Add(Logger);

            Harmony.CreateAndPatchAll(typeof(AssemblerSpeedUIMod));
        }

        [Conditional("DEBUG")]
        static void DebugLog(string logMessage)
        {
            Logger.LogDebug(logMessage);
        }
        #endregion

        #region Patcher
        internal class ItemSpeedInfoLabel
        {
            public GameObject gameObject;
            public Text value;
        }

        internal static Dictionary<string, ItemSpeedInfoLabel> speedInfos = new Dictionary<string, ItemSpeedInfoLabel>();

        static readonly string TEXT_PATH = "UI Root/Overlay Canvas/In Game/Windows/Assembler Window/produce/speed/speed-text";

        public static readonly string[] itemKeys = { "assembler-speed-item0", "assembler-speed-item1", "assembler-speed-item2" };

        public static float[][] xLookup = new float[][]{ new float[]{ -60f }, new float[]{ -125f, -60f }, new float[] { -190f, -125f, -60f } };
        public static Vector3? ogPos = null;

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnAssemblerIdChange")]
        public static void OnAssemblerIdChangePostfix(UIAssemblerWindow __instance)
        {
            SetupLabels(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipePickerReturn")]
        public static void OnRecipePickerReturnPostfix(UIAssemblerWindow __instance)
        {
            SetupLabels(__instance);
        }

        public static void SetupLabels(UIAssemblerWindow window)
        {
            int? productCount = window.factorySystem?.assemblerPool[window.assemblerId].products?.Length;
            if (!productCount.HasValue)
            {
                return;
            }
            int loopCap = Math.Min(productCount.Value, itemKeys.Length);

            for (int cnt = 0; cnt < loopCap; cnt++)
            {
                if (!speedInfos.ContainsKey(itemKeys[cnt]))
                {
                    AddSpeedLabel(itemKeys[cnt], cnt, loopCap);
                }
            }

            string perMinuteString = "每分钟".Translate();

            for (int cnt2 = 0; cnt2 < speedInfos.Count; cnt2++)
            {
                if (cnt2 < productCount)
                {
                    speedInfos[itemKeys[cnt2]].gameObject.SetActive(true);
                    speedInfos[itemKeys[cnt2]].value.text = "0.0" + perMinuteString;
                    PositionSpeedLabel(speedInfos[itemKeys[cnt2]].gameObject, cnt2, loopCap);
                }
                else
                {
                    speedInfos[itemKeys[cnt2]].gameObject.SetActive(false);
                }
            }
        }

        public static void AddSpeedLabel(string id, int num, int ofNum)
        {
            var originalDetailLabel = GameObject.Find(TEXT_PATH);
            if (originalDetailLabel == null)
            {
                throw new InvalidOperationException("Assembler speed base entry is not present");
            }

            GameObject gameObject = null;
            var originalDetailLabelText = originalDetailLabel.GetComponent<Text>();

            gameObject = Instantiate(originalDetailLabel, originalDetailLabel.transform.position, Quaternion.identity);
            Destroy(gameObject.GetComponentInChildren<Localizer>());

            gameObject.name = id;
            gameObject.transform.SetParent(originalDetailLabel.transform.parent);

            var textComponents = gameObject.GetComponentsInChildren<Text>();
            var value = textComponents[0];

            if (!ogPos.HasValue)
            {
                ogPos = originalDetailLabel.transform.localPosition;
            }

            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            PositionSpeedLabel(gameObject, num, ofNum);
            gameObject.transform.right = originalDetailLabel.transform.right;

            ItemSpeedInfoLabel newItemSpeedInfo = new ItemSpeedInfoLabel()
            {
                gameObject = gameObject,
                value = value
            };
            speedInfos.Add(id, newItemSpeedInfo);
        }

        public static void PositionSpeedLabel(GameObject gameObject, int num, int ofNum)
        {

            DebugLog($"OgPosition:{gameObject.transform.localPosition}");
            Vector3 shiftVector = getPosShift(num, ofNum);
            DebugLog($"ShiftedBy:{shiftVector}");

            gameObject.transform.localPosition = ogPos.Value + shiftVector;
        }

        public static Vector3 getPosShift(int num, int ofNum)
        {
            return new Vector3(xLookup[ofNum - 1][num], -50f, 0f);
        }

        public static void UpdateSpeedLabel(string id, float value)
        {
            string perMinuteString = "每分钟".Translate();
            speedInfos[id].value.text = value.ToString("0.0") + perMinuteString;
        }

        public static void UpdateSpeedLabels(float baseSpeed, int[] productCounts)
        {
            for(int cnt = 0; cnt < Math.Min(productCounts.Length, itemKeys.Length); cnt++)
            {
                UpdateSpeedLabel(itemKeys[cnt], productCounts[cnt]*baseSpeed);
            }
        }


        [HarmonyTranspiler, HarmonyPatch(typeof(UIAssemblerWindow), "_OnUpdate")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            DebugLog("UiTextTranspiler started!");
            CodeMatcher matcher = new CodeMatcher(instructions);
            //find -->
            //ldloc.s 18
            //ldloc.s 19
            //mul
            //stloc.s 18
            //<-- endFind
            DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 18),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 19),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 18)
            );

            DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            matcher.Advance(1); //move from last match to next element
            //insert-->
            //ldloc.s 18
            //ldloca.s 0 //AssemblerComponent local var
            //ldfld int32[] AssemblerComponent::productCounts
            //call update
            //<-- endInsert
            DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)18),
                new CodeInstruction(OpCodes.Ldloca_S, (byte)0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("productCounts")),
                new CodeInstruction(OpCodes.Call, typeof(AssemblerSpeedUIMod).GetMethod("UpdateSpeedLabels"))
            );

            return matcher.InstructionEnumeration();
        }
        #endregion
    }
}
