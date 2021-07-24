using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;
using HarmonyLib;

namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    public class UiMinerWindowPatch
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
            var matcher = new CodeMatcher(instructions);

            matcher.MatchForward(false, 
                                 new CodeMatch(i => i.opcode == OpCodes.Ldloca_S && i.operand is LocalBuilder lb && lb.LocalIndex == 0),
                                 new CodeMatch(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("type")),
                                 new CodeMatch(OpCodes.Ldc_I4_2),
                                 new CodeMatch(instruction => instruction.opcode == OpCodes.Bne_Un),
                                 new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 21),
                                 new CodeMatch(i => i.opcode == OpCodes.Ldloca_S && i.operand is LocalBuilder lb && lb.LocalIndex == 0),
                                 new CodeMatch(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("veinCount"))
                );

            matcher.Advance(-1);
            AssemblerSpeedUIMod.ModLogger.DebugLog($"{nameof(UiMinerWindowPatch)} Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");
            
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc, (byte) 2), 
                new CodeInstruction(OpCodes.Ldloc, (byte) 0), 
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UiMinerWindowPatch), nameof(UpdateSpeedLabel)))
                );
            
            return matcher.Instructions();
        }

        private static void UpdateSpeedLabel(VeinData[] veinPool, MinerComponent minerComponent)
        {
            var speed = CalcSpeed(veinPool, minerComponent);
            additionalSpeedLabels.UpdateSpeedLabel($"assembler-speed-out-item0", speed, false);
        }
        
        private static float CalcSpeed(VeinData[] veinPool, MinerComponent minerComponent)
        {
            var periodSec = (float)(minerComponent.period / 600000.0);
            var periodMin = 60f / periodSec;
            //LogDebug($"s: {minerComponent.speed}, sc: {GameMain.history.miningSpeedScale}, vc: {minerComponent.veinCount}, p: {minerComponent.period} ({periodSec}, {periodMin})");
            var speed = (float) (0.0001 * minerComponent.speed * GameMain.history.miningSpeedScale * periodMin);
            if (minerComponent.type == EMinerType.Vein)
            {
                speed *= minerComponent.veinCount;
            }
            else if (minerComponent.type == EMinerType.Oil)
            {
                var veinIndex = minerComponent.veinCount != 0 ? minerComponent.veins[minerComponent.currentVeinIndex] : 0;
                speed *= (float) (veinPool[veinIndex].amount * (double) VeinData.oilSpeedMultiplier);
            }

            return speed;
        }
    }
}
