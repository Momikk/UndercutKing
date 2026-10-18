using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Interfaces;
using KamiToolKit.Nodes;
using Lumina.Data.Parsing.Uld;

namespace UndercutKing;

internal sealed record NativePriceRow(string Name, string Mine, string Lowest, string Status, string Source);

internal sealed class NativePriceRowNode : ListItemNode<NativePriceRow>, IListItemNode
{
    public static float ItemHeight => 30;
    private readonly TextNode[] cells;
    public NativePriceRowNode()
    {
        EnableSelection = false;
        cells = Enumerable.Range(0, 4).Select(_ => new TextNode
        {
            FontSize = 13, AlignmentType = AlignmentType.Left, TextFlags = TextFlags.Ellipsis,
        }).ToArray();
        foreach (var cell in cells) cell.AttachNode(this);
    }
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        if (cells == null) return;
        for (var i = 0; i < cells.Length; i++)
        {
            var (x, width) = Column(i, Width);
            cells[i].Position = new(x, 0);
            cells[i].Size = new(Math.Max(1, width - 4), 30);
        }
    }
    internal static (float X, float Width) Column(int i, float width) => i switch
    {
        0 => (0, width * .42f), 1 => (width * .42f, width * .2f),
        2 => (width * .62f, width * .2f), _ => (width * .82f, width * .18f),
    };
    protected override void SetNodeData(NativePriceRow row)
    {
        cells[0].String = row.Name; cells[1].String = row.Mine;
        cells[2].String = row.Lowest; cells[3].String = row.Status;
        TextTooltip = $"{row.Name}\n我的单价：{row.Mine} / 市场最低价：{row.Lowest}\n{row.Status} · {row.Source}";
    }
}
