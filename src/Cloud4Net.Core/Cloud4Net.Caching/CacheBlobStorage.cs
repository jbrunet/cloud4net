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
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Caching;

namespace System.StorageModel.Caching
{
    using Configuration;

    partial class AspNetCacheProvider : IBlobProvider
    {
        public IBlobProvider ChainBlobProvider { get; set; }
        public string CacheKey { get; private set; }
        public TimeSpan Sliding { get; set; }
        public AspNetCacheBlobContainerCollection Containers { get; private set; }

        #region .ctor

        partial void InitializeBlobs(NameValueCollection config)
        {
            var chainProvider = config["ChainProvider"];
            ChainBlobProvider = Storage.GetProvider<IBlobProvider>(chainProvider);
            CacheKey = "webcache:blobs";
            Sliding = TimeSpan.FromMinutes(1);
            Containers = new AspNetCacheBlobContainerCollection(this);
            BlobStorage.Initialize();
        }

        #endregion

        #region IBlobProvider Members

        IBlobContainer IBlobProvider.NewContainer(string name, StorageResourceDefinition config)
        {
            return new AspNetCacheBlobContainer(this, name);
        }

        IBlobSpec IBlobProvider.NewSpec(IBlobContainer container, string path, StorageResourceDefinition config)
        {
            var cContainer = (AspNetCacheBlobContainer)container;
            return new AspNetCacheBlobSpec(cContainer, path);
        }

        IBlobContainerCollection IBlobProvider.Containers
        {
            get { return this.Containers; }
        }

        public string NormalizeContainerName(string name)
        {
            return name;
        }

        public string NormalizeBlobPath(string path)
        {
            return path;
        }

        public bool Copy(IBlobSpec spec1, IBlobSpec target, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            return false;
        }

        #endregion
    }

    public sealed class AspNetCacheBlobContainerCollection : BlobContainerCollection<AspNetCacheProvider, AspNetCacheBlobContainer>
    {
        #region .ctor

        internal AspNetCacheBlobContainerCollection(AspNetCacheProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AspNetCacheBlobContainer> FindAll()
        {
            return (from container in Provider.ChainBlobProvider.Containers.FindAll()
                    select new AspNetCacheBlobContainer(Provider, container.Name, container));
        }
    }

    public sealed class AspNetCacheBlobContainer : BlobContainer<AspNetCacheProvider, AspNetCacheBlobSpecCollection>
    {
        public IBlobContainer ChainContainer { get; private set; }
        public string CacheKey { get; private set; }

        #region .ctor

        public AspNetCacheBlobContainer(AspNetCacheProvider provider, string name, IBlobContainer chainContainer)
            : base(provider, name)
        {
            ChainContainer = chainContainer;
            CacheKey = Provider.CacheKey + "/" + ChainContainer.Name;
            this.Blobs = new AspNetCacheBlobSpecCollection(this);
            Refresh();
        }

        public AspNetCacheBlobContainer(AspNetCacheProvider provider, string name)
            : this(provider, name, provider.ChainBlobProvider.Containers[name])
        {
        }

        #endregion

        internal void EnsureCache()
        {
            var obj = (AspNetCacheBlobContainer)HttpRuntime.Cache[CacheKey];
            if (obj == null)
            {
                HttpRuntime.Cache.Insert(CacheKey, this, null, Cache.NoAbsoluteExpiration, Provider.Sliding,
                                         CacheItemPriority.Default, null);
            }
        }

        internal void Refresh()
        {
            this.Found = ChainContainer.Found;

            this.ETag = ChainContainer.ETag;
            this.LastModified = ChainContainer.LastModified;
        }

        public override void Create()
        {
            ChainContainer.Create();
            Refresh();
        }

        public override void Delete()
        {
            ChainContainer.Delete();
            Refresh();
        }

        public override bool Exists
        {
            get
            {
                if (!Found.HasValue)
                    Found = ChainContainer.Exists;
                return Found.Value;
            }
        }
    }

    public sealed class AspNetCacheBlobSpecCollection : BlobSpecCollection<AspNetCacheBlobContainer, AspNetCacheBlobSpec>
    {
        private readonly IBlobSpecCollection _impl;

        #region .ctor

        public AspNetCacheBlobSpecCollection(AspNetCacheBlobContainer container)
            : base(container)
        {
            _impl = container.ChainContainer.Blobs;
        }

        #endregion

        public override IEnumerable<AspNetCacheBlobSpec> FindAll(BlobSelect select)
        {
            return (from spec in _impl.Find(@select)
                    select new AspNetCacheBlobSpec(Container, spec.Path));
        }
    }

    public sealed class AspNetCacheBlobSpec : BlobSpec<AspNetCacheProvider, AspNetCacheBlobContainer, AspNetCacheBlobSpecCollection>
    {
        public IBlobSpec ChainSpec { get; private set; }
        public string CacheKey { get; private set; }

        #region .ctor

        public AspNetCacheBlobSpec(AspNetCacheBlobContainer container, string path, IBlobSpec chainSpec)
            : base(container, path)
        {
            ChainSpec = chainSpec;
            CacheKey = container.CacheKey + "/" + path;
            RefreshFrom(ChainSpec);
        }

        public AspNetCacheBlobSpec(AspNetCacheBlobContainer container, string path)
            : this(container, path, container.ChainContainer.Blobs[path])
        {
        }

        #endregion

        internal class CacheObj
        {
            internal IBlobSpec Spec;
            internal string FilePath;
            internal byte[] Data;
        }

        private void RefreshFrom(IBlobSpec spec)
        {
            this.Found = spec.Found;

            this.ETag = spec.ETag;
            this.LastModified = spec.LastModified;

            this.ContentType = spec.ContentType;
            this.ContentLanguage = spec.ContentLanguage;
            this.ContentLength = spec.ContentLength;
            this.ContentEncoding = spec.ContentEncoding;
            this.ContentMD5 = spec.ContentMD5;

            this.MetaData = spec.MetaData.Clone();
        }

        private string GetCacheKey(BlobOptions options, out Stack<string> deps)
        {
            deps = new Stack<string>();
            Container.EnsureCache();
            deps.Push(Container.CacheKey);

            var sb = new StringBuilder(CacheKey);
            if (options.RangeStart.HasValue || options.RangeEnd.HasValue)
            {
                deps.Push(CacheKey);
                sb.Append('|');
                sb.Append(options.RangeStart.GetValueOrDefault(0));
                sb.Append('-');
                sb.Append(options.RangeEnd.GetValueOrDefault(0));
            }
            return sb.ToString();
        }

        private void UpdateCache(string cacheKey, CacheObj obj, Stack<string> deps)
        {
            var dep = new CacheDependency(obj.FilePath == null
                                              ? null
                                              : new[] { obj.FilePath }, deps.ToArray());
            HttpRuntime.Cache.Insert(cacheKey, obj, dep, Cache.NoAbsoluteExpiration, Container.Provider.Sliding,
                                     CacheItemPriority.Default, null);
        }

        public override Stream Read(BlobOptions options)
        {
            Stack<string> deps;
            var cacheKey = GetCacheKey(options, out deps);
            var obj = (CacheObj)HttpRuntime.Cache[cacheKey];
            if (obj == null)
                using (var stream = ChainSpec.Read(options))
                {
                    RefreshFrom(ChainSpec);
                    var fs = stream as FileStream;
                    obj = new CacheObj
                              {
                                  Spec = this,
                                  Data = (stream == null) ? null : stream.ToArray()
                              };
                    if (fs != null)
                        obj.FilePath = fs.Name;
                    UpdateCache(cacheKey, obj, deps);
                }
            else
                RefreshFrom(obj.Spec);
            return obj.Data == null
                       ? null
                       : new MemoryStream(obj.Data);
        }

        public override void Write(Stream stream, BlobOptions options)
        {
            Stack<string> deps;
            var cacheKey = GetCacheKey(options, out deps);
            var obj = (CacheObj) HttpRuntime.Cache[cacheKey];
            obj = obj != null ? new CacheObj {Spec = this, FilePath = obj.FilePath} : new CacheObj {Spec = this};
            HttpRuntime.Cache.Remove(cacheKey);

            obj.Data = stream.ToArray();
            stream.Position = 0;

            ChainSpec.ETag = this.ETag;
            ChainSpec.LastModified = this.LastModified;
            ChainSpec.ContentType = this.ContentType;
            ChainSpec.ContentLanguage = this.ContentLanguage;
            ChainSpec.ContentLength = this.ContentLength;
            ChainSpec.ContentEncoding = this.ContentEncoding;
            ChainSpec.ContentMD5 = this.ContentMD5;
            ChainSpec.MetaData = this.MetaData.Clone();

            ChainSpec.Write(stream, options);

            RefreshFrom(ChainSpec);
            UpdateCache(cacheKey, obj, deps);
        }

        public override void Delete()
        {
            ChainSpec.Delete();
            Stack<string> deps;
            var cacheKey = GetCacheKey(BlobOptions.Default, out deps);
            HttpRuntime.Cache.Remove(cacheKey);
            Found = false;
        }
    }
}