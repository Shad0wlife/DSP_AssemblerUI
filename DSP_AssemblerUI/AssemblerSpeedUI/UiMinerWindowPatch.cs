using System.Security;
using System.Security.Permissions;
using HarmonyLib;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    public class UiMinerWindowPatch
    {
        internal static AdditionalSpeedLabels additionalSpeedLabels;

        private static void SetupLabels(UIMinerWindow window)
        {
            AssemblerSpeedUIMod.ModLogger.DebugLog("Setup miner label");
            additionalSpeedLabels.SetupLabels(1, null);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIMinerWindow), "OnMinerIdChange")]
        public static void OnMinerIdChangePostfix(UIMinerWindow __instance)
        {
            SetupLabels(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIMinerWindow), "_OnOpen")]
        public static void _OnOpenPostfix(UIMinerWindow __instance)
        {
            SetupLabels(__instance);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIMinerWindow), "_OnUpdate")]
        public static void _OnUpdatePostfix(UIMinerWindow __instance)
        {
            if (__instance == null || __instance.minerId == 0 || __instance.factory == null || __instance.factorySystem == null)
            {
                var method = AccessTools.Method(typeof(UIMinerWindow), "_Close");
                method.Invoke(__instance, new object[0]);
                return;
            }

            var minerComponent = __instance.factorySystem.minerPool[__instance.minerId];
            if (minerComponent.id != __instance.minerId)
            {
                var method = AccessTools.Method(typeof(UIMinerWindow), "_Close");
                method.Invoke(__instance, new object[0]);
                return;
            }

            var speed = CalcSpeed(__instance, minerComponent);
            additionalSpeedLabels.UpdateSpeedLabel($"assembler-speed-out-item0", speed, false);
        }

        private static float CalcSpeed(UIMinerWindow __instance, MinerComponent minerComponent)
        {
            var veinPool = __instance.factory.veinPool;
            var veinIndex = minerComponent.veinCount != 0 ? minerComponent.veins[minerComponent.currentVeinIndex] : 0;

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
                speed *= (float) (veinPool[veinIndex].amount * (double) VeinData.oilSpeedMultiplier);
            }

            return speed;
        }
    }
}
