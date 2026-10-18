using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes.Simplified;
using Lumina.Data.Parsing.Uld;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.Nodes;

/// <summary>
///     Specialization of a button representing a standard text button.
/// </summary>
public unsafe class TextButtonNode : ButtonBase
{
    /// <summary>
    ///     Constructs a new <see cref="TextButtonNode" />
    /// </summary>
    public TextButtonNode()
    {
        BackgroundNode = new SimpleNineGridNode();
        BackgroundNode.AttachNode(this);

        LabelNode = new TextNode
        {
            AlignmentType = AlignmentType.Center,
            Position      = new Vector2(16.0f, 3.0f),
            TextColorID   = 50
        };
        LabelNode.AttachNode(this);

        ApplyTexture();

        Data->Nodes[0] = LabelNode.NodeId;
        Data->Nodes[1] = BackgroundNode.NodeId;

        InitializeComponentEvents();
    }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public NineGridNode BackgroundNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode LabelNode { get; }

    /// <summary>
    ///     Gets or sets the label displayed for this button.
    /// </summary>
    public ReadOnlySeString String
    {
        get => LabelNode.String;
        set => LabelNode.String = value;
    }

    /// <summary>
    ///     Gets or sets the text id that reads a label from the datasheets instead.
    /// </summary>
    public uint TextId
    {
        get => LabelNode.TextId;
        set => LabelNode.TextId = value;
    }

    /// <summary>
    ///     Gets or sets which datasheet should be used to resolve <see cref="TextId" />
    /// </summary>
    public NodeData.SheetType SheetType
    {
        get => LabelNode.SheetType;
        set => LabelNode.SheetType = value;
    }

    /// <summary>
    ///     Gets or sets which background texture this button draws.
    /// </summary>
    public ButtonTextureType TextureType
    {
        get;
        set
        {
            field = value;
            ApplyTexture();
            UpdateLabelLayout();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        BackgroundNode.Size = Size;

        UpdateLabelLayout();
    }

    private static (Vector2 Position, float PaddingY) GetLabelLayout
    (
        ButtonTextureType textureType
    )
        => textureType switch
        {
            ButtonTextureType.ButtonB => (new Vector2(16.0f, 6.0f), 12.0f),
            _                         => (new Vector2(16.0f, 3.0f), 8.0f)
        };

    private void ApplyTexture()
    {
        var backgroundNode = (SimpleNineGridNode)BackgroundNode;

        var (texturePath, textureSize, leftOffset, rightOffset) = TextureType switch
        {
            ButtonTextureType.ButtonB => ("ui/uld/ButtonB.tex", new Vector2(80.0f,  36.0f), 20.0f, 20.0f),
            _                         => ("ui/uld/ButtonA.tex", new Vector2(100.0f, 28.0f), 16.0f, 16.0f)
        };

        backgroundNode.TexturePath = texturePath;
        backgroundNode.TextureSize = textureSize;
        backgroundNode.LeftOffset  = leftOffset;
        backgroundNode.RightOffset = rightOffset;

        LoadThreePartTimelines(this, BackgroundNode, LabelNode, GetLabelLayout(TextureType).Position);
    }

    private void UpdateLabelLayout()
    {
        var (labelPosition, labelPaddingY) = GetLabelLayout(TextureType);

        LabelNode.Position = labelPosition;
        LabelNode.Size     = new Vector2(Width - 32.0f, Height - labelPaddingY);
    }
}
