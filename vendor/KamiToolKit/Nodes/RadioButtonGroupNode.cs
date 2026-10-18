using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Enums;
using KamiToolKit.Interfaces;
using KamiToolKit.Timelines;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.Nodes;

/// <summary>
///     Node representing a set of radio buttons.
/// </summary>
public class RadioButtonGroupNode : ResNode, IControllerNavigable
{
    private readonly List<RadioButtonNode> radioButtons = [];
    private          RadioButtonNode?      selectedButton;
    private          bool                  isRecalculatingLayout;

    /// <summary>
    ///     Constructs a new <see cref="RadioButtonGroupNode" />
    /// </summary>
    public RadioButtonGroupNode()
    {
        RadioButtons = radioButtons.AsReadOnly();
        BuildTimelines();
    }

    /// <summary>
    ///     Gets or sets the direction in which buttons are arranged. Defaults to vertical.
    /// </summary>
    public LayoutOrientation LayoutOrientation
    {
        get;
        set
        {
            if (value is not (LayoutOrientation.Vertical or LayoutOrientation.Horizontal))
                throw new ArgumentOutOfRangeException(nameof(value));

            field = value;
            RecalculateLayout();
        }
    } = LayoutOrientation.Vertical;

    /// <summary>
    ///     Gets or sets the corner from which buttons are arranged.
    /// </summary>
    public LayoutAnchor LayoutAnchor
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            field = value;
            RecalculateLayout();
        }
    } = LayoutAnchor.TopLeft;

    /// <summary>
    ///     Gets or sets the padding applied to both sides of each axis.
    /// </summary>
    public Vector2 Padding
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    }

    /// <summary>
    ///     Gets or sets whether the group width follows the visible contents.
    /// </summary>
    public bool FitToContentWidth
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    } = true;

    /// <summary>
    ///     Gets or sets whether the group height follows the visible contents.
    /// </summary>
    public bool FitToContentHeight
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    } = true;

    /// <summary>
    ///     Gets the size required by the visible buttons, including padding and spacing.
    /// </summary>
    public Vector2 ContentSize { get; private set; }

    /// <summary>
    ///     Gets or sets whether the first added button is selected automatically.
    /// </summary>
    public bool SelectFirstButtonByDefault { get; set; } = true;

    /// <summary>
    ///     Invoked after the selected button changes, including programmatic changes.
    ///     Receives null when the selection is cleared.
    /// </summary>
    public Action<RadioButtonNode?>? OnSelectionChanged { get; set; }

    /// <summary>
    ///     Gets or sets the selected button. Set to null to clear the selection.
    ///     Setting this property does not invoke the button callback.
    /// </summary>
    public RadioButtonNode? SelectedButton
    {
        get => selectedButton;
        set => SelectButton(value);
    }

    /// <summary>
    ///     Gets or sets the selected button index. Use -1 to clear the selection.
    /// </summary>
    public int SelectedIndex
    {
        get => selectedButton is null ?
                   -1 :
                   radioButtons.IndexOf(selectedButton);
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, radioButtons.Count);

            SelectButton
            (
                value is -1 ?
                    null :
                    radioButtons[value]
            );
        }
    }

    /// <summary>
    ///     Gets or sets the selection via label. The first matching button is selected.
    ///     Set to null to clear the selection; an unknown label throws an argument exception.
    /// </summary>
    public ReadOnlySeString? SelectedOption
    {
        get => selectedButton?.String;
        set
        {
            if (value is null)
            {
                SelectButton(null);
                return;
            }

            var button = radioButtons.Find(button => button.String == value);
            if (button is null)
                throw new ArgumentException("The label does not belong to this group.", nameof(value));

            SelectButton(button);
        }
    }

    /// <summary>
    ///     Gets or sets the spacing between radio buttons.
    /// </summary>
    public float ItemSpacing
    {
        get;
        set
        {
            field = value;
            RecalculateLayout();
        }
    } = 2.0f;

    /// <summary>
    ///     Gets the radio buttons contained in this group.
    /// </summary>
    public IReadOnlyList<RadioButtonNode> RadioButtons { get; }

    /// <summary>
    ///     Gets or sets the first controller navigation index. Zero disables automatic navigation.
    /// </summary>
    public int NavIndex
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    }

    /// <inheritdoc />
    public int NavLeft
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    }

    /// <inheritdoc />
    public int NavRight
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    }

    /// <inheritdoc />
    public int NavUp
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    }

    /// <inheritdoc />
    public int NavDown
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    }

    /// <summary>
    ///     Gets or sets whether controller navigation wraps within the group.
    /// </summary>
    public bool WrapNavigation
    {
        get;
        set
        {
            field = value;
            RecalculateNavigation();
        }
    } = true;

    /// <summary>
    ///     Creates a button sized to its label and returns it for further customization.
    ///     The optional callback runs when the button is clicked.
    /// </summary>
    public RadioButtonNode AddButton
    (
        ReadOnlySeString label,
        Action?          callback = null
    )
    {
        var newRadioButton = new RadioButtonNode
        {
            Height   = 16.0f,
            String   = label,
            Callback = callback
        };

        var labelSize = newRadioButton.LabelNode.GetTextDrawSize(considerScale: false);
        newRadioButton.LabelNode.Size = new Vector2(MathF.Ceiling(labelSize.X), newRadioButton.Height);
        newRadioButton.Width          = newRadioButton.LabelNode.X + newRadioButton.LabelNode.Width;

        AddButton(newRadioButton);
        return newRadioButton;
    }

    /// <summary>
    ///     Adds a custom button. The group owns the button and disposes it when removed.
    ///     Change selection through the group rather than the button flags.
    /// </summary>
    public void AddButton
    (
        RadioButtonNode button
    )
    {
        ArgumentNullException.ThrowIfNull((object)button, nameof(button));
        if (radioButtons.Contains(button))
            throw new ArgumentException("The button already belongs to this group.", nameof(button));

        button.IsChecked  = false;
        button.IsSelected = false;
        button.AddEvent(AtkEventType.ButtonClick, () => SelectButton(button));
        button.OnSizeUpdated += RecalculateLayout;

        radioButtons.Add(button);
        button.AttachNode(this);

        RecalculateLayout();

        if (radioButtons.Count is 1 && SelectFirstButtonByDefault)
            SelectButton(button);
    }

    /// <summary>
    ///     Selects a button or clears the selection. Optionally invokes the selected button callback.
    ///     Selection change notifications run before the button callback.
    /// </summary>
    public void SelectButton
    (
        RadioButtonNode? button,
        bool             invokeCallback = false
    )
    {
        if (button is not null && !radioButtons.Contains(button))
            throw new ArgumentException("The button does not belong to this group.", nameof(button));

        foreach (var radioButton in radioButtons)
        {
            var isSelected = radioButton == button;
            radioButton.IsChecked  = isSelected;
            radioButton.IsSelected = isSelected;
        }

        var previousButton = selectedButton;
        selectedButton = button;

        if (previousButton != button)
            OnSelectionChanged?.Invoke(button);

        if (invokeCallback)
            button?.Callback?.Invoke();
    }

    /// <summary>
    ///     Removes the button via the specified label.
    /// </summary>
    public bool RemoveButton
    (
        ReadOnlySeString label
    )
    {
        var button = radioButtons.Find(button => button.String == label);
        return button is not null && RemoveButton(button);
    }

    /// <summary>
    ///     Removes and disposes a button. Removing the selected button clears the selection.
    /// </summary>
    public bool RemoveButton
    (
        RadioButtonNode button
    )
    {
        if (!radioButtons.Remove(button)) return false;

        var wasSelected = selectedButton == button;
        if (wasSelected)
            selectedButton = null;

        button.OnSizeUpdated -= RecalculateLayout;
        button.Dispose();
        RecalculateLayout();

        if (wasSelected)
            OnSelectionChanged?.Invoke(null);

        return true;
    }

    /// <summary>
    ///     Removes all radio buttons from this node.
    /// </summary>
    public void Clear()
    {
        var hadSelection = selectedButton is not null;
        selectedButton = null;

        foreach (var node in radioButtons)
        {
            node.OnSizeUpdated -= RecalculateLayout;
            node.Dispose();
        }

        radioButtons.Clear();
        RecalculateLayout();

        if (hadSelection)
            OnSelectionChanged?.Invoke(null);
    }

    /// <summary>
    ///     Recalculates visible button positions, content size and controller navigation.
    ///     Invoke after changing button scale or visibility.
    /// </summary>
    public void RecalculateLayout()
    {
        if (IsDisposed || isRecalculatingLayout) return;

        isRecalculatingLayout = true;

        try
        {
            var horizontal   = LayoutOrientation is LayoutOrientation.Horizontal;
            var contentSize  = Vector2.Zero;
            var visibleCount = 0;

            foreach (var button in radioButtons)
            {
                if (!button.IsVisible) continue;

                var buttonSize = button.Size * button.Scale;

                if (horizontal)
                {
                    contentSize.X += buttonSize.X;
                    contentSize.Y =  MathF.Max(contentSize.Y, buttonSize.Y);
                }
                else
                {
                    contentSize.X =  MathF.Max(contentSize.X, buttonSize.X);
                    contentSize.Y += buttonSize.Y;
                }

                visibleCount++;
            }

            if (visibleCount > 1)
            {
                if (horizontal)
                    contentSize.X += (visibleCount - 1) * ItemSpacing;
                else
                    contentSize.Y += (visibleCount - 1) * ItemSpacing;
            }

            ContentSize = contentSize + (Padding * 2.0f);
            var fittedSize = new Vector2
            (
                FitToContentWidth ?
                    MathF.Ceiling(ContentSize.X) :
                    Width,
                FitToContentHeight ?
                    MathF.Ceiling(ContentSize.Y) :
                    Height
            );

            if (Size != fittedSize)
                Size = fittedSize;

            var fromRight  = LayoutAnchor is LayoutAnchor.TopRight or LayoutAnchor.BottomRight;
            var fromBottom = LayoutAnchor is LayoutAnchor.BottomLeft or LayoutAnchor.BottomRight;
            var position = new Vector2
            (
                fromRight ?
                    Width - Padding.X :
                    Padding.X,
                fromBottom ?
                    Height - Padding.Y :
                    Padding.Y
            );

            foreach (var button in radioButtons)
            {
                if (!button.IsVisible) continue;

                var buttonSize = button.Size * button.Scale;
                button.Position = position -
                                  new Vector2
                                  (
                                      fromRight ?
                                          buttonSize.X :
                                          0.0f,
                                      fromBottom ?
                                          buttonSize.Y :
                                          0.0f
                                  );

                if (horizontal)
                    position.X += (fromRight ?
                                       -1.0f :
                                       1.0f) *
                                  (buttonSize.X + ItemSpacing);
                else
                    position.Y += (fromBottom ?
                                       -1.0f :
                                       1.0f) *
                                  (buttonSize.Y + ItemSpacing);
            }

            RecalculateNavigation();
        }
        finally
        {
            isRecalculatingLayout = false;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();
        RecalculateLayout();
    }

    /// <inheritdoc />
    protected override void Dispose
    (
        bool disposing,
        bool isNativeDestructor
    )
    {
        if (disposing)
        {
            foreach (var button in radioButtons)
            {
                button.OnSizeUpdated -= RecalculateLayout;
            }

            radioButtons.Clear();
            selectedButton     = null;
            OnSelectionChanged = null;
        }

        base.Dispose(disposing, isNativeDestructor);
    }

    private void RecalculateNavigation()
    {
        if (IsDisposed || NavIndex is 0) return;

        var visibleCount = 0;

        foreach (var button in radioButtons)
        {
            if (button.IsVisible)
                visibleCount++;
            else
                button.NavIndex = 0;
        }

        var horizontal = LayoutOrientation is LayoutOrientation.Horizontal;
        var reversed = horizontal ?
                           LayoutAnchor is LayoutAnchor.TopRight or LayoutAnchor.BottomRight :
                           LayoutAnchor is LayoutAnchor.BottomLeft or LayoutAnchor.BottomRight;
        var visibleIndex = 0;

        foreach (var button in radioButtons)
        {
            if (!button.IsVisible) continue;

            var positionIndex = reversed ?
                                    visibleCount - 1 - visibleIndex :
                                    visibleIndex;
            var previousIndex = NavIndex + positionIndex - 1;
            var nextIndex     = NavIndex                 + positionIndex + 1;

            if (positionIndex is 0)
            {
                previousIndex = horizontal ?
                                    NavLeft :
                                    NavUp;
                if (WrapNavigation)
                    previousIndex = NavIndex + visibleCount - 1;
            }

            if (positionIndex == visibleCount - 1)
            {
                nextIndex = horizontal ?
                                NavRight :
                                NavDown;
                if (WrapNavigation)
                    nextIndex = NavIndex;
            }

            button.NavIndex = NavIndex + positionIndex;
            button.NavLeft = horizontal ?
                                 previousIndex :
                                 NavLeft;
            button.NavRight = horizontal ?
                                  nextIndex :
                                  NavRight;
            button.NavUp = horizontal ?
                               NavUp :
                               previousIndex;
            button.NavDown = horizontal ?
                                 NavDown :
                                 nextIndex;
            visibleIndex++;
        }
    }

    private void BuildTimelines() =>
        AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(1, 19)
                .AddLabel(1,  101, AtkTimelineJumpBehavior.PlayOnce, 0)
                .AddLabel(10, 102, AtkTimelineJumpBehavior.PlayOnce, 0)
                .EndFrameSet()
                .Build()
        );
}
