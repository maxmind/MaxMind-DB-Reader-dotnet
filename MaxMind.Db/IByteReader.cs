#region

using System;

#endregion

namespace MaxMind.Db
{
    internal interface IByteReader : IDisposable
    {
        int Length { get; }

        byte[] Read(long offset, int count);

        byte ReadOne(long offset);

        void Copy(long offset, byte[] array);
    }
}