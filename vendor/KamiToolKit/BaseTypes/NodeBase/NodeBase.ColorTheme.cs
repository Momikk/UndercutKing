using FFXIVClientStructs.FFXIV.Client.Graphics;
using KamiToolKit.Classes;

namespace KamiToolKit.BaseTypes;

public abstract unsafe partial class NodeBase
{
    private ByteColor previousColor;

    /// <summary>
    /// 绑定节点颜色使用的 UIColor 行号。直接设置 Color 或 ColorHsva 会取消绑定。
    /// </summary>
    public uint? ColorID
    {
        get;
        set
        {
            field = value;
            if (value is not { } colorID)
                return;

            ResNode->Color   = ColorHelper.GetColor(colorID).ToByteColor();
            previousColor    = ResNode->Color;
            ResNode->IsDirty = true;
        }
    }

    /// <summary>
    /// 节点完成主题刷新后触发。
    /// </summary>
    public event Action? ColorThemeChanged;

    internal bool NotifyColorThemeChanged()
    {
        if (IsDisposed || ResNode == null)
            return false;

        OnColorThemeChanged();
        ColorThemeChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 更新节点的主题颜色。自定义节点可以重写此方法刷新自身的主题资源。
    /// </summary>
    protected virtual void OnColorThemeChanged()
    {
        if (ColorID is { } colorID && ColorHelper.RefreshColor(ref ResNode->Color, colorID, ref previousColor))
            ResNode->IsDirty = true;
    }
}
