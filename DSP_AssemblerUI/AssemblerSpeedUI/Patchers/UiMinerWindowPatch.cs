using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using DSP_AssemblerUI.AssemblerSpeedUI.Util;
using HarmonyLib;

namespace DSP_AssemblerUI.AssemblerSpeedUI.Patchers
{
    public static class UiMinerWindowPatch
    {
        internal static AdditionalSpeedLabels additionalSpeedLabels;

        [HarmonyPostfix, HarmonyPatch(typeof(UIMinerWindow), "OnMinerIdChange")]
        public static void OnMinerIdChangePostfix()
        {
            additionalSpeedLabels.SetupLabels(1, null);
        }

        [HarmonyTranspiler, HarmonyPatch(typeof(UIMinerWindow), "_OnUpdate")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator codeGen)
        {
            //declare local variable to keep our calculation base speed in
            LocalBuilder baseSpeedValue = codeGen.DeclareLocal(typeof(float));
            baseSpeedValue.SetLocalSymInfo("baseSpeedValue");

            //declare local variable to keep an additional factor in
            LocalBuilder miningSpeedFactor = codeGen.DeclareLocal(typeof(float));
            baseSpeedValue.SetLocalSymInfo("miningSpeedFactor");

            //define label we later use for a branching instruction. label location is set separately
            Label noLiveData = codeGen.DefineLabel();
            Label loadForDisplay = codeGen.DefineLabel();

            int divisor60, speedvalue, outputstringFactor;

            var searchResult = FieldIndexFinder.FindRelevantFieldIndices(instructions);
            if (searchResult.HasValue)
            {
                (divisor60, speedvalue, outputstringFactor) = searchResult.Value;
                AssemblerSpeedUIMod.ModLogger.DebugLog($"[UIMinerWindow] Found indices {divisor60}, {speedvalue}, {outputstringFactor}");
            }
            else
            {
                AssemblerSpeedUIMod.ModLogger.ErrorLog("[UIMinerWindow] Could not find the desired fields for patching the update logic.");
                return instructions;
            }

            AssemblerSpeedUIMod.ModLogger.DebugLog("Miner UiTextTranspiler started!");
            CodeMatcher matcher = new CodeMatcher(instructions);
            AssemblerSpeedUIMod.ModLogger.DebugLog($"Miner UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");

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

            //This is the speed calculation also done in 0.0001 * minerComponent.speed * num2 [num is power factor] * miningSpeedScale, but without num2
            //Whole calculation is done in float to avoid extra casting back from double to float at the end. Imprecision is too small to matter here
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldc_R4, 0.0001f),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(MinerComponent).GetField("speed")),
                new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Mul), //scale device speed to usable factor (0.0001 * speed)
                new CodeInstruction(OpCodes.Call, typeof(GameMain).GetProperty("history").GetGetMethod()), //get Upgrade data from Game Data
                new CodeInstruction(OpCodes.Ldfld, typeof(GameHistoryData).GetField("miningSpeedScale")), //get Mining speed multiplier (is float already!)
                new CodeInstruction(OpCodes.Mul), //multiply with miningSpeedScale
                new CodeInstruction(OpCodes.Ldloc_S, (byte)speedvalue),
                new CodeInstruction(OpCodes.Mul), //multiply base speed with factor

                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue), //store base speed
                new CodeInstruction(OpCodes.Ldc_R4, 1.0f), //load factor 1 as default speed factor here
                new CodeInstruction(OpCodes.Stloc_S, miningSpeedFactor) //save that 1 as the default factor
            );

            //Find vein multiplier
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo fi && fi.Name == "veinCount" && fi.FieldType == typeof(int)),
                new CodeMatch(i => i.opcode == OpCodes.Conv_R4)
            );
            matcher.Advance(1); //move from last match to next element

            //Save vein multiplier as factor if it is used (this code is in the "if", so it will only store the factor in there if it is a vein miner
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Stloc_S, miningSpeedFactor),
                new CodeInstruction(OpCodes.Ldloc_S, miningSpeedFactor)
            );

            //Find oil Multiplier
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldsfld && i.operand is FieldInfo fi && fi.Name == "oilSpeedMultiplier" && fi.FieldType == typeof(float)),
                new CodeMatch(i => i.opcode == OpCodes.Conv_R8),
                new CodeMatch(i => i.opcode == OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Conv_R4)

            );
            matcher.Advance(1); //move from last match to next element


            //Save oil multiplier as factor if it is used (this code is in the "if", so it will only store the factor in there if it is a vein miner
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Stloc_S, miningSpeedFactor),
                new CodeInstruction(OpCodes.Ldloc_S, miningSpeedFactor)
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

            //Create code instruction with target label for Brfalse
            CodeInstruction targetInstructionNoLiveData = new CodeInstruction(OpCodes.Ldloc_S, baseSpeedValue);
            targetInstructionNoLiveData.labels.Add(noLiveData);

            //Create code instruction with target label for Br
            CodeInstruction targetInstructionLoadForDisplay = new CodeInstruction(OpCodes.Ldloc_S, baseSpeedValue);
            targetInstructionLoadForDisplay.labels.Add(loadForDisplay);


            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldsfld, typeof(AssemblerSpeedUIMod).GetField("configShowMinerLiveSpeed")), //Load config option
                new CodeInstruction(OpCodes.Callvirt, typeof(ConfigEntry<bool>).GetProperty("Value").GetGetMethod()), //load value of config option
                new CodeInstruction(OpCodes.Brfalse, noLiveData), //branch the speed saving to (labelledInstruction) if above config setting is true, so no power data gets cleaned
                new CodeInstruction(OpCodes.Ldloc_S, (byte)speedvalue),
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue),
                new CodeInstruction(OpCodes.Br, loadForDisplay),
                targetInstructionNoLiveData,
                new CodeInstruction(OpCodes.Ldloc_S, miningSpeedFactor),
                new CodeInstruction(OpCodes.Mul),
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue),
                targetInstructionLoadForDisplay, //load base speed
                new CodeInstruction(OpCodes.Call, typeof(UiMinerWindowPatch).GetMethod("UpdateSpeedLabel", new Type[] { typeof(float) })) //UpdateSpeedLabel(baseSpeed)
            );

            return matcher.Instructions();
        }

        public static void UpdateSpeedLabel(float baseSpeed)
        {
            additionalSpeedLabels.UpdateSpeedLabel($"assembler-speed-out-item0", baseSpeed, AssemblerSpeedUIMod.configMinerSpeedsPerSecond.Value);
        }
    }
}
