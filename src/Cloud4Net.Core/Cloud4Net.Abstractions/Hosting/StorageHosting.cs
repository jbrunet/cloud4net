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
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace System.StorageModel.Hosting
{
    #region Interfaces

    public interface IStorageHostProvider
    {
        void Setup(StorageHostContext context);
    }

    public interface IWebContext
    {
        IWebRequest Request { get; }
        IWebResponse Response { get; }
    }

    public interface IWebRequest
    {
        Uri Url { get; }
        NameValueCollection QueryString { get; }
        string HttpMethod { get; }
        NameValueCollection Headers { get; }
    }

    public interface IWebResponse
    {
        HttpStatusCode StatusCode { get; set; }
        string StatusDescription { get; set; }
        void AppendHeader(string name, string value);
        NameValueCollection Headers { get; }
        Stream OutputStream { get; }
    }

    #endregion

    public class StorageHostContext : IDisposable
    {
        private readonly Dictionary<HttpListener, Action<IWebContext>> _dic;

        #region .ctor

        public StorageHostContext(params IStorageHostProvider[] providers)
        {
            _dic = new Dictionary<HttpListener, Action<IWebContext>>();
            foreach (var provider in providers)
                provider.Setup(this);
            foreach (var listener in _dic.Keys)
            {
                listener.Start();
                listener.BeginGetContext(AsyncGetContext, listener);
            }
        }

        #endregion

        private void AsyncGetContext(IAsyncResult ar)
        {
            var listener = (HttpListener)ar.AsyncState;
            if (!listener.IsListening)
                return;
            var context = new HttpListenerContextWrapper(listener.EndGetContext(ar));
            var process = _dic[listener];
            process(context);
            listener.BeginGetContext(AsyncGetContext, listener);
        }

        public void Dispose()
        {
            foreach (var listener in _dic.Keys)
            {
                listener.Abort();
                listener.Stop();
                var d = (IDisposable)listener;
                d.Dispose();
            }
        }

        public void Register(Action<IWebContext> process, params Uri[] uriPrefixes)
        {
            var listener = new HttpListener();
            foreach (var uriPrefix in uriPrefixes)
            {
                listener.Prefixes.Add(uriPrefix.ToString());
            }
            _dic.Add(listener, process);
        }

        internal class HttpListenerContextWrapper : IWebContext
        {
            #region .ctor

            internal HttpListenerContextWrapper(HttpListenerContext context)
            {
                Request = new HttpListenerRequestWrapper(context.Request);
                Response = new HttpListenerResponseWrapper(context.Response);
            }

            #endregion

            public IWebRequest Request { get; private set; }
            public IWebResponse Response { get; private set; }
        }

        internal class HttpListenerRequestWrapper : IWebRequest
        {
            private readonly HttpListenerRequest _impl;

            #region .ctor

            internal HttpListenerRequestWrapper(HttpListenerRequest impl)
            {
                _impl = impl;
            }

            #endregion

            public Uri Url { get { return _impl.Url; } }
            public NameValueCollection QueryString { get { return _impl.QueryString; } }
            public string HttpMethod { get { return _impl.HttpMethod; } }
            public NameValueCollection Headers { get { return _impl.Headers; } }
        }

        internal class HttpListenerResponseWrapper : IWebResponse
        {
            private readonly HttpListenerResponse _impl;

            #region .ctor

            internal HttpListenerResponseWrapper(HttpListenerResponse impl)
            {
                _impl = impl;
            }

            #endregion

            public HttpStatusCode StatusCode
            {
                get { return (HttpStatusCode)_impl.StatusCode; }
                set { _impl.StatusCode = (int)value; }
            }

            public string StatusDescription
            {
                get { return _impl.StatusDescription; }
                set { _impl.StatusDescription = value; }
            }

            public void AppendHeader(string name, string value)
            {
                _impl.AppendHeader(name, value);
            }

            public NameValueCollection Headers { get { return _impl.Headers; } }

            public Stream OutputStream { get { return _impl.OutputStream; } }
        }
    }
}