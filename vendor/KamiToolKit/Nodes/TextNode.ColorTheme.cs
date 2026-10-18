using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;

namespace KamiToolKit.Nodes;

public unsafe partial class TextNode
{
    private ByteColor previousTextColor;
    private ByteColor previousOutlineColor;
    private ByteColor previousBackgroundColor;

    /// <summary>
    /// 绑定正文颜色使用的 UIColor 行号。直接设置 TextColor 会取消绑定。
    /// </summary>
    public uint? TextColorID
    {
        get;
        set
        {
            field = value;
            if (value is not { } colorID)
                return;

            Node->TextColor   = ColorHelper.GetColor(colorID).ToByteColor();
            previousTextColor = Node->TextColor;
            Node->IsDirty     = true;
        }
    }

    /// <summary>
    /// 绑定描边颜色使用的 UIColor 行号。直接设置 TextOutlineColor 会取消绑定。
    /// </summary>
    public uint? TextOutlineColorID
    {
        get;
        set
        {
            field = value;
            if (value is not { } colorID)
                return;

            Node->EdgeColor      = ColorHelper.GetColor(colorID).ToByteColor();
            previousOutlineColor = Node->EdgeColor;
            Node->IsDirty        = true;
        }
    }

    /// <summary>
    /// 绑定背景颜色使用的 UIColor 行号。直接设置 BackgroundColor 会取消绑定。
    /// </summary>
    public uint? BackgroundColorID
    {
        get;
        set
        {
            field = value;
            if (value is not { } colorID)
                return;

            Node->BackgroundColor   = ColorHelper.GetColor(colorID).ToByteColor();
            previousBackgroundColor = Node->BackgroundColor;
            Node->IsDirty           = true;
        }
    }

    /// <summary>
    /// 是否根据当前主题启用默认主题的文字浮雕。
    /// </summary>
    public bool UseThemeEmboss
    {
        get;
        set
        {
            field = value;
            if (!value)
                return;

            if (AtkStage.Instance()->AtkUIColorHolder->ActiveColorThemeType == 0)
                Node->TextFlags |= TextFlags.Emboss;
            else
                Node->TextFlags &= ~TextFlags.Emboss;

            Node->IsDirty = true;
        }
    }

    /// <inheritdoc />
    protected override void OnColorThemeChanged()
    {
        base.OnColorThemeChanged();

        if (TextColorID is { } textColorID)
            ColorHelper.RefreshColor(ref Node->TextColor, textColorID, ref previousTextColor);

        if (TextOutlineColorID is { } outlineColorID)
            ColorHelper.RefreshColor(ref Node->EdgeColor, outlineColorID, ref previousOutlineColor);

        if (BackgroundColorID is { } backgroundColorID)
            ColorHelper.RefreshColor(ref Node->BackgroundColor, backgroundColorID, ref previousBackgroundColor);

        if (UseThemeEmboss)
        {
            if (AtkStage.Instance()->AtkUIColorHolder->ActiveColorThemeType == 0)
                Node->TextFlags |= TextFlags.Emboss;
            else
                Node->TextFlags &= ~TextFlags.Emboss;
        }

        Node->IsDirty = true;
    }
}
