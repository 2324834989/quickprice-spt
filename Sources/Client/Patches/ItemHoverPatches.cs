using System.Reflection;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine.EventSystems;

namespace QuickPrice.Patches
{
    /// <summary>
    /// 捕获鼠标进入物品事件
    /// </summary>
    public class GridItemOnPointerEnterPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.FirstMethod(
                typeof(GridItemView),
                x => x.Name == nameof(GridItemView.OnPointerEnter)
            );
        }

        [PatchPrefix]
        public static void Prefix(GridItemView __instance, PointerEventData eventData)
        {
            Plugin.HoveredItem = __instance?.Item;
            if (Plugin.HoveredItem != null)
            {
                Plugin.Log.LogDebug($"鼠标进入物品: {Plugin.HoveredItem.LocalizedName()}");
            }
        }
    }

    /// <summary>
    /// 捕获鼠标离开物品事件
    /// </summary>
    public class GridItemOnPointerExitPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.FirstMethod(
                typeof(GridItemView),
                x => x.Name == nameof(GridItemView.OnPointerExit)
            );
        }

        [PatchPrefix]
        public static void Prefix(GridItemView __instance, PointerEventData eventData)
        {
            Plugin.HoveredItem = null;
            Plugin.Log.LogDebug("鼠标离开物品");
        }
    }
}
