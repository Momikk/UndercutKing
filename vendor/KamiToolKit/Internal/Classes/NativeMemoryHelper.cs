using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Memory;

namespace KamiToolKit.Internal.Classes;

internal static class NativeMemoryHelper
{
    public static unsafe T* UiAlloc<T>
    (
        int   elementCount,
        ulong alignment = 8
    ) where T : unmanaged
        => UiAlloc<T>((uint)elementCount, alignment);

    public static unsafe T* UiAlloc<T>
    (
        uint  elementCount = 1,
        ulong alignment    = 8
    ) where T : unmanaged
    {
        var allocSize = (ulong)sizeof(T) * elementCount;
        var memory    = (T*)IMemorySpace.GetUISpace()->Malloc(allocSize, alignment);

        if (memory is null)
            throw new Exception($"Unable to allocate memory for {typeof(T)}");

        IMemorySpace.Memset(memory, 0, allocSize);

        return memory;
    }

    public static unsafe void UiFree<T>
    (
        T* memory
    ) where T : unmanaged
        => IMemorySpace.Free(memory);

    public static unsafe void UiFree<T>
    (
        T*   memory,
        uint elementCount
    ) where T : unmanaged
        => IMemorySpace.Free(memory, (ulong)sizeof(T) * elementCount);

    public static unsafe T* Create<T>() where T : unmanaged, ICreatable<T>
    {
        var memory = IMemorySpace.GetUISpace()->Create<T>();

        if (memory is null)
            throw new Exception($"Unable to allocate memory for {typeof(T)}");

        return memory;
    }

    public static unsafe nint Malloc
    (
        ulong size,
        ulong alignment = 8
    )
    {
        var memory = (nint)IMemorySpace.GetUISpace()->Malloc(size, alignment);

        if (memory is 0 && size is not 0)
            throw new OutOfMemoryException($"Unable to allocate {size} bytes.");

        return memory;
    }

    public static unsafe void Free
    (
        void* memory,
        ulong size
    )
        => IMemorySpace.Free(memory, size);

    public static unsafe void* Realloc
    (
        void* memory,
        ulong size
    )
    {
        var resizedMemory = IMemorySpace.GetUISpace()->AlignedRealloc(memory, size, 0x10);

        if (resizedMemory is null && size is not 0)
            throw new OutOfMemoryException($"Unable to reallocate {size} bytes.");

        return resizedMemory;
    }

    public static unsafe void ResizeArray<T>
    (
        ref T* array,
        int    oldSize,
        uint   newSize
    ) where T : unmanaged
        => ResizeArray(ref array, oldSize, checked((int)newSize));

    public static unsafe void ResizeArray<T>
    (
        ref T* array,
        uint   oldSize,
        uint   newSize
    ) where T : unmanaged
        => ResizeArray(ref array, checked((int)oldSize), checked((int)newSize));

    public static unsafe void ResizeArray<T>
    (
        ref T* array,
        uint   oldSize,
        int    newSize
    ) where T : unmanaged
        => ResizeArray(ref array, checked((int)oldSize), newSize);

    public static unsafe void ResizeArray<T>
    (
        ref T* array,
        int    oldSize,
        int    newSize
    ) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(oldSize);
        ArgumentOutOfRangeException.ThrowIfNegative(newSize);

        if (newSize is 0)
        {
            if (array is not null)
            {
                UiFree(array, (uint)oldSize);
                array = null;
            }

            return;
        }

        var newBuffer = UiAlloc<T>((uint)newSize);

        if (array is not null)
            Copy(array, newBuffer, Math.Min(oldSize, newSize));

        if (array is not null)
            UiFree(array, (uint)oldSize);

        array = newBuffer;
    }

    public static unsafe void Copy<T>
    (
        T*  oldBuffer,
        T*  newBuffer,
        int count
    ) where T : unmanaged
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Copy(oldBuffer, newBuffer, (uint)count);
    }

    public static unsafe void Copy<T>
    (
        T*   oldBuffer,
        T*   newBuffer,
        uint count
    ) where T : unmanaged
        => NativeMemory.Copy(oldBuffer, newBuffer, checked((nuint)sizeof(T) * count));

    public static unsafe void MemCopy<T>
    (
        T*   oldBuffer,
        T*   newBuffer,
        uint byteCount
    ) where T : unmanaged
        => NativeMemory.Copy(oldBuffer, newBuffer, byteCount);
}
