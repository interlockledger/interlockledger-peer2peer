/******************************************************************************************************************************
 *
 *      Copyright (c) 2017-2018 InterlockLedger Network
 *
 ******************************************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

#pragma warning disable S3887 // Mutable, non-private fields should not be "readonly"

namespace InterlockLedger.Peer2Peer
{
    public struct Response
    {
        public Response(MemoryStream ms) : this(ms.ToArray()) { }

        public Response(ReadOnlyMemory<byte> readOnlyMemory) : this(readOnlyMemory.ToArray()) { }

        public Response(ArraySegment<byte> data) : this(new List<ArraySegment<byte>>() { data }) { }

        public Response(byte[] array) : this(array, 0, array.Length) { }

        public Response(byte[] array, int start, int length) : this(new ArraySegment<byte>(array, start, length)) { }

        public Response(IEnumerable<ArraySegment<byte>> dataList) {
            if (dataList == null)
                throw new ArgumentNullException(nameof(dataList));
            _segmentList = new List<ArraySegment<byte>>(dataList);
            _dataList = null;
        }

        public static Response Done { get; } = new Response(Enumerable.Empty<ArraySegment<byte>>(), true);

        public IList<ArraySegment<byte>> DataList => _dataList ?? (_dataList = _segmentList.AsReadOnly());

        public bool Exit => !DataList.Any(s => s.Count > 0);

        public Response Add(byte[] array) => Add(new ArraySegment<byte>(array));

        public Response Add(byte[] array, int start, int length) => Add(new ArraySegment<byte>(array, start, length));

        public Response Add(ArraySegment<byte> data) {
            if (_dataList == null)
                _segmentList.Add(data);
            return this;
        }

        private readonly List<ArraySegment<byte>> _segmentList;
        private ReadOnlyCollection<ArraySegment<byte>> _dataList;

        private Response(IEnumerable<ArraySegment<byte>> dataList, bool readOnly) : this() {
            _segmentList = new List<ArraySegment<byte>>(dataList);
            _dataList = readOnly ? _segmentList.AsReadOnly() : null;
        }
    }
}