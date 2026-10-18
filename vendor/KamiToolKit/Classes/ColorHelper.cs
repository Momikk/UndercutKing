using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Nodes;

namespace KamiToolKit.Classes;

/// <summary>
///     Helper class for getting the current themed color from the AtkUIColorHolder.
/// </summary>
public static unsafe class ColorHelper
{
    /// <summary>
    /// 原生颜色表更新且现有节点刷新完成后触发。订阅者应在所属界面释放时取消订阅。
    /// </summary>
    public static event Action? ColorThemeChanged;

    /// <summary>
    /// 主题刷新修改文字绘制参数后触发，供宿主更新原生字体缓存。
    /// </summary>
    public static event Action<TextNode>? TextStyleChanged;

    /// <summary>
    /// 在游戏主线程上刷新现有节点和界面。调用前必须完成原生颜色表加载。
    /// </summary>
    public static void RefreshColorTheme()
    {
        foreach (var node in NodeBase.CreatedNodes.ToArray())
        {
            try
            {
                if (!node.NotifyColorThemeChanged())
                    continue;

                if (node is TextNode textNode)
                    TextStyleChanged?.Invoke(textNode);
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, $"刷新 {node.GetType().Name} 的颜色主题失败。");
            }
        }

        foreach (var addon in NativeAddon.CreatedAddons.ToArray())
        {
            try
            {
                addon.NotifyColorThemeChanged();
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, $"刷新 {addon.GetType().Name} 的颜色主题失败。");
            }
        }

        if (ColorThemeChanged == null)
            return;

        foreach (var handler in ColorThemeChanged.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception ex)
            {
                IPluginLog.Get().Error(ex, "颜色主题变化回调失败。");
            }
        }
    }

    internal static bool RefreshColor
    (
        ref ByteColor color,
        uint          colorID,
        ref ByteColor previous
    )
    {
        var next = AtkStage.Instance()->AtkUIColorHolder->GetColor(true, colorID);
        var updated = (color.RGBA & RGB_MASK) == (previous.RGBA & RGB_MASK) ?
                          (next   & RGB_MASK) | (color.RGBA     & ALPHA_MASK) :
                          color.RGBA;
        previous.RGBA = next;

        if (color.RGBA == updated)
            return false;

        color.RGBA = updated;
        return true;
    }

    /// <summary>
    ///     Gets a color by UIColorId
    /// </summary>
    /// <param name="colorId">The UIColor Row Id.</param>
    /// <returns>Vector4 RGBA color.</returns>
    public static Vector4 GetColor
    (
        uint colorId
    )
        => ConvertToVector4(AtkStage.Instance()->AtkUIColorHolder->GetColor(true, colorId));

    private static Vector4 ConvertToVector4
    (
        uint color
    )
    {
        var a = (byte)(color >> 24);
        var b = (byte)(color >> 16);
        var g = (byte)(color >> 8);
        var r = (byte)color;

        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
    }

    #region 常量

    private const uint RGB_MASK   = 0x00FFFFFF;
    private const uint ALPHA_MASK = 0xFF000000;

    #endregion
}
