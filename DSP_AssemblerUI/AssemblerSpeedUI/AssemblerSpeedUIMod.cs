using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Diagnostics;

namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    [BepInPlugin(ModInfo.ModID, ModInfo.ModName, ModInfo.VersionString)]
    public class AssemblerSpeedUIMod : BaseUnityPlugin
    {
        #region Main Plugin
        internal Harmony harmony = new(ModInfo.ModID);

		internal static readonly new ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(ModInfo.ModName);

		public static ConfigEntry<bool> configEnableOutputSpeeds = null!;
		public static ConfigEntry<bool> configEnableInputSpeeds = null!;
		public static ConfigEntry<bool> configInputSpeedsPerSecond = null!;
		public static ConfigEntry<bool> configOutputSpeedsPerSecond = null!;
		public static ConfigEntry<bool> configShowLiveSpeed = null!;
		public static ConfigEntry<uint> configShownDecimalPlaces = null!;

		public static ConfigEntry<bool> configShowMinerSpeed = null!;
		public static ConfigEntry<bool> configShowMinerLiveSpeed = null!;
		public static ConfigEntry<bool> configMinerSpeedsPerSecond = null!;

        [Conditional("DEBUG")]
        internal static void LogDebug(string message)
            => Logger.LogInfo(message);

        internal void Awake()
        {
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

            Patchers.UIAssemblerWindowPatch.additionalSpeedLabels = new Util.AdditionalSpeedLabels(configEnableOutputSpeeds.Value, configEnableInputSpeeds.Value, Constants.AssemblerWindowSpeedTextPath);
            Patchers.UIMinerWindowPatch.additionalSpeedLabels = new Util.AdditionalSpeedLabels(configShowMinerSpeed.Value, false, Constants.MinerWindowSpeedTextPath);

            try
            {
				LogDebug("Patching AssemblerUI");
                harmony.PatchAll(typeof(Patchers.UIAssemblerWindowPatch));

				LogDebug("Patching MinerUI");
                harmony.PatchAll(typeof(Patchers.UIMinerWindowPatch));
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.Message);
                Logger.LogError(ex.StackTrace);
            }
        }

        internal void OnDestroy()
        {
            harmony?.UnpatchSelf();

            Patchers.UIAssemblerWindowPatch.additionalSpeedLabels.Destroy();
            Patchers.UIMinerWindowPatch.additionalSpeedLabels.Destroy();
        }
        
        #endregion
    }
}
