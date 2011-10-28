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
using System.Net;
using System.Reflection;

namespace System.StorageModel.Diagnostics
{
    public abstract class NetworkMonitor : TraceListener
    {
        protected static TraceSource SetupSource(string propertyName)
        {
            var logging = typeof(HttpWebRequest).Assembly.GetType("System.Net.Logging", true);
            logging.InvokeMember("InitializeLogging",
                                 BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static, null, null,
                                 null);
            var enabledField = logging.GetField("s_LoggingEnabled", BindingFlags.NonPublic | BindingFlags.Static);
            enabledField.SetValue(null, true);

            var property = logging.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Static);
            var source = (TraceSource)property.GetValue(null, null);
            return source;
        }

        public override void Write(string message)
        {
        }

        public override void WriteLine(string message)
        {
        }

        protected static int SkipThreadID(ref string message)
        {
            var p = message.IndexOf(']');
            if (p != -1)
                message = message.Substring(p + 2);
            return p;
        }

        protected static int FindMatch(ref string message, string prefix, out string objectID, params string[] suffixes)
        {
            if (message.StartsWith(prefix))
            {
                var startIndex = prefix.Length;
                for (var i = 0; i < suffixes.Length; i++)
                {
                    var suffix = suffixes[i];
                    var p = message.IndexOf(suffix, startIndex);
                    if (p != -1)
                    {
                        objectID = message.Substring(startIndex, p - startIndex);
                        message = message.Substring(p + suffix.Length);
                        return i;
                    }
                }
            }
            objectID = null;
            return -1;
        }

        protected static string TakeUntil(ref string message, char stopChar)
        {
            var p = message.IndexOf(stopChar);
            if (p == -1)
                return null;
            var token = message.Substring(0, p);
            message = message.Substring(p);
            return token;
        }
    }

    public sealed class WebRequestMonitor : NetworkMonitor
    {
        private static readonly TraceSource _source;
        internal static readonly Dictionary<long, WebRecord> _recordPerID = new Dictionary<long, WebRecord>();

        #region .ctor

        static WebRequestMonitor()
        {
            _source = SetupSource("Web");
        }

        #endregion

        public static WebRequestMonitor Enable()
        {
            _source.Switch.Level |= SourceLevels.Verbose;
            return _source.EnsureListener<WebRequestMonitor>();
        }

        private const string SystemNetSource = "System.Net";
        private const string HttpWebRequestPrefix = "HttpWebRequest#";
        private const string _0_HttpWebRequestSuffix = "::HttpWebRequest(";
        private const string _1_RequestSuffix = " - Request: ";
        private const string _2_EndGetResponseSuffix = "::EndGetResponse(";

        private const string ExitingHttpWebRequestPrefix = "Exiting HttpWebRequest#";
        private const string _0_GetResponseSuffix = "::GetResponse()";

        private static void EndRequest(string requestID)
        {
            WebRecord rec;
            lock (_recordPerID)
            {
                var lRequestID = long.Parse(requestID);
                if (!_recordPerID.TryGetValue(lRequestID, out rec))
                    return;

                rec.BytesReceived = BandwithMonitor.BytesReceived;
                rec.BytesSent = BandwithMonitor.BytesSent;
                rec.TimeTaken = new TimeSpan(Environment.TickCount-rec.RequestTicks);
                BandwithMonitor.BytesReceived = 0;
                BandwithMonitor.BytesSent = 0;
                _recordPerID.Remove(lRequestID);
            }
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (source != SystemNetSource || SkipThreadID(ref message) == -1)
                return;

            string requestID;
            WebRecord rec;
            switch (FindMatch(ref message, HttpWebRequestPrefix, out requestID
                              , _0_HttpWebRequestSuffix
                              , _1_RequestSuffix
                              , _2_EndGetResponseSuffix))
            {
                case 0: // HttpWebRequest#48285313::HttpWebRequest(http://...#-1132631513)
                    var log = WebRequestLog.Current;
                    if (log == null)
                        return;
                    var url = TakeUntil(ref message, '#');
                    if (url == null)
                        return;
                    rec = new WebRecord
                              {
                                  RequestID = long.Parse(requestID),
                                  Uri = new Uri(url),
                                  Log = WebRequestLog.Current,
                                  RequestTicks = Environment.TickCount,
                              };
                    lock (_recordPerID)
                        _recordPerID[rec.RequestID] = rec;
                    log.Records.Enqueue(rec);
                    return;

                case 1: // HttpWebRequest#48285313 - Request: HEAD /... HTTP/1.1
                    if (!_recordPerID.TryGetValue(long.Parse(requestID), out rec))
                        return;

                    rec.HttpMethod = TakeUntil(ref message, ' ');
                    break;

                case 2: // HttpWebRequest#43495525::EndGetResponse()
                case 3: // HttpWebRequest#43495525::GetResponse()
                    EndRequest(requestID);
                    break;

            }

            switch (FindMatch(ref message, ExitingHttpWebRequestPrefix, out requestID
                , _0_GetResponseSuffix))
            {
                case 0:
                    // Exiting HttpWebRequest#64116351::GetResponse() 	-> HttpWebResponse#30953898
                    EndRequest(requestID);
                    break;
            }
        }
    }

    public sealed class BandwithMonitor : NetworkMonitor
    {
        private static readonly TraceSource _source;

        #region .ctor

        static BandwithMonitor()
        {
            _source = SetupSource("Sockets");
        }

        #endregion

        #region Properties

        public static long BytesSent;
        public static long BytesReceived;

        #endregion

        public static BandwithMonitor Enable()
        {
            _source.Switch.Level |= SourceLevels.Verbose;
            return _source.EnsureListener<BandwithMonitor>();
        }

        private const string SystemNetSocketsSource = "System.Net.Sockets";
        private const string ExitingSocket = "Exiting Socket#";
        private const string _0_SendSuffix = "::Send()";
        private const string _1_EndSendSuffix = "::EndSend()";
        private const string _2_ReceiveSuffix = "::Receive()";
        private const string _3_EndReceiveSuffix = "::EndReceive()";
        private const string ArrowPrefix = " \t-> ";

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (source != SystemNetSocketsSource || SkipThreadID(ref message) == -1)
                return;

            string socketID;
            bool isSend;
            switch (FindMatch(ref message, ExitingSocket, out socketID
                              , _0_SendSuffix
                              , _1_EndSendSuffix
                              , _2_ReceiveSuffix
                              , _3_EndReceiveSuffix))
            {

                case 0: // Exiting Socket#34948909::EndSend() 	-> 277#277
                case 1: // Exiting Socket#34948909::Send() 	-> 277#277
                    isSend = true;
                    break;
                case 2: // [3164] Exiting Socket#34948909::EndReceive() 	-> 290#290
                case 3: // [3164] Exiting Socket#34948909::Receive() 	-> 290#290
                    isSend = false;
                    break;
                default:
                    return;
            }
            if (!message.StartsWith(ArrowPrefix))
                return;
            message = message.Substring(ArrowPrefix.Length);
            var bytesStr = TakeUntil(ref message, '#');
            if (bytesStr == null)
                return;
            int bytes;
            if (!int.TryParse(bytesStr, out bytes))
                return;

            if (isSend)
                BytesSent += bytes;
            else
                BytesReceived += bytes;
        }
    }
}