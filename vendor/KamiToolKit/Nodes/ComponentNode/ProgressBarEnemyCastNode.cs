using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.Timelines;

namespace KamiToolKit.Nodes;

/// <summary>
///     Specialization of <see cref="ProgressNode" /> to represent the castbar that enemies use.
///     Modeled off ui/uld/EnemyList.uld and ui/uld/TargetInfoCastBar.uld.
/// </summary>
public class ProgressBarEnemyCastNode : ProgressNode
{
    private const int INTERRUPT_FRAME_LENGTH_LENGTH = 30;
    private const int INTERRUPT_FRAME_OFFSET        = 15;
    private const int INTERRUPT_MAX_COUNT           = 2;

    private readonly List<SimpleImageNode> interruptNodes = [];

    /// <summary>
    ///     Constructs a new <see cref="ProgressBarEnemyCastNode" />.
    /// </summary>
    public ProgressBarEnemyCastNode()
    {
        BackgroundNode = new SimpleImageNode
        {
            TexturePath        = "ui/uld/EnemyList.tex",
            TextureCoordinates = new Vector2(0.0f,   120.0f),
            TextureSize        = new Vector2(120.0f, 16.0f)
        };
        BackgroundNode.AttachNode(this);

        ProgressBarNode = new SimpleImageNode
        {
            TexturePath        = "ui/uld/EnemyList.tex",
            TextureCoordinates = new Vector2(0.0f,   104.0f),
            TextureSize        = new Vector2(120.0f, 16.0f),
            OriginX            = 4.0f
        };
        ProgressBarNode.AttachNode(this);

        for (var index = 0; index < INTERRUPT_MAX_COUNT; index++)
        {
            var startFrame = 1          + (index                         * INTERRUPT_FRAME_OFFSET);
            var peakFrame  = startFrame + (INTERRUPT_FRAME_LENGTH_LENGTH / 2);
            var endFrame   = startFrame + INTERRUPT_FRAME_LENGTH_LENGTH;

            var interruptNode = new SimpleImageNode
            {
                TexturePath        = "ui/uld/Interrupt.tex",
                TextureCoordinates = new Vector2(0.0f,   160.0f),
                TextureSize        = new Vector2(232.0f, 32.0f),
                WrapMode           = WrapMode.Stretch,
                AddColor           = new Vector3(1.0f, 0.0f, -80.0f / 255.0f),
                Alpha              = 0.0f
            };
            interruptNode.AttachNode(this);

            interruptNode.AddTimeline
            (
                new TimelineBuilder()
                    .BeginFrameSet(startFrame, endFrame)
                    .AddFrame
                    (
                        startFrame,
                        alpha: 0,
                        scale: new Vector2(1.0f, 1.0f)
                    )
                    .AddFrame
                    (
                        peakFrame,
                        alpha: (byte)(255 - (index * 75))
                    )
                    .AddFrame
                    (
                        endFrame,
                        alpha: 0,
                        scale: new Vector2(1.2f, 2f)
                    )
                    .EndFrameSet()
                    .Build()
            );

            interruptNodes.Add(interruptNode);
        }

        LoadTimelines();

        Progress = 0.0f;
    }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public SimpleImageNode BackgroundNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public SimpleImageNode ProgressBarNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public IReadOnlyList<SimpleImageNode> InterruptNodes => interruptNodes;

    /// <summary>
    ///     Gets or sets whether the cast can be interrupted, playing the interrupt animation.
    /// </summary>
    public bool IsInterruptible
    {
        get;
        set
        {
            field = value;

            if (value)
                Timeline?.PlayAnimation(AtkTimelineJumpBehavior.LoopForever, 1);
            else
                Timeline?.StopAnimation();
        }
    }

    /// <inheritdoc />
    public override Vector4 BackgroundColor
    {
        get => BackgroundNode.Color;
        set => BackgroundNode.Color = value;
    }

    /// <inheritdoc />
    public override Vector4 BarColor
    {
        get => ProgressBarNode.Color;
        set => ProgressBarNode.Color = value;
    }

    /// <inheritdoc />
    public override float Progress
    {
        get;
        set
        {
            field = Math.Clamp(value, 0.0f, 1.0f);

            ProgressBarNode.ScaleX = field;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        BackgroundNode.Position = Vector2.Zero;
        BackgroundNode.Size     = Size;

        ProgressBarNode.Position = Vector2.Zero;
        ProgressBarNode.Size     = Size;

        foreach (var interruptNode in interruptNodes)
        {
            interruptNode.Position = Vector2.Zero;
            interruptNode.Size     = new Vector2(120.0f, 16.0f);
            interruptNode.Origin   = new Vector2(60.0f,  8.0f);
        }
    }

    private void LoadTimelines()
    {
        const int TOTAL_FRAMES = 1 + (INTERRUPT_FRAME_OFFSET * (INTERRUPT_MAX_COUNT - 1)) + INTERRUPT_FRAME_LENGTH_LENGTH;

        AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(1, TOTAL_FRAMES)
                .AddLabel(1,            1, AtkTimelineJumpBehavior.Start,       0)
                .AddLabel(TOTAL_FRAMES, 0, AtkTimelineJumpBehavior.LoopForever, 1)
                .EndFrameSet()
                .Build()
        );
    }
}
