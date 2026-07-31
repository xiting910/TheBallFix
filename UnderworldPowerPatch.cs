using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace TheBallFix;

/// <summary>
/// 补丁: 拦截 <see cref="UnderworldPower.AfterDamageGiven"/> 方法, 防止敌方伤害触发灾厄效果
/// </summary>
[HarmonyPatch(typeof(UnderworldPower), nameof(UnderworldPower.AfterDamageGiven))]
public static class UnderworldPowerPatch
{
    /// <summary>
    /// 在原始方法执行前拦截: 若伤害来源与能力拥有者不在同一阵营,
    /// 则跳过原始方法, 防止敌方伤害触发灾厄效果.
    /// </summary>
    /// <param name="__instance">当前实例</param>
    /// <param name="dealer">伤害来源生物</param>
    /// <param name="__result">原始方法的返回值, 跳过时设为 <see cref="Task.CompletedTask"/></param>
    /// <returns><see langword="false"/> 跳过原始方法; <see langword="true"/> 正常执行</returns>
    [HarmonyPrefix]
    public static bool Prefix(UnderworldPower __instance, Creature? dealer, ref Task __result)
    {
        // dealer 不为空, 且 dealer 与能力拥有者不在同一阵营
        if (dealer is not null && dealer.Side != __instance.Owner.Side)
        {
            // 跳过原始方法, 直接返回已完成的 Task, 并记录日志
            TheBallFixMod.Log($"跳过 {nameof(UnderworldPower)}.{nameof(UnderworldPower.AfterDamageGiven)} 方法: 伤害来源 {dealer} 与能力拥有者 {__instance.Owner} 不在同一阵营");
            __result = Task.CompletedTask;
            return false;
        }

        // dealer 为空或同阵营, 正常执行原始方法
        return true;
    }
}
