using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Enums;

namespace KamiToolKit.Nodes;

/// <summary>
///     A <see cref="LayoutListNode" /> that represents a horizontally growing list of nodes.
/// </summary>
public class HorizontalListNode : LayoutListNode
{
    /// <summary>
    ///     Gets or sets the alignment used when calculating layout.
    /// </summary>
    /// <remarks>
    ///     Setting triggers layout recalculation.
    /// </remarks>
    public HorizontalListAnchor Alignment
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    }

    /// <summary>
    ///     Adjusts contained nodes heights to match this nodes height
    /// </summary>
    public bool FitHeight
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    }

    /// <summary>
    ///     Resizes the horizontal list node to fit the height of all contents
    /// </summary>
    public bool FitToContentHeight
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    }

    /// <summary>
    ///     Resizes the horizontal list node to fit the width of all contents
    /// </summary>
    public bool FitToContentWidth
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    }

    /// <summary>
    ///     Gets the amount of space remaining in this node.
    /// </summary>
    public float AreaRemaining
    {
        get
        {
            var visibleNodes = NodeList.Where(node => node.NodeFlags.HasFlag(NodeFlags.Visible)).ToList();
            var contentWidth = visibleNodes.Sum(node => node.Width * node.ScaleX);
            var spacingWidth = Math.Max(visibleNodes.Count - 1, 0) * ItemSpacing;
            var anchorSpacing = Alignment is HorizontalListAnchor.Center ?
                                    0.0f :
                                    FirstItemSpacing;

            return Width - anchorSpacing - contentWidth - spacingWidth;
        }
    }

    /// <summary>
    ///     Gets or sets the up nav index.
    /// </summary>
    public int NavUp { get; set; }

    /// <summary>
    ///     Gets or sets the down nav index.
    /// </summary>
    public int NavDown { get; set; }

    /// <inheritdoc />
    protected override void OnRecalculateLayout()
    {
        var visibleNodes = NodeList.Where(node => node.NodeFlags.HasFlag(NodeFlags.Visible)).ToList();
        var contentWidth = visibleNodes.Sum(node => node.Width * node.ScaleX) + (Math.Max(visibleNodes.Count - 1, 0) * ItemSpacing);

        if (FitToContentWidth)
        {
            var fittedWidth = contentWidth +
                              (Alignment is HorizontalListAnchor.Center ?
                                   0.0f :
                                   FirstItemSpacing);

            if (Width != fittedWidth)
                base.Width = fittedWidth;
        }

        var startX = Alignment switch
        {
            HorizontalListAnchor.Left   => FirstItemSpacing,
            HorizontalListAnchor.Right  => -FirstItemSpacing,
            HorizontalListAnchor.Center => -contentWidth / 2.0f,
            _                           => 0.0f
        };

        foreach (var node in visibleNodes)
        {
            var nodeWidth = node.Width * node.ScaleX;

            if (Alignment is HorizontalListAnchor.Right)
                startX -= nodeWidth;

            node.X = startX;
            AdjustNode(node);

            if (Alignment is HorizontalListAnchor.Left or HorizontalListAnchor.Center)
                startX += nodeWidth + ItemSpacing;
            else if (Alignment is HorizontalListAnchor.Right)
                startX -= ItemSpacing;

            if (FitHeight)
                node.Height = Height;
        }

        if (FitToContentHeight)
        {
            var contentHeight = visibleNodes.Select(node => node.Height).DefaultIfEmpty().Max();

            if (Height != contentHeight)
                Height = contentHeight;
        }
    }

    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        RecalculateLayout();
    }

    /// <inheritdoc />
    protected override void OnRecalculateNavigation()
    {
        var componentNodes = NodeList.OfType<ComponentNode>().Where(node => node.NodeFlags.HasFlag(NodeFlags.Visible)).ToList();
        if (componentNodes.Count is 0) return;

        if (Alignment is HorizontalListAnchor.Right)
            componentNodes = componentNodes.AsEnumerable().Reverse().ToList();

        foreach (var (index, node) in componentNodes.Index())
        {
            node.NavIndex = index + NavIndex;
            node.NavUp    = NavUp;
            node.NavDown  = NavDown;

            // First Element
            if (index is 0)
                node.NavLeft = componentNodes.Count - 1 + NavIndex;
            else
                node.NavLeft = index - 1 + NavIndex;

            // Last Element
            if (index == componentNodes.Count - 1)
                node.NavRight = NavIndex;
            else
                node.NavRight = index + 1 + NavIndex;
        }
    }
}
