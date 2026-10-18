using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.System.Input;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using KamiToolKit.BaseTypes.ComponentNode;
using KamiToolKit.Classes;
using KamiToolKit.Enums;
using KamiToolKit.Internal.Classes;
using KamiToolKit.Nodes.Simplified;
using KamiToolKit.Timelines;
using Lumina.Data.Parsing.Uld;
using Lumina.Text.ReadOnly;

namespace KamiToolKit.Nodes;

/// <summary>
///     Implementation of the games TextInputNode and its associated component.
/// </summary>
public unsafe class TextInputNode : ComponentNode<AtkComponentTextInput, AtkUldComponentDataTextInput>
{
    private readonly AtkComponentInputBase.CallbackDelegate pinnedCallbackFunction;
    private          int                                    focusVersion;

    /// <summary>
    ///     Constructs a new <see cref="TextInputNode" />
    /// </summary>
    public TextInputNode() : this(false)
    {
    }

    /// <summary>
    ///     Constructs a text input with native single-line or multiline settings.
    /// </summary>
    protected TextInputNode
    (
        bool multiLine
    )
    {
        SetInternalComponentType(ComponentType.TextInput);

        BackgroundNode = new SimpleNineGridNode
        {
            NodeId             = 19,
            TexturePath        = "ui/uld/TextInputA.tex",
            TextureCoordinates = new Vector2(24.0f, 0.0f),
            TextureSize        = new Vector2(24.0f, 24.0f),
            Offsets            = new Vector4(10.0f),
            Size               = new Vector2(152.0f, 28.0f)
        };
        BackgroundNode.AttachNode(this);

        FocusBorderNode = new SimpleNineGridNode
        {
            NodeId             = 18,
            TexturePath        = "ui/uld/TextInputA.tex",
            TextureCoordinates = new Vector2(0.0f,  0.0f),
            TextureSize        = new Vector2(24.0f, 24.0f),
            Offsets            = new Vector4(10.0f),
            Size               = new Vector2(152.0f, 28.0f)
        };
        FocusBorderNode.AttachNode(this);

        TextLimitsNode = new TextNode
        {
            NodeId        = 17,
            Position      = new Vector2(-24.0f, 6.0f),
            Size          = new Vector2(170.0f, 19.0f),
            FontType      = FontType.MiedingerMed,
            FontSize      = 14,
            AlignmentType = (AlignmentType)21
        };
        TextLimitsNode.AttachNode(this);

        CurrentTextNode = new TextNode
        {
            NodeId        = 16,
            Position      = new Vector2(10.0f,  6.0f),
            Size          = new Vector2(132.0f, 18.0f),
            AlignmentType = AlignmentType.TopLeft,
            TextFlags     = TextFlags.OverflowHidden,
            TextColorID   = 1
        };
        CurrentTextNode.AttachNode(this);

        SelectionListNode = new TextInputSelectionListNode
        {
            NodeId   = 4,
            Position = new Vector2(0.0f,   22.0f),
            Size     = new Vector2(186.0f, 208.0f)
        };
        SelectionListNode.AttachNode(this);

        CursorNode = new CursorNode
        {
            NodeId   = 2,
            Position = new Vector2(10.0f, 2.0f),
            Size     = new Vector2(4.0f,  24.0f),
            OriginY  = 4.0f
        };
        CursorNode.AttachNode(this);

        PlaceholderTextNode = new TextNode
        {
            Position      = new Vector2(10.0f,  6.0f),
            Size          = new Vector2(132.0f, 18.0f),
            AlignmentType = AlignmentType.TopLeft,
            TextFlags     = TextFlags.OverflowHidden,
            TextColorID   = 3
        };
        PlaceholderTextNode.AttachNode(this);

        Data->Nodes[0]  = CurrentTextNode.NodeId;
        Data->Nodes[1]  = BackgroundNode.NodeId;
        Data->Nodes[2]  = CursorNode.NodeId;
        Data->Nodes[3]  = SelectionListNode.NodeId;
        Data->Nodes[4]  = SelectionListNode.Buttons[8].NodeId;
        Data->Nodes[5]  = SelectionListNode.Buttons[7].NodeId;
        Data->Nodes[6]  = SelectionListNode.Buttons[6].NodeId;
        Data->Nodes[7]  = SelectionListNode.Buttons[5].NodeId;
        Data->Nodes[8]  = SelectionListNode.Buttons[4].NodeId;
        Data->Nodes[9]  = SelectionListNode.Buttons[3].NodeId;
        Data->Nodes[10] = SelectionListNode.Buttons[2].NodeId;
        Data->Nodes[11] = SelectionListNode.Buttons[1].NodeId;
        Data->Nodes[12] = SelectionListNode.Buttons[0].NodeId;
        Data->Nodes[13] = SelectionListNode.LabelNode.NodeId;
        Data->Nodes[14] = SelectionListNode.BackgroundNode.NodeId;
        Data->Nodes[15] = TextLimitsNode.NodeId;

        Data->CandidateColor = new ByteColor { R = 66 };
        Data->IMEColor       = new ByteColor { R = 67 };
        Data->FocusColor     = ColorHelper.GetColor(67).ToByteColor();

        Data->Flags1  = TextInputFlags1.EnableIME        | TextInputFlags1.AllowUpperCase | TextInputFlags1.AllowLowerCase;
        Data->Flags2  = TextInputFlags2.AllowNumberInput | TextInputFlags2.AllowSymbolInput;
        Data->MaxLine = 1;

        if (multiLine)
        {
            Data->Flags2                    |= TextInputFlags2.MultiLine | TextInputFlags2.WordWrap;
            Data->MaxLine                   =  byte.MaxValue;
            Data->MaxByte                   =  ushort.MaxValue;
            CurrentTextNode.LineSpacing     =  14;
            PlaceholderTextNode.LineSpacing =  14;
            TextLimitsNode.AlignmentType    =  AlignmentType.BottomRight;
        }

        AllowEnterToComplete = !multiLine;

        LoadTimelines();

        InitializeComponentEvents();

        PlaceholderTextNode.TextFlags = CurrentTextNode.TextFlags;
        EnableCompletion              = false;
        Component->EnableTabCallback  = true;
        Size                          = new Vector2(152.0f, 28.0f);

        pinnedCallbackFunction = OnCallback;
        Component->Callback =
            (delegate* unmanaged<AtkUnitBase*, InputCallbackType, CStringPointer, CStringPointer, int, InputCallbackResult>)Marshal.GetFunctionPointerForDelegate
                (pinnedCallbackFunction);

        ShowLimitText = false;

    }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public NineGridNode BackgroundNode { get; }

    /// <inheritdoc />
    protected override void OnColorThemeChanged()
    {
        base.OnColorThemeChanged();

        var focusColor = ColorHelper.GetColor(67).ToByteColor();
        Component->InputDataBase.FocusColor     = focusColor;
        Component->ComponentTextData.FocusColor = focusColor;
        Data->FocusColor                        = focusColor;
    }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode CurrentTextNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public CursorNode CursorNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public NineGridNode FocusBorderNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextInputSelectionListNode SelectionListNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode TextLimitsNode { get; }

    /// <summary>
    ///     Not intended for public use, but it's here if you absolutely need it.
    /// </summary>
    public TextNode PlaceholderTextNode { get; }

    /// <summary>
    ///     Gets whether this node is being focused.
    /// </summary>
    public bool IsFocused
        => Component->IsActive && AtkStage.Instance()->AtkInputManager->FocusedNode == Component->CollisionNode;

    /// <summary>
    ///     Gets or sets the maximum number of characters allowed.
    /// </summary>
    public int MaxCharacters
    {
        get => (int)Component->ComponentTextData.MaxChar;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Data->MaxChar = (uint)value;
            Component->SetMaxChar(value);
        }
    }

    /// <summary>
    ///     Gets or sets the maximum number of UTF-8 bytes allowed.
    /// </summary>
    public uint MaxBytes
    {
        get => Component->ComponentTextData.MaxByte;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, (uint)int.MaxValue);
            Data->MaxByte = value;
            Component->SetMaxByte((int)value);
        }
    }

    /// <summary>
    ///     Gets or sets the maximum number of lines allowed.
    /// </summary>
    public uint MaxLines
    {
        get => Component->ComponentTextData.MaxLine;
        set
        {
            Data->MaxLine = Math.Min(value, byte.MaxValue);
            Component->SetMaxLine(Data->MaxLine);
        }
    }

    /// <summary>
    ///     Gets or sets whether the text input's character, line, or byte limit should be shown.
    /// </summary>
    public bool ShowLimitText
    {
        get => TextLimitsNode.IsVisible;
        set
        {
            Data->Nodes[15] = value ?
                                  TextLimitsNode.NodeId :
                                  0;
            Component->ComponentTextData.Nodes[15] = Data->Nodes[15];
            Component->AvailableLinesTextNode = value ?
                                                    TextLimitsNode.Node :
                                                    null;
            TextLimitsNode.IsVisible = value;
        }
    }

    /// <summary>
    ///     Gets or sets the text input flags.
    /// </summary>
    public TextInputFlags Flags
    {
        get => (TextInputFlags)((byte)Component->ComponentTextData.Flags1 | ((byte)Component->ComponentTextData.Flags2 << 8));
        set
        {
            if ((value & TextInputFlags.AutoMaxWidth) != 0)
                value |= TextInputFlags.WordWrap;

            if ((value & TextInputFlags.MultiLine) != 0)
                value &= ~TextInputFlags.EnableHistory;

            Component->ComponentTextData.Flags1 = (TextInputFlags1)((ushort)value & 0xFF);
            Component->ComponentTextData.Flags2 = (TextInputFlags2)((ushort)value >> 8);
            Component->ToggleUpperCase((value    & TextInputFlags.AllowUpperCase)   != 0);
            Component->ToggleLowerCase((value    & TextInputFlags.AllowLowerCase)   != 0);
            Component->ToggleNumberInput((value  & TextInputFlags.AllowNumberInput) != 0);
            Component->ToggleSymbolInput((value  & TextInputFlags.AllowSymbolInput) != 0);
            Component->ToggleIME((value          & TextInputFlags.EnableIme)        != 0);
            Component->ToggleDictionary((value   & TextInputFlags.EnableDictionary) != 0);
            Component->ToggleCapitalize((value   & TextInputFlags.Capitalize)       != 0);
            Component->ToggleEscapeClears((value & TextInputFlags.EscapeClears)     != 0);

            Data->Flags1 = Component->ComponentTextData.Flags1;
            Data->Flags2 = Component->ComponentTextData.Flags2;

            var textFlags = CurrentTextNode.TextFlags & ~(TextFlags.WordWrap | TextFlags.MultiLine);
            if ((value & TextInputFlags.WordWrap) != 0)
                textFlags |= TextFlags.WordWrap;
            if ((value & (TextInputFlags.WordWrap | TextInputFlags.MultiLine)) != 0)
                textFlags |= TextFlags.MultiLine;

            CurrentTextNode.TextFlags     = textFlags;
            PlaceholderTextNode.TextFlags = textFlags;
            OnTextChanged();
        }
    }

    /// <summary>
    ///     Gets or sets if auto-translate/completion should be shown when pressing Tab.
    /// </summary>
    public bool EnableCompletion
    {
        get => Component->EnableCompletion;
        set
        {
            Component->ToggleDictionary(value);
            Data->Flags1 = Component->ComponentTextData.Flags1;
        }
    }

    /// <summary>
    ///     Gets or sets whether sound effects should be enabled when selecting or unselecting the node.
    /// </summary>
    public bool EnableFocusSounds
    {
        get => Component->EnableFocusSounds;
        set => Component->EnableFocusSounds = value;
    }

    /// <summary>
    ///     Gets or sets whether pressing Enter should trigger input completion.
    /// </summary>
    protected internal virtual bool AllowEnterToComplete { get; set; } = true;

    /// <summary>
    ///     Gets or sets the current text input.
    /// </summary>
    public virtual ReadOnlySeString String
    {
        get => Component->EvaluatedString.AsSpan();
        set
        {
            Component->SetText(value);
            OnTextChanged();
        }
    }

    /// <summary>
    ///     Gets or sets a string to be used as a placeholder when nothing has been input yet, or the input has been removed.
    /// </summary>
    public string? PlaceholderString
    {
        get;
        set
        {
            field = value;
            if (value is not null)
                PlaceholderTextNode.String = value;
            UpdatePlaceholderVisibility();
        }
    }

    /// <summary>
    ///     TextId of the placeholder string to load from <see cref="SheetType" />.
    /// </summary>
    public uint PlaceholderStringId
    {
        get => PlaceholderTextNode.TextId;
        set
        {
            PlaceholderTextNode.TextId = value;
            UpdatePlaceholderVisibility();
        }
    }

    /// <summary>
    ///     Gets or sets which data sheet to use to resolve <see cref="PlaceholderStringId" />.
    /// </summary>
    public NodeData.SheetType SheetType
    {
        get => PlaceholderTextNode.SheetType;
        set => PlaceholderTextNode.SheetType = value;
    }

    /// <summary>
    ///     Gets or sets whether the text input's border should be reddened to indicate an error.
    /// </summary>
    public bool IsError
    {
        get => FocusBorderNode.MultiplyColor == new Vector3(1.0f, 0.6f, 0.6f);
        set => FocusBorderNode.MultiplyColor = value ?
                                                   new Vector3(1.0f, 0.6f, 0.6f) :
                                                   Vector3.One;
    }

    /// <summary>
    ///     Gets or sets whether all text should be selected when this element becomes focused.
    /// </summary>
    public bool AutoSelectAll { get; set; } = true;

    /// <summary>
    ///     Gets or sets the action to be invoked when typing into the input, is triggered for each letter/symbol.
    ///     Includes a reference to the modified string.
    /// </summary>
    public virtual Action<ReadOnlySeString>? OnInputReceived { get; set; }

    /// <summary>
    ///     Gets or sets the action to be invoked when input is completed via pressing return.
    ///     Includes a reference to the modified string.
    /// </summary>
    public Action<ReadOnlySeString>? OnInputComplete { get; set; }

    /// <summary>
    ///     Gets or sets the action to be called once the element has lost focus.
    /// </summary>
    public Action? OnFocusLost { get; set; }

    /// <summary>
    ///     Gets or sets the action to be called when input is canceled via escape.
    /// </summary>
    public Action? OnEscapeEntered { get; set; }

    /// <summary>
    ///     Gets or sets the action to be called when tab is input.
    /// </summary>
    public Action? OnTabEntered { get; set; }

    /// <summary>
    ///     Gets or sets the action to be called when the input is focused.
    /// </summary>
    public Action? OnFocused { get; set; }

    /// <summary>
    ///     Gets or sets the action to be called when the node loses focus.
    /// </summary>
    /// <remarks>
    ///     May be redundant with <see cref="OnFocusLost" />
    /// </remarks>
    public Action? OnUnfocused { get; set; }

    /// <summary>
    ///     Clears the focus from this node.
    /// </summary>
    public void ClearFocus()
    {
        if (IsFocused)
            AtkStage.Instance()->AtkInputManager->SetFocus(null, Component->OwnerAddon, 0);
    }

    /// <inheritdoc />
    protected override void OnReceiveEvent
    (
        AtkComponentBase* thisPtr,
        AtkEventType      eventType,
        int               eventParam,
        AtkEvent*         atkEvent,
        AtkEventData*     atkEventData
    )
    {
        base.OnReceiveEvent(thisPtr, eventType, eventParam, atkEvent, atkEventData);
        if (IsDisposed) return;

        try
        {
            switch (eventType)
            {
                case AtkEventType.FocusStart:
                    if (IsFocused)
                        OnInputFocusStarted();
                    break;
                case AtkEventType.FocusStop:
                    focusVersion++;
                    UpdatePlaceholderVisibility();
                    OnUnfocused?.Invoke();
                    break;
            }
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
        }
    }

    /// <summary>
    ///     Updates the input's presentation after its text changes.
    /// </summary>
    protected virtual void OnTextChanged() => UpdatePlaceholderVisibility();

    /// <inheritdoc />
    protected override void OnSizeChanged()
    {
        base.OnSizeChanged();

        BackgroundNode.Size      = Size;
        FocusBorderNode.Size     = Size;
        PlaceholderTextNode.Size = Vector2.Max(new Vector2(Width - 20.0f, Height - 10.0f), Vector2.Zero);
        TextLimitsNode.Size      = Vector2.Max(new Vector2(Width + 18.0f, Height - 9.0f),  Vector2.Zero);
        CurrentTextNode.Size     = PlaceholderTextNode.Size;
        SelectionListNode.Y      = Height - 6.0f;
    }

    /// <inheritdoc />
    protected override void Dispose
    (
        bool disposing,
        bool isNativeDestructor
    )
    {
        if (!disposing) return;

        focusVersion++;
        if (!isNativeDestructor)
            Component->Callback = null;

        base.Dispose(disposing, isNativeDestructor);
        GC.KeepAlive(pinnedCallbackFunction);
    }

    private InputCallbackResult OnCallback
    (
        AtkUnitBase*      addon,
        InputCallbackType type,
        CStringPointer    rawString,
        CStringPointer    evaluatedString,
        int               eventKind
    )
    {
        try
        {
            switch (type)
            {
                case InputCallbackType.Enter:
                    if (!AllowEnterToComplete)
                        return InputCallbackResult.InsertLineBreak;
                    var completedString = String;
                    ClearFocus();
                    OnInputComplete?.Invoke(completedString);
                    break;

                case InputCallbackType.TextChanged:
                    var receivedString = String;
                    OnTextChanged();
                    OnInputReceived?.Invoke(receivedString);
                    break;

                case InputCallbackType.Escape:
                    OnEscapeEntered?.Invoke();
                    break;

                case InputCallbackType.FocusLost:
                    OnFocusLost?.Invoke();
                    break;

                case InputCallbackType.Tab:
                    OnTabEntered?.Invoke();
                    break;
            }

            return InputCallbackResult.None;
        }
        catch (Exception e)
        {
            IPluginLog.Get().Exception(e);
            return InputCallbackResult.None;
        }
    }

    private void OnInputFocusStarted()
    {
        var version = ++focusVersion;
        PlaceholderTextNode.IsVisible = false;

        if (AutoSelectAll && Component->EvaluatedString.Length > 0)
        {
            IFramework.Get().RunOnTick
            (
                () =>
                {
                    if (IsDisposed || focusVersion != version || !IsFocused) return;

                    var keyModifiers = new AtkTextInput.KeyModifiers
                    {
                        IsControlDown = true
                    };

                    AtkStage.Instance()->AtkInputManager->TextInput->ProcessKeyShortcut(SeVirtualKey.A, &keyModifiers);
                },
                delayTicks: 1
            );
        }

        OnFocused?.Invoke();
    }

    private void UpdatePlaceholderVisibility() =>
        PlaceholderTextNode.IsVisible = !IsFocused && String.IsEmpty && (!PlaceholderString.IsNullOrEmpty() || PlaceholderStringId != 0);

    private void LoadTimelines()
    {
        AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(1, 29)
                .AddLabelPair(1,  9,  17)
                .AddLabelPair(10, 19, 18)
                .AddLabelPair(20, 29, 7)
                .EndFrameSet()
                .Build()
        );

        BackgroundNode.AddTimeline
        (
            new TimelineBuilder()
                .AddFrameSetWithFrame(1, 9, 1, alpha: 255)
                .BeginFrameSet(10, 19)
                .AddFrame(10, alpha: 255)
                .AddFrame(12, alpha: 255)
                .EndFrameSet()
                .AddFrameSetWithFrame(20, 29, 20, alpha: 127)
                .Build()
        );

        FocusBorderNode.AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(10, 19)
                .AddFrame(10, alpha: 0)
                .AddFrame(12, alpha: 255)
                .EndFrameSet()
                .Build()
        );

        TextLimitsNode.AddTimeline
        (
            new TimelineBuilder()
                .AddFrameSetWithFrame(1, 9, 1, alpha: 102)
                .BeginFrameSet(10, 19)
                .AddFrame(10, alpha: 102)
                .AddFrame(12, alpha: 127)
                .EndFrameSet()
                .AddFrameSetWithFrame(20, 29, 20, alpha: 76)
                .Build()
        );

        CursorNode.AddTimeline
        (
            new TimelineBuilder()
                .BeginFrameSet(1, 15)
                .AddLabel(1,  101, AtkTimelineJumpBehavior.Start,       0)
                .AddLabel(15, 0,   AtkTimelineJumpBehavior.LoopForever, 101)
                .EndFrameSet()
                .Build()
        );
    }
}
