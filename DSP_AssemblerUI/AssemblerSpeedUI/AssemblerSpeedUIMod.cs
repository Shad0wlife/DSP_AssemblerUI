using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

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
        public static ConfigEntry<bool> configInputSpeedsPerSecond;
        public static ConfigEntry<bool> configOutputSpeedsPerSecond;
        public static ConfigEntry<bool> configShowLiveSpeed;

        public static ConfigEntry<bool> configShowMinerSpeed;
        public static ConfigEntry<bool> configShowMinerLiveSpeed;
        public static ConfigEntry<bool> configMinerSpeedsPerSecond;

        private static AdditionalSpeedLabels additionalSpeedLabels;
        
        internal void Awake()
        {
            //Adding the Logger
            var logger = new ManualLogSource("AssemblerSpeedUIMod");
            BepInEx.Logging.Logger.Sources.Add(logger);
            ModLogger = new ModLogger(logger);

            configEnableOutputSpeeds = Config.Bind("General", "EnableOutputSpeedInfo", true, "Enables the speed information below the output area in the Assembler Window.");
            configEnableInputSpeeds = Config.Bind("General", "EnableInputSpeedInfo", true, "Enables the speed information above the input area in the Assembler Window.");

            configOutputSpeedsPerSecond = Config.Bind("General", "EnableOutputSpeedInfoPerSecond", false, "Sets the output speeds shown in Assemblers to items/s (default: items/min).");
            configInputSpeedsPerSecond = Config.Bind("General", "EnableInputSpeedInfoPerSecond", false, "Sets the input speeds shown in Assemblers to items/s (default: items/min).");

            configShowLiveSpeed = Config.Bind("General", "ShowLiveSpeedInfo", false, "True: shows current speed of production building. False: shows regular recipe speed of production building.");

            configShowMinerSpeed = Config.Bind("Miner", "EnableMinerSpeedInfo", true, "Enables the speed information below the output area in the Miner Window.");
            configShowMinerLiveSpeed = Config.Bind("Miner", "ShowMinerLiveSpeedInfo", false, "True: shows current speed of production building. False: shows regular recipe speed of production building.");
            configMinerSpeedsPerSecond = Config.Bind("Miner", "EnableMinerOutputSpeedInfoPerSecond", false, "Sets the output speeds shown in Miners to items/s (default: items/min).");

            additionalSpeedLabels = new AdditionalSpeedLabels(ModLogger, configEnableOutputSpeeds.Value, configEnableInputSpeeds.Value, Constants.AssemblerWindowSpeedTextPath);
            UiMinerWindowPatch.additionalSpeedLabels = new AdditionalSpeedLabels(ModLogger, configShowMinerSpeed.Value, false, Constants.MinerWindowSpeedTextPath);

            harmony = new Harmony(ModInfo.ModID);
            try
            {
                ModLogger.DebugLog("Patching AssemblerUI");
                harmony.PatchAll(typeof(AssemblerSpeedUIMod));

                ModLogger.DebugLog("Patching MinerUI");
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

            additionalSpeedLabels.Destroy();
        }

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
        
        #endregion

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

            ModLogger.DebugLog("UiTextTranspiler started!");
            CodeMatcher matcher = new CodeMatcher(instructions);
            ModLogger.DebugLog($"UiTextTranspiler Matcher Codes Count: {matcher.Instructions().Count}, Matcher Pos: {matcher.Pos}!");

            //find -->
            //ldc.r4 60
            //ldloc.s 6
            //div
            //stloc.s 7
            //<-- endFind
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldc_R4 && i.OperandIs(60f)),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 6),
                new CodeMatch(OpCodes.Div),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 7)
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
            //This is the speed calculation also done in 0.0001 * assemblerComponent.speed * num [num is power], but without num
            //Whole calculation is done in float to avoid extra casting back from double to float at the end. Imprecision is too small to matter here
            matcher.InsertAndAdvance(
                new CodeInstruction(OpCodes.Ldloc_S, (byte)7), //Load base speed without power
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("speed")), 
                new CodeInstruction(OpCodes.Conv_R4),
                new CodeInstruction(OpCodes.Ldc_R4, 0.0001f), 
                new CodeInstruction(OpCodes.Mul), //scale device speed to usable factor (0.0001 * speed)
                new CodeInstruction(OpCodes.Mul), //multiply base (mk2) speed with factor
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue) //load value of config option
            );

            //find -->
            //ldloc.s 7
            //ldloc.s 8
            //mul
            //stloc.s 7
            //<-- endFind
            //This is the multiplication before the .ToString("0.0") + Translate when setting speedText.Text
            matcher.MatchForward(
                true,
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 7),
                new CodeMatch(i => i.opcode == OpCodes.Ldloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 8),
                new CodeMatch(OpCodes.Mul),
                new CodeMatch(i => i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder lb && lb.LocalIndex == 7)
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
                new CodeInstruction(OpCodes.Ldloc_S, (byte)7), //Load the multiplied speed value the game uses (see last match)
                new CodeInstruction(OpCodes.Stloc_S, baseSpeedValue),

                labelledInstruction, //Load base speed on stack
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("productCounts")), //load product counts array on stack
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(OpCodes.Ldfld, typeof(AssemblerComponent).GetField("requireCounts")), //load require counts array on stack
                new CodeInstruction(OpCodes.Call, typeof(AssemblerSpeedUIMod).GetMethod("UpdateSpeedLabels")) //UpdateSpeedLabels(baseSpeed, productCounts[], requireCounts[])
            );

            return matcher.InstructionEnumeration();
        }
        #endregion
    }
}
