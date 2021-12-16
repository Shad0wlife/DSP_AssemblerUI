using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx.Configuration;
using DSP_AssemblerUI.AssemblerSpeedUI.Util;
using HarmonyLib;

namespace DSP_AssemblerUI.AssemblerSpeedUI.Patchers
{
    public static class UiAssemblerWindowPatch
    {
        internal static AdditionalSpeedLabels additionalSpeedLabels;

        public static void UpdateSpeedLabels(float baseSpeed, int[] productCounts, int[] requireCounts)
        {
            additionalSpeedLabels.UpdateSpeedLabels(baseSpeed, productCounts, requireCounts);
        }

        private static void SetupLabels(UIAssemblerWindow window)
        {
            int? productCount = window.factorySystem?.assemblerPool[window.assemblerId].products?.Length;
            int? inputCount = window.factorySystem?.assemblerPool[window.assemblerId].requires?.Length;
            additionalSpeedLabels.SetupLabels(productCount, inputCount);
        }

        #region Patcher

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

        [HarmonyTranspiler, HarmonyPatch(typeof(UIAssemblerWindow), "_OnUpdate")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator codeGen)
        {
            //declare local variable to keep our calculation base speed in
            LocalBuilder baseSpeedValue = codeGen.DeclareLocal(typeof(float));
            baseSpeedValue.SetLocalSymInfo("baseSpeedValue");

            //define label we later use for a branching instruction. label location is set separately
            Label noLiveData = codeGen.DefineLabel();

            int divisor60, speedvalue, outputstringFactor;

            var searchResult = FieldIndexFinder.FindRelevantFieldIndices(instructions);
            if (searchResult.HasValue)
            {
                (divisor60, speedvalue, outputstringFactor) = searchResult.Value;
                AssemblerSpeedUIMod.ModLogger.DebugLog($"[UIAssemblerWindow] Found indices {divisor60}, {speedvalue}, {outputstringFactor}");
            }
            else
            {
                AssemblerSpeedUIMod.ModLogger.ErrorLog("[UIAssemblerWindow] Could not find the desired fields for patching the update logic.");
                return instructions;
            }

            AssemblerSpeedUIMod.ModLogger.DebugLog("UiTextTranspiler started!");
            CodeMatcher matcher = new CodeMatcher(instructions);
            AssemblerSpeedUIMod.ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");



            //find -->
            //ldc.r4 60
            //ldloc.s *divisor60*
            //div
            //stloc.s *speedvalue*
            //<-- endFind
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && i.OperandIs(60f)),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == divisor60),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == speedvalue)
            );
            matcher.Advance(1); //move from last match to next element

            //insert-->
            //ldloc.s *speedvalue*
            //ldloca.s 0 //AssemblerComponent local var
            //ldfld int32 AssemblerComponent::speed
            //conv.r4
            //ldc.r4 0.0001
            //mul
            //mul
            //stloc baseSpeedValue
            //<-- endInsert
            //This is the speed calculation also done in 0.0001 * assemblerComponent.speed * num [num is power], but without num
            //Whole calculation is done in float to avoid extra casting back from double to float at the end. Imprecision is too small to matter here
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)speedvalue), //Load base speed without power
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("speed")),
                new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Ldc_R4, 0.0001f),
                new CodeInstruction(OpCodes.Mul), //scale device speed to usable factor (0.0001 * speed)
                new CodeInstruction(OpCodes.Mul), //multiply base (mk2) speed with factor
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue) //load value of config option
            );

            //find -->
            //ldloc.s *speedvalue*
            //ldloc.s *outputstringFactor*
            //mul
            //stloc.s *speedvalue*
            //<-- endFind
            //This is the multiplication before the .ToString("0.0") + Translate when setting speedText.Text
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == speedvalue),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == outputstringFactor),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == speedvalue)
            );

            AssemblerSpeedUIMod.ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            matcher.Advance(1); //move from last match to next element

            //Create code instruction with target label for Brfalse
            CodeInstruction labelledInstruction = new CodeInstruction(OpCodes.Ldloc_S, baseSpeedValue);
            labelledInstruction.labels.Add(noLiveData);

            //insert-->
            //ldsfld ConfigEntry<bool> AssemblerSpeedUIMod.configShowLiveSpeed
            //callvirt int32 get_Value //or something like this. just run the getter of the Value Property
            //brfalse noLiveData //if Value is false, branch to the labelledInstruction
            //ldloc.s *speedvalue* //load calculated base speed incl. power
            //stloc.s baseSpeedValue //use that as calculation base

            //ldloc.s baseSpeedValue, label noLiveData
            //ldloc.0 //AssemblerComponent local var
            //ldfld int32[] AssemblerComponent::productCounts
            //ldloc.0 //AssemblerComponent local var
            //ldfld int32[] AssemblerComponent::requireCounts
            //call update
            //<-- endInsert
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, typeof(AssemblerSpeedUIMod).GetField("configShowLiveSpeed")), //Load config option
                new CodeInstruction(OpCodes.Callvirt, typeof(ConfigEntry<bool>).GetProperty("Value").GetGetMethod()), //load value of config option
                new CodeInstruction(OpCodes.Brfalse, noLiveData), //branch the speed loading (labelledInstruction) if setting is false
                new CodeInstruction(OpCodes.Ldloc_S, (byte)speedvalue), //Load the multiplied speed value the game uses (see last match)
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue),

                labelledInstruction, //Load base speed on stack
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("productCounts")), //load product counts array on stack
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("requireCounts")), //load require counts array on stack
                new CodeInstruction(OpCodes.Call, typeof(UiAssemblerWindowPatch).GetMethod("UpdateSpeedLabels")) //UpdateSpeedLabels(baseSpeed, productCounts[], requireCounts[])
            );

            AssemblerSpeedUIMod.ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");

            return matcher.InstructionEnumeration();
        }
        #endregion
    }
}
