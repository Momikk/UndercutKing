using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Memory;

namespace KamiToolKit.Internal.Extensions;

internal static unsafe class MemorySpaceExtensions
{
    extension
    (
        ref IMemorySpace memorySpace
    )
    {
        public T* MallocZeroed<T>() where T : unmanaged
        {
            var blockSize     = (nuint)sizeof(T);
            var memoryPointer = memorySpace.Malloc<T>();

            if (memoryPointer is null)
                throw new OutOfMemoryException($"Unable to allocate memory for {typeof(T)}.");

            NativeMemory.Clear(memoryPointer, blockSize);

            return memoryPointer;
        }

        public T* AllocateZeroedArray<T>
        (
            int count
        ) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (count is 0) return null;

            var blockSize     = checked((nuint)sizeof(T) * (nuint)count);
            var memoryPointer = (T*)memorySpace.Malloc(blockSize, 8);

            if (memoryPointer is null)
                throw new OutOfMemoryException($"Unable to allocate memory for {count} {typeof(T)} values.");

            NativeMemory.Clear(memoryPointer, blockSize);

            return memoryPointer;
        }

        public T* AllocateZeroedArray<T>
        (
            uint count
        ) where T : unmanaged
            => memorySpace.AllocateZeroedArray<T>(checked((int)count));

        public T* Realloc<T>
        (
            void* memory,
            int   newCount
        ) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(newCount);
            return memorySpace.Realloc<T>(memory, (uint)newCount);
        }

        public T* Realloc<T>
        (
            void* memory,
            uint  newCount
        ) where T : unmanaged
        {
            var blockSize     = checked((ulong)sizeof(T) * newCount);
            var memoryPointer = (T*)memorySpace.AlignedRealloc(memory, blockSize, 16);

            if (memoryPointer is null && newCount is not 0)
                throw new OutOfMemoryException($"Unable to reallocate memory for {newCount} {typeof(T)} values.");

            return memoryPointer;
        }

        public static void Copy<T>
        (
            T*  oldBuffer,
            T*  newBuffer,
            int count
        ) where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            IMemorySpace.Copy(oldBuffer, newBuffer, (uint)count);
        }

        public static void Copy<T>
        (
            T*   oldBuffer,
            T*   newBuffer,
            uint count
        ) where T : unmanaged
            => NativeMemory.Copy(oldBuffer, newBuffer, checked((nuint)sizeof(T) * count));
    }
}
