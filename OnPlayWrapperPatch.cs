using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;

namespace TheBallFix;

/// <summary>
/// 补丁: 在 <see cref="CardModel.OnPlayWrapper"/> 完成后, 将 <see cref="TheBall"/> 的 _owner 改回接收方
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
public static class OnPlayWrapperPatch
{
    /// <summary>
    /// <see cref="CardModel.OnPlayWrapper"/> 完成后执行的后置方法
    /// </summary>
    /// <param name="__instance">当前牌实例</param>
    /// <param name="__result">原始 <see cref="CardModel.OnPlayWrapper"/> 返回的 <see cref="Task"/></param>
    [HarmonyPostfix]
    public static void Postfix(CardModel __instance, ref Task __result)
    {
        // 如果当前牌不是 THE_BALL, 则不做任何处理
        if (__instance is not TheBall) { return; }

        // 替换返回的 Task: 等待原始 OnPlayWrapper 全部完成后, 再恢复 _owner 给接收方
        __result = RestoreOwnerAfterWrapperAsync(__result, __instance);
    }

    /// <summary>
    /// 等待 <see cref="CardModel.OnPlayWrapper"/> 的全部异步工作完成后,
    /// 将 <see cref="TheBall"/> 的 _owner 恢复给接收方
    /// </summary>
    /// <param name="originalTask"><see cref="CardModel.OnPlayWrapper"/> 返回的原始 <see cref="Task"/></param>
    /// <param name="card">当前牌实例</param>
    /// <returns>一个新的 <see cref="Task"/>, 在原始 <see cref="Task"/> 完成后执行恢复操作</returns>
    private static async Task RestoreOwnerAfterWrapperAsync(Task originalTask, CardModel card)
    {
        // 等待 OnPlayWrapper 的全部异步工作(包括 OnPlay, Enchantment, AfterCardPlayed 等)完成
        await originalTask;

        // 从 PendingTransfers 中获取接收方并恢复 _owner
        if (TheBallOnPlayPatch.PendingTransfers.Remove(card, out var targetPlayer))
        {
            TheBallFixMod.OwnerField.SetValue(card, targetPlayer);
            TheBallFixMod.Log($"将 {card} 的 _owner 恢复给接收方: {targetPlayer.NetId}");
        }
    }
}
