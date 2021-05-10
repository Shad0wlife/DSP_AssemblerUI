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
    }
}
