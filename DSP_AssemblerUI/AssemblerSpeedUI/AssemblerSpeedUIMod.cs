using BepInEx;
using BepInEx.Configuration;
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
    [BepInPlugin(ModInfo.ModID, ModInfo.ModName, ModInfo.VersionString)]
    public class AssemblerSpeedUIMod : BaseUnityPlugin
    {
        #region Main Plugin
        internal Harmony harmony;

        internal static ModLogger ModLogger;

        public static ConfigEntry<bool> configEnableOutputSpeeds;
        public static ConfigEntry<bool> configEnableInputSpeeds;
        public static ConfigEntry<bool> configShowLiveSpeed;

        internal void Awake()
        {
            //Adding the Logger
            var logger = new ManualLogSource(nameof(AssemblerSpeedUIMod));
            BepInEx.Logging.Logger.Sources.Add(Logger);
            ModLogger = new ModLogger(logger);

            configEnableOutputSpeeds = Config.Bind("General", "EnableOutputSpeedInfo", true, "Enables the speed information below the output area in the Assembler Window.");
            configEnableInputSpeeds = Config.Bind("General", "EnableInputSpeedInfo", true, "Enables the speed information above the input area in the Assembler Window.");
            configShowLiveSpeed = Config.Bind("General", "ShowLiveSpeedInfo", true, "True: shows current speed of production building. False: shows regular recipe speed of production building.");

            harmony = new Harmony(ModInfo.ModID);
            try
            {
                harmony.PatchAll(typeof(AssemblerSpeedUIMod));
                harmony.PatchAll(typeof(UiMinerWindowPatch));
            }
            catch(Exception ex)
            {
                ModLogger.ErrorLog(ex.Message);
                ModLogger.ErrorLog(ex.StackTrace);
            }
        }

        internal void OnDestroy()
        {
            harmony?.UnpatchSelf();

            foreach(KeyValuePair<string, ItemSpeedInfoLabel> pair in speedInfos)
            {
                Destroy(pair.Value.GameObject);
            }
        }

        #endregion

        #region Patcher

        internal static Dictionary<string, ItemSpeedInfoLabel> speedInfos = new Dictionary<string, ItemSpeedInfoLabel>();
        internal static int speedInfosOutCount = 0;
        internal static int speedInfosInCount = 0;

        static readonly string TEXT_PATH = "UI Root/Overlay Canvas/In Game/Windows/Assembler Window/produce/speed/speed-text";

        public const string outputKeyBase = "assembler-speed-out-item";
        public const string inputKeyBase = "assembler-speed-in-item";

        public static readonly string[] itemOutputKeys = { outputKeyBase + "0", outputKeyBase + "1", outputKeyBase + "2" };
        public static readonly string[] itemInputKeys = { inputKeyBase + "0", inputKeyBase + "1", inputKeyBase + "2" };

        public static Vector3? vanillaSpeedPos = null;

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

        /// <summary>
        /// Sets up all currently required and configured labels
        /// </summary>
        /// <param name="window">The UIAssemblerWindow for which the data shall be set up.</param>
        public static void SetupLabels(UIAssemblerWindow window)
        {
            //Output
            if (configEnableOutputSpeeds.Value)
            {
                int? productCount = window.factorySystem?.assemblerPool[window.assemblerId].products?.Length;
                if (productCount.HasValue)
                {
                    SetupSidedLabels(productCount.Value, false);
                }
            }

            //Input
            if (configEnableInputSpeeds.Value)
            {
                int? inputCount = window.factorySystem?.assemblerPool[window.assemblerId].requires?.Length;
                if (inputCount.HasValue)
                {
                    SetupSidedLabels(inputCount.Value, true);
                }
            }
        }

        /// <summary>
        /// Sets up the currently required labels for either input or output side
        /// </summary>
        /// <param name="itemCount">The number of items which currently need a label</param>
        /// <param name="isInput">Whether the labels are on the input or output side of the UI</param>
        public static void SetupSidedLabels(int itemCount, bool isInput)
        {
            string[] matchingKeys = isInput ? itemInputKeys : itemOutputKeys;
            int loopCap = Math.Min(itemCount, matchingKeys.Length);

            for (int cnt = 0; cnt < loopCap; cnt++)
            {
                if (!speedInfos.ContainsKey(matchingKeys[cnt]))
                {
                    AddSpeedLabel(matchingKeys[cnt], cnt, loopCap, isInput);
                    if (isInput)
                    {
                        speedInfosInCount++;
                    }
                    else
                    {
                        speedInfosOutCount++;
                    }
                }
            }

            string perMinuteString = "每分钟".Translate();
            int matchingInfoCount = isInput ? speedInfosInCount : speedInfosOutCount;

            //Iterate only over the already created text labels for the side
            for (int cnt2 = 0; cnt2 < matchingInfoCount; cnt2++)
            {
                if (cnt2 < itemCount)
                {
                    //If it is a label that should be visible, set it up
                    PositionSpeedLabel(speedInfos[matchingKeys[cnt2]].GameObject, cnt2, loopCap, isInput);
                    speedInfos[matchingKeys[cnt2]].GameObject.SetActive(true);
                    speedInfos[matchingKeys[cnt2]].Value.text = "  0.0" + perMinuteString;
                }
                else
                {
                    //If the label exists, but the current assembler doesn't use it, set it to inactive
                    speedInfos[matchingKeys[cnt2]].GameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Adds and initalizes a new speed label when it is needed.
        /// </summary>
        /// <param name="id">The dictionary ID of the new label</param>
        /// <param name="num">Index of the new label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        public static void AddSpeedLabel(string id, int num, int ofNum, bool input)
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

            if (!vanillaSpeedPos.HasValue)
            {
                vanillaSpeedPos = originalDetailLabel.transform.localPosition;
            }

            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);
            PositionSpeedLabel(gameObject, num, ofNum, input);
            gameObject.transform.right = originalDetailLabel.transform.right;

            //Input area is smaller, decrease font size
            if (input)
            {
                value.fontSize -= 2;
            }

            ItemSpeedInfoLabel newItemSpeedInfo = new ItemSpeedInfoLabel()
            {
                GameObject = gameObject,
                Value = value
            };
            speedInfos.Add(id, newItemSpeedInfo);
        }

        /// <summary>
        /// Sets the position of a given label.
        /// </summary>
        /// <param name="gameObject">The GameObject of the label to be moved.</param>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        public static void PositionSpeedLabel(GameObject gameObject, int num, int ofNum, bool input)
        {

            ModLogger.DebugLog($"OgPosition:{gameObject.transform.localPosition}");
            Vector3 shiftVector = getPosShift(num, ofNum, input);
            ModLogger.DebugLog($"ShiftedBy:{shiftVector}");

            gameObject.transform.localPosition = vanillaSpeedPos.Value + shiftVector;
        }

        /// <summary>
        /// Gets the Vector3 by which the label is shifted compared to the original speed label.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The Vector3 by which the label shall be shifted</returns>
        public static Vector3 getPosShift(int num, int ofNum, bool input)
        {
            float yShift = input ? 25f : -50f;
            float xShift = getXShift(num, ofNum, input);

            return new Vector3(xShift, yShift, 0f);
        }

        /// <summary>
        /// Calculates the x-Shift of a Label, based on the label count and whether it's on the input or output side.
        /// </summary>
        /// <param name="num">Index of the label (0-based)</param>
        /// <param name="ofNum">Count of total labels (max index + 1)</param>
        /// <param name="input">Whether the label is on the input or output side.</param>
        /// <returns>The x-Shift of the label</returns>
        private static float getXShift(int num, int ofNum, bool input)
        {
            //based on:
            //float[][] xOutputLookup = new float[][] { new float[] { -60f }, new float[] { -125f, -60f }, new float[] { -190f, -125f, -60f } };
            //float[][] xInputLookup = new float[][] { new float[] { 74 }, new float[] { 74f, 125f }, new float[] { 74f, 125f, 176f } };
            if (input)
            {
                float baseX = 74f;
                float itemStep = 49f;
                return baseX + num * itemStep;
            }
            else
            {
                float baseX = -60f;
                float itemStep = -65f;
                return baseX + (ofNum - 1 - num) * itemStep;
            }
        }

        /// <summary>
        /// Updates a singular label with the current speed data
        /// </summary>
        /// <param name="id">The Dict-ID of the label to update</param>
        /// <param name="value">The value which to write in the label</param>
        /// <param name="input"></param>
        public static void UpdateSpeedLabel(string id, float value, bool input)
        {
            var perMinuteString = "每分钟".Translate();
            var speedText = value.ToString("0.0").PadLeft(5) + perMinuteString;
            if (!input)
            {
                speedText += $" ({value / 60:0.0}/s)";
            }

            speedInfos[id].Value.text = speedText;
        }

        /// <summary>
        /// Update all the labels for the given base speed, as well as inputs and outputs
        /// </summary>
        /// <param name="baseSpeed">The base speed (number of recipe runs per minute)</param>
        /// <param name="productCounts">The array with info how many items of each product are created each run</param>
        /// <param name="requireCounts">The array with info how many items of each input are consumed each run</param>
        public static void UpdateSpeedLabels(float baseSpeed, int[] productCounts, int[] requireCounts)
        {
            //Output
            if (configEnableOutputSpeeds.Value)
            {
                for (int cnt = 0; cnt < Math.Min(productCounts.Length, itemOutputKeys.Length); cnt++)
                {
                    UpdateSpeedLabel(itemOutputKeys[cnt], productCounts[cnt] * baseSpeed, false);
                }
            }

            //Input
            if (configEnableInputSpeeds.Value)
            {
                for (int cnt = 0; cnt < Math.Min(requireCounts.Length, itemInputKeys.Length); cnt++)
                {
                    UpdateSpeedLabel(itemInputKeys[cnt], requireCounts[cnt] * baseSpeed, true);
                }
            }
        }


        [HarmonyTranspiler, HarmonyPatch(typeof(UIAssemblerWindow), "_OnUpdate")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator codeGen)
        {
            //declare local variable to keep our calculation base speed in
            LocalBuilder baseSpeedValue = codeGen.DeclareLocal(typeof(float));
            baseSpeedValue.SetLocalSymInfo("baseSpeedValue");

            //define label we later use for a branching instruction. label location is set separately
            Label noLiveData = codeGen.DefineLabel();

            ModLogger.DebugLog("UiTextTranspiler started!");
            CodeMatcher matcher = new CodeMatcher(instructions);
            ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");

            //find -->
            //ldc.r4 60
            //ldloc.s 17
            //div
            //stloc.s 18
            //<-- endFind
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && i.OperandIs(60f)),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 17),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 18)
            );
            matcher.Advance(1); //move from last match to next element

            //insert-->
            //ldloc.s 18
            //ldloca.s 0 //AssemblerComponent local var
            //ldfld int32 AssemblerComponent::speed
            //conv.r4
            //ldc.r4 0.0001
            //mul
            //mul
            //stloc baseSpeedValue
            //<-- endInsert
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)18), //Load base speed without power
                new CodeInstruction(OpCodes.Ldloca_S, (byte)0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("speed")), 
                new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Ldc_R4, 0.0001f), 
                new CodeInstruction(OpCodes.Mul), //scale device speed to usable factor (0.0001 * speed)
                new CodeInstruction(OpCodes.Mul), //multiply base (mk2) speed with factor
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue) //load value of config option
            );

            //find -->
            //ldloc.s 18
            //ldloc.s 19
            //mul
            //stloc.s 18
            //<-- endFind
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 18),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 19),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 18)
            );

            ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            matcher.Advance(1); //move from last match to next element

            //Create code instruction with target label for Brfalse
            CodeInstruction labelledInstruction = new CodeInstruction(OpCodes.Ldloc_S, baseSpeedValue);
            labelledInstruction.labels.Add(noLiveData);

            //insert-->
            //ldsfld ConfigEntry<bool> AssemblerSpeedUIMod.configShowLiveSpeed
            //callvirt int32 get_Value //or something like this. just run the getter of the Value Property
            //brfalse noLiveData //if Value is false, branch to the labelledInstruction
            //ldloc.s 18 //load calculated base speed incl. power
            //stloc.s baseSpeedValue //use that as calculation base

            //ldloc.s baseSpeedValue, label noLiveData
            //ldloca.s 0 //AssemblerComponent local var
            //ldfld int32[] AssemblerComponent::productCounts
            //ldloca.s 0 //AssemblerComponent local var
            //ldfld int32[] AssemblerComponent::requireCounts
            //call update
            //<-- endInsert
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, typeof(AssemblerSpeedUIMod).GetField("configShowLiveSpeed")), //Load config option
                new CodeInstruction(OpCodes.Callvirt, typeof(ConfigEntry<bool>).GetProperty("Value").GetGetMethod()), //load value of config option
                new CodeInstruction(OpCodes.Brfalse, noLiveData), //branch the speed loading (labelledInstruction) if setting is false
                new CodeInstruction(OpCodes.Ldloc_S, (byte)18),
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue),

                labelledInstruction, //Load base speed on stack
                new CodeInstruction(OpCodes.Ldloca_S, (byte)0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("productCounts")), //load product counts array on stack
                new CodeInstruction(OpCodes.Ldloca_S, (byte)0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("requireCounts")), //load require counts array on stack
                new CodeInstruction(OpCodes.Call, typeof(AssemblerSpeedUIMod).GetMethod("UpdateSpeedLabels"))
            );

            return matcher.InstructionEnumeration();
        }
        #endregion
    }
}
