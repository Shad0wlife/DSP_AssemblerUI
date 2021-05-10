using System.Security;
using System.Security.Permissions;
using HarmonyLib;
using UnityEngine.UI;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace DSP_AssemblerUI.AssemblerSpeedUI
{
    public class UiMinerWindowPatch
    {
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

            if (minerComponent.type == EMinerType.Oil)
            {
                return;
            }

            var speed = CalcSpeed(__instance, minerComponent);

            var speedText = AccessTools.Field(typeof(UIMinerWindow), "speedText").GetValue(__instance) as Text;
            UpdateLabel(speedText, speed);
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

        private static void UpdateLabel(Text label, float value)
        {
            var labelValue = value.ToString("0.0") + "每分钟".Translate();
            labelValue += $" ({value / 60}/s)";
            label.text = labelValue;
        }
    }
}
