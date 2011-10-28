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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.StorageModel.Diagnostics
{
    public class WebRequestLog : IDisposable
    {
        private static WebRequestLog _current;
        private List<WebRequestLog> _innerContexts;
        private Queue<WebRecord> _records;
        private readonly Stopwatch _watch;

        #region Properties

        public static readonly object SyncObject = new object();
        public static WebRequestLog Current { get { return _current; } }
        public WebRequestLog Parent { get; private set; }
        public Queue<WebRecord> Records
        {
            get { return (_records ?? (_records = new Queue<WebRecord>())); }
        }
        public List<WebRequestLog> InnerContexts
        {
            get { return (_innerContexts ?? (_innerContexts = new List<WebRequestLog>())); }
        }
        public StorageProvider Provider { get; set; }

        #endregion

        #region .ctor/Dispose

        public WebRequestLog()
        {
            Monitor.Enter(SyncObject);
            Parent = _current;
            if (Parent != null)
                Parent.InnerContexts.Add(this);
            _current = this;
            _watch = new Stopwatch();
            _watch.Start();
        }

        public void Dispose()
        {
            _watch.Stop();
            _current = Parent;
            Monitor.Exit(SyncObject);
        }

        public static TLog TryCreate<TLog>(Func<TLog> create)
            where TLog : WebRequestLog
        {
            return (Current == null)
                ? null
                : create();
        }

        #endregion

        public IEnumerable<WebRecord> GetAllRecords()
        {
            if (_records != null)
                foreach (var record in _records)
                    yield return record;
            if (_innerContexts != null)
                foreach (var context in _innerContexts)
                    foreach (var record in context.GetAllRecords())
                        yield return record;
        }

        public TimeSpan ElapsedTime
        {
            get { return _watch.Elapsed; }
        }
    }

    public class WebRecord
    {
        public long RequestID;
        public string HttpMethod;
        public Uri Uri;
        public long BytesSent;
        public long BytesReceived;
        public long RequestTicks;
        //public long ResponseTicks;
        public WebRequestLog Log;

        public TimeSpan TimeTaken;
        //{
        //    get { return TimeSpan.FromTicks(ResponseTicks - RequestTicks); }
        //}

        public override string ToString()
        {
            return string.Format("#{0} {1} {2} Sent={3} Received={4} TimeTaken={5}"
                , RequestID
                , HttpMethod
                , Uri
                , BytesSent
                , BytesReceived
                , TimeTaken);
        }
    }
}