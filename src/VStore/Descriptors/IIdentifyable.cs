using System;

namespace NuClear.VStore.Descriptors
{
    public interface IIdentifyable<out T> where T : IEquatable<T>
    {
        T Id { get; }
    }
}