using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;

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
        public static ConfigEntry<uint> configShownDecimalPlaces;

        public static ConfigEntry<bool> configShowMinerSpeed;
        public static ConfigEntry<bool> configShowMinerLiveSpeed;
        public static ConfigEntry<bool> configMinerSpeedsPerSecond;
        
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

            configShownDecimalPlaces = Config.Bind("General", "NumberOfDecimalsShown", (uint)1,
                new ConfigDescription(
                    $"Sets the number of decimal places shown for speed values. Value must be in range [{Constants.MIN_DECIMAL_PLACES},{Constants.MAX_DECIMAL_PLACES}].",
                    new AcceptableValueRange<uint>(Constants.MIN_DECIMAL_PLACES, Constants.MAX_DECIMAL_PLACES)
                )
            );

            configShowMinerSpeed = Config.Bind("Miner", "EnableMinerSpeedInfo", true, "Enables the speed information below the output area in the Miner Window.");
            configShowMinerLiveSpeed = Config.Bind("Miner", "ShowMinerLiveSpeedInfo", false, "True: shows current speed of production building. False: shows regular recipe speed of production building.");
            configMinerSpeedsPerSecond = Config.Bind("Miner", "EnableMinerOutputSpeedInfoPerSecond", false, "Sets the output speeds shown in Miners to items/s (default: items/min).");

            Patchers.UiAssemblerWindowPatch.additionalSpeedLabels = new Util.AdditionalSpeedLabels(ModLogger, configEnableOutputSpeeds.Value, configEnableInputSpeeds.Value, Constants.AssemblerWindowSpeedTextPath);
            Patchers.UiMinerWindowPatch.additionalSpeedLabels = new Util.AdditionalSpeedLabels(ModLogger, configShowMinerSpeed.Value, false, Constants.MinerWindowSpeedTextPath);

            harmony = new Harmony(ModInfo.ModID);
            try
            {
                ModLogger.DebugLog("Patching AssemblerUI");
                harmony.PatchAll(typeof(Patchers.UiAssemblerWindowPatch));

                ModLogger.DebugLog("Patching MinerUI");
                harmony.PatchAll(typeof(Patchers.UiMinerWindowPatch));
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

            Patchers.UiAssemblerWindowPatch.additionalSpeedLabels.Destroy();
            Patchers.UiMinerWindowPatch.additionalSpeedLabels.Destroy();
        }
        
        #endregion
    }
}
