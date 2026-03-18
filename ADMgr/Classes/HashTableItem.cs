using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ADMgr
{
    public class HashTableItem
    {
        public Byte[] hash;
        public List<Int32> trace;

        public HashTableItem(Byte[] _hash, List<Int32> _trace)
        {
            hash = new Byte[16];
            _hash.CopyTo(hash, 0);
            trace = new List<Int32>(_trace);
        }

        public HashTableItem(Byte[] _hash, Int32 _index)
        {
            hash = new Byte[16];
            _hash.CopyTo(hash, 0);
            trace = new List<Int32>();
            trace.Add(_index);
        }
    }
}
