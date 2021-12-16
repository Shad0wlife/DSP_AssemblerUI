using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace DSP_AssemblerUI.AssemblerSpeedUI.Util
{
    public class FieldIndexFinder
    {
        /// <summary>
        /// Finds the two code matches that with high reliability represent the speed calculation parts that the transpiler uses for orientation.
        /// Searching for the patterns without the specific indices gives a slight risk of mismatch, 
        /// but checking the field indices of both matches against each other should still be very reliable.
        /// The found field indices are then taken and returned for the transpiler to use them.
        /// </summary>
        /// <param name="instructions">The instructions to search through.</param>
        /// <returns>A nullable ValueTuple (int, int, int), containing the 3 field indices on successful find, or null on failure.</returns>
        public static (int a, int b, int c)? FindRelevantFieldIndices(IEnumerable<CodeInstruction> instructions)
        {
            int first = -1;
            int second = -1;
            int third = -1;

            CodeMatcher matcher = new CodeMatcher(instructions);

            matcher.MatchForward(
                false,
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && i.OperandIs(60f)),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb)
            );

            if (matcher.IsValid)
            {
                matcher.Advance(1);
                first = ((LocalBuilder)matcher.Instruction.operand).LocalIndex;
                matcher.Advance(2);
                second = ((LocalBuilder)matcher.Instruction.operand).LocalIndex;
            }
            else
            {
                return null;
            }

            matcher.MatchForward(
                false,
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == second),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == second)
            );

            if (matcher.IsValid)
            {
                matcher.Advance(1);
                third = ((LocalBuilder)matcher.Instruction.operand).LocalIndex;

                return (first, second, third);
            }
            else
            {
                return null;
            }
        }
    }
}
