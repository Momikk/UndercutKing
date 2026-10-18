using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.BaseTypes;

/// <summary>
///     Generic Base class for all nodes used in KamiToolKit.
///     This is not intended for external use. If you need base type use <see cref="NodeBase" />.
/// </summary>
public abstract unsafe class NodeBase<T> : NodeBase where T : unmanaged, ICreatable<T>
{
    /// <summary>
    ///     Allocates an initializes a <see cref="NodeBase{T}" />
    /// </summary>
    protected NodeBase
    (
        NodeType nodeType
    )
    {

        IPluginLog.Get().Verbose($"Creating new node {GetType()}");
        Node = NativeMemoryHelper.Create<T>();

        if (ResNode is null)
            throw new Exception($"Unable to allocate memory for {typeof(T)}");

        KamiToolKitLibrary.AllocatedNodes?.TryAdd((nint)Node, GetType());

        BuildVirtualTable();

        ResNode->Type   = nodeType;
        ResNode->NodeId = NODE_ID_BASE + CurrentOffset++;
        ResNode->ToggleVisibility(true);

        CreatedNodes.Add(this);
    }

    /// <summary>
    ///     Gets the typed inner node pointer for this instance.
    /// </summary>
    public T* Node { get; private set; }

    public sealed override AtkResNode* ResNode => (AtkResNode*)Node;

    /// <summary>
    ///     Implicit operator to seamlessly cast this instance with the contained node type pointer.
    /// </summary>
    public static implicit operator T*
    (
        NodeBase<T> node
    ) => node.Node;

    /// <inheritdoc />
    protected override void Dispose
    (
        bool disposing,
        bool isNativeDestructor
    )
    {
        if (disposing)
        {
            try
            {
                base.Dispose(disposing, isNativeDestructor);
            }
            catch (Exception e)
            {
                IPluginLog.Get().Exception(e);
            }
            finally
            {
                if (!isNativeDestructor)
                    OriginalDestroy(this, true);

                KamiToolKitLibrary.AllocatedNodes?.Remove((nint)Node, out _);

                Node = null;
            }
        }
    }
}
