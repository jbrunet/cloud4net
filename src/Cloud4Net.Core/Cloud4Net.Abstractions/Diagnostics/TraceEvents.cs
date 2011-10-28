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
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace System.StorageModel.Diagnostics
{
    using Reflection;

    public class TraceEventSource : TraceSource
    {
        private int _newEventCode;
        //private static readonly Stack<int> _reservedEventCodes = new Stack<int>();

        #region .ctor

        public TraceEventSource(Type type, SourceLevels defaultLevel, int eventCodeBase)
            : this(type.FullName, defaultLevel, eventCodeBase)
        {
        }

        public TraceEventSource(string name, SourceLevels defaultLevel, int eventCodeBase)
            : base(name, defaultLevel)
        {
            var section = ConfigurationManager.GetSection("system.diagnostics");
            var sources = section.GetInstanceProperty<ConfigurationElementCollection>("Sources");

            var baseGet = sources.GetMethod<string, ConfigurationElement>("BaseGet");
            var source = baseGet(name);
            if (source == null)
                Debug.WriteLine(string.Format("Warning: TraceSource '{0}' is not defined in the configuration file",
                                              name));

            _newEventCode = eventCodeBase - 1;
            //if (_newEventCode + 1 < WebEventCodes.WebExtendedBase)
            //    throw new ArgumentException(
            //        string.Format("eventCodeBase must be >= {0}", WebEventCodes.WebExtendedBase)
            //        , "eventCodeBase");
        }

        #endregion

        internal int NewEventCode()
        {
            var value = Interlocked.Increment(ref _newEventCode);
            //lock (_reservedEventCodes)
            //{
            //    if (_reservedEventCodes.Contains(value))
            //        throw new Exception(string.Format("EventCode {0} is already bound", value));
            //    _reservedEventCodes.Push(value);
            //}
            return value;
        }
    }

    public static class TraceEvents
    {
        public static T EnsureListener<T>(this TraceSource source)
            where T : TraceListener, new()
        {
            var listener = source.Listeners.OfType<T>().FirstOrDefault();
            if (listener == null)
            {
                listener = new T();
                source.Listeners.Add(new T());
            }
            return listener;
        }
    }
}