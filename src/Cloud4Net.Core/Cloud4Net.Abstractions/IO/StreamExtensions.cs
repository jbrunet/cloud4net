#region License
// Copyright (c) 2009-2010 Topian System - http://www.topian.net
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

namespace System.IO
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream target, long? length)
        {
            const int maxChunkSize = 16 * 1024;
            var chunkSize = (int)Math.Min(maxChunkSize, length.GetValueOrDefault(int.MaxValue));
            var buffer = new byte[chunkSize];
            while (true)
            {
                var toRead = (int)Math.Min(chunkSize, length.GetValueOrDefault(chunkSize));
                if (toRead == 0)
                    break;
                var read = source.Read(buffer, 0, toRead);
                if (read == 0)
                    break;
                target.Write(buffer, 0, read);
                if (length.HasValue)
                    length -= toRead;
            }
        }

        public static byte[] ToArray(this Stream stream)
        {
            var ms1 = stream as MemoryStream;
            if (ms1 != null)
                return ms1.ToArray();
            using (var ms2 = new MemoryStream((int)Math.Min(int.MaxValue, stream.Length)))
            {
                stream.CopyTo(ms2, null);
                return ms2.ToArray();
            }
        }
    }
}