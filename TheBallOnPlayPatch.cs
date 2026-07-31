using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace TheBallFix;

/// <summary>
/// 补丁: 拦截 <see cref="TheBall"/> 的 <see cref="TheBall.OnPlay"/> 方法, 在原始方法执行后将 _owner 改回原始出牌人
/// </summary>
[HarmonyPatch(typeof(TheBall), "OnPlay")]
public static class TheBallOnPlayPatch
{
    /// <summary>
    /// 保存 <see cref="TheBall"/> 的接收方, 供 <see cref="OnPlayWrapperPatch"/> 恢复使用
    /// </summary>
    internal static readonly Dictionary<CardModel, Player> PendingTransfers = [];

    /// <summary>
    /// 保存 <see cref="TheBall"/> 的原始出牌人, 供 <see cref="Postfix"/> 恢复使用
    /// </summary>
    private static readonly Dictionary<CardModel, Player> OriginalOwners = [];

    /// <summary>
    /// 保存 <see cref="TheBall"/> 的视觉节点引用, 用于在捕获 <see cref="NullReferenceException"/> 时清理残留视觉
    /// </summary>
    private static readonly Dictionary<CardModel, NCard> VisualNodes = [];

    /// <summary>
    /// 在原始方法执行前保存原始出牌人和卡牌视觉节点
    /// </summary>
    /// <param name="__instance">当前牌实例</param>
    /// <returns>总是返回 <see langword="true"/> 以继续执行原始方法</returns>
    [HarmonyPrefix]
    public static bool Prefix(TheBall __instance)
    {
        // 保存原始出牌人
        OriginalOwners[__instance] = __instance.Owner;

        // 保存视觉节点引用
        var cardNode = NCard.FindOnTable(__instance);
        if (cardNode is not null)
        {
            VisualNodes[__instance] = cardNode;
        }

        // 继续执行原始方法
        return true;
    }

    /// <summary>
    /// 在原始方法执行后将 _owner 改回原始出牌人, 并保存接收方, 供 <see cref="OnPlayWrapperPatch"/> 恢复使用
    /// </summary>
    /// <param name="__instance">当前牌实例</param>
    /// <param name="cardPlay">当前牌的出牌信息</param>
    /// <param name="__result">原始方法的返回值</param>
    [HarmonyPostfix]
    public static void Postfix(TheBall __instance, CardPlay cardPlay, ref Task __result)
    {
        // 尝试从 OriginalOwners 中获取原始出牌人
        if (OriginalOwners.Remove(__instance, out var originalOwner))
        {
            // 如果当前牌是最后一张出牌, 并且 CombatState 不为 null
            if (cardPlay.IsLastInSeries && __instance.CombatState is not null)
            {
                // 等待原始 OnPlay 的 Task 完全结束后, 将 _owner 改回原始出牌人
                __result = RestoreOwnerAfterOnPlayAsync(__result, __instance, originalOwner);
            }
            else
            {
                // 不需要包装 Task, 但也需要清理 VisualNodes 中可能存在的条目
                _ = VisualNodes.Remove(__instance);
            }
        }
    }

    /// <summary>
    /// 在原始 <see cref="TheBall.OnPlay"/> 方法执行完成后,
    /// 将 _owner 改回原始出牌人, 并保存接收方, 供 <see cref="OnPlayWrapperPatch"/> 恢复使用
    /// </summary>
    /// <param name="__originalTask">原始 <see cref="TheBall.OnPlay"/> 方法的 <see cref="Task"/></param>
    /// <param name="__instance">当前牌实例</param>
    /// <param name="__originalOwner">原始出牌人</param>
    /// <returns>一个新的 <see cref="Task"/>, 在原始 <see cref="Task"/> 完成后执行恢复操作</returns>
    private static async Task RestoreOwnerAfterOnPlayAsync(Task __originalTask, TheBall __instance, Player __originalOwner)
    {
        try
        {
            await __originalTask;
        }
        catch (NullReferenceException)
        {
            // 原版 GiveToAnotherPlayer 因战斗状态变更抛了 NRE, 牌的 _owner 可能已被设为 null 或未正确转移
            TheBallFixMod.Log($"在 {__instance} 的 OnPlay 中捕获到 NullReferenceException, 将 _owner 恢复给原始出牌人: {__originalOwner.NetId}");

            // 将 _owner 改回原始出牌人, 以防后续 hook 看到错误的玩家
            TheBallFixMod.OwnerField.SetValue(__instance, __originalOwner);

            // 尝试清理残留视觉节点
            if (VisualNodes.Remove(__instance, out var stuckNode) && GodotObject.IsInstanceValid(stuckNode))
            {
                stuckNode.Visible = false;
                TheBallFixMod.Log($"已将 {__instance} 的残留视觉节点隐藏");
            }

            // 提前返回, 不再执行后续恢复操作
            return;
        }

        // OnPlay 中的 GiveToAnotherPlayer 正常完成, 视觉节点已被正确处理, 不再需要此引用
        _ = VisualNodes.Remove(__instance);

        // OnPlay 中的 GiveToAnotherPlayer 已经把 _owner 改成了接收方
        var recipient = __instance.Owner;

        // 如果接收方和原始出牌人相同, 则不做任何处理
        if (recipient == __originalOwner) { return; }

        // 保存接收方, 供 PatchOnPlayWrapper 在全部 hook 执行完后恢复
        PendingTransfers[__instance] = recipient;

        // 改回原始出牌人, 使后续 hook 看到正确的玩家
        TheBallFixMod.OwnerField.SetValue(__instance, __originalOwner);

        // 记录日志
        TheBallFixMod.Log($"将 {__instance} 的 _owner 暂时改回原始出牌人: {__originalOwner.NetId}, 接收方为: {recipient.NetId}");
    }
}
