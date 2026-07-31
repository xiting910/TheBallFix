using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using System.Reflection;

namespace TheBallFix;

/// <summary>
/// Mod 入口类
/// </summary>
[ModInitializer(nameof(Initialize))]
public class TheBallFixMod
{
    /// <summary>
    /// 日志记录器
    /// </summary>
    private static readonly Logger _logger = new(nameof(TheBallFix), LogType.Generic);

    /// <summary>
    /// 要修补的字段
    /// </summary>
    internal static readonly FieldInfo OwnerField = typeof(CardModel).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(nameof(CardModel), "_owner");

    /// <summary>
    /// 初始化方法
    /// </summary>
    public static void Initialize()
    {
        var harmony = new Harmony(nameof(TheBallFix));
        harmony.PatchAll();
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    /// <param name="message">日志消息</param>
    public static void Log(string message)
    {
        _logger.Info(nameof(TheBallFixMod) + ": " + message);
    }
}
