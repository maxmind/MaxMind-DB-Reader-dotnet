using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MaxMind.Db
{
    internal interface IByteReader: IDisposable
    {
        int Length { get; }

        byte[] Read(int offset, int count);
        byte ReadOne(int offset);

        void Copy(int offset, byte[] array);
    }
}
