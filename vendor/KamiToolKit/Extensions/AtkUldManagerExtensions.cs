using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using KamiToolKit.BaseTypes;
using KamiToolKit.Internal.Classes;

namespace KamiToolKit.Extensions;

/// <summary>
///     Extension methods for AtkUldManager's.
/// </summary>
public static unsafe class AtkUldManagerExtensions
{
    extension
    (
        ref AtkUldManager manager
    )
    {
        private Span<Pointer<AtkResNode>> ObjectNodeSpan
            => new(manager.Objects->NodeList, manager.Objects->NodeCount);

        private bool HasObjectList
        {
            get
            {
                const AtkUldManagerResourceFlag REQUIRED_FLAGS =
                    AtkUldManagerResourceFlag.Initialized | AtkUldManagerResourceFlag.ArraysAllocated;

                if ((manager.ResourceFlags & REQUIRED_FLAGS) != REQUIRED_FLAGS) return false;
                if (manager.Objects is null            || manager.Objects->NodeCount < 0) return false;
                return manager.Objects->NodeCount is 0 || manager.Objects->NodeList is not null;
            }
        }

        /// <summary>
        ///     Adds node and all children nodes to this UldManager's Object List
        /// </summary>
        [OverloadResolutionPriority(1)]
        public void AddNodeToObjectList
        (
            NodeBase node
        )
        {
            if (!manager.HasObjectList) return;

            var oldSize = manager.Objects->NodeCount;

            manager.AddNodeToObjectList(node.ResNode);

            foreach (var child in NodeBase.GetLocalChildren(node))
                manager.AddNodeToObjectList(child.ResNode);

            if (manager.Objects->NodeCount != oldSize && !node.SuppressNativeUpdate)
                manager.UpdateDrawNodeList();
        }

        /// <summary>
        ///     Adds just this node to the UldManagers Object List.
        /// </summary>
        [OverloadResolutionPriority(0)]
        public void AddNodeToObjectList
        (
            AtkResNode* newNode
        )
        {
            if (!manager.HasObjectList) return;
            if (newNode is null) return;

            // If the node is already in the object list, skip.
            if (manager.IsNodeInObjectList(newNode)) return;

            var oldSize = manager.Objects->NodeCount;
            var newSize = oldSize + 1;

            var newBuffer = (AtkResNode**)IMemorySpace.GetUISpace()->Realloc<nint>(manager.Objects->NodeList, newSize);
            newBuffer[newSize - 1] = newNode;

            manager.Objects->NodeList  = newBuffer;
            manager.Objects->NodeCount = newSize;
        }

        /// <summary>
        ///     Removes node and all children nodes from this UldManager's Object List
        /// </summary>
        public void RemoveNodeFromObjectList
        (
            NodeBase node
        )
        {
            if (!manager.HasObjectList) return;

            var oldSize = manager.Objects->NodeCount;

            manager.RemoveNodeFromObjectList(node.ResNode);

            foreach (var child in NodeBase.GetLocalChildren(node))
                manager.RemoveNodeFromObjectList(child.ResNode);

            if (manager.Objects->NodeCount != oldSize && !node.SuppressNativeUpdate)
                manager.UpdateDrawNodeList();
        }

        /// <summary>
        ///     Removes just this node from the UldManagers Object List.
        /// </summary>
        public void RemoveNodeFromObjectList
        (
            AtkResNode* node
        )
        {
            if (!manager.HasObjectList) return;
            if (node is null) return;

            var oldSize = manager.Objects->NodeCount;
            if (oldSize is 0) return;

            var nodeList    = manager.Objects->NodeList;
            var removeIndex = -1;

            for (var index = 0; index < oldSize; index++)
                if (nodeList[index] == node)
                {
                    removeIndex = index;
                    break;
                }

            if (removeIndex < 0) return;

            var newSize = oldSize - 1;
            for (var index = removeIndex; index < newSize; index++)
                nodeList[index] = nodeList[index + 1];

            var newBuffer = (AtkResNode**)IMemorySpace.GetUISpace()->Realloc<nint>(nodeList, Math.Max(newSize, 1));
            if (newSize is 0)
                newBuffer[0] = null;

            manager.Objects->NodeList  = newBuffer;
            manager.Objects->NodeCount = newSize;
        }

        /// <summary>
        ///     Debug helper for printing a UldManagers entire object list.
        /// </summary>
        public void PrintObjectList()
        {
            if (!manager.HasObjectList) return;

            IPluginLog.Get().Debug("Beginning NodeList");

            foreach (var index in Enumerable.Range(0, manager.Objects->NodeCount))
            {
                var nodePointer = manager.Objects->NodeList[index];
                IPluginLog.Get().Debug($"[{index}]: {(nint)nodePointer:X}");
            }
        }

        /// <summary>
        ///     Helper to search for a node by id, helpful for AtkLists as the GetNodeById doesn't return duplicated nodes.
        /// </summary>
        public T* SearchNodeById<T>
        (
            uint nodeId
        ) where T : unmanaged
        {
            foreach (var node in manager.Nodes)
            {
                if (node.Value is not null)
                {
                    if (node.Value->NodeId == nodeId)
                        return (T*)node.Value;
                }
            }

            return null;
        }

        /// <summary>
        ///     Default typed SearchNodeById helper.
        /// </summary>
        public AtkResNode* SearchNodeById
        (
            uint nodeId
        )
            => manager.SearchNodeById<AtkResNode>(nodeId);

        private bool IsNodeInObjectList
        (
            AtkResNode* node
        )
        {
            foreach (var objectNode in manager.ObjectNodeSpan)
                if (objectNode.Value == node)
                    return true;

            return false;
        }
    }
}
