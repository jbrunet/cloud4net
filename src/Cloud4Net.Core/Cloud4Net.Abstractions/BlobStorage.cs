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
using System.Configuration;
using System.Diagnostics;
using System.Diagnostics.IntelliTrace;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.StorageModel.Diagnostics;
using System.Text;
using System.Xml;

namespace System.StorageModel
{
    #region Configuration

    using Configuration;

    namespace Configuration
    {
        public sealed class BlobStorageSection : StorageSection
        {
            private const string ContainersProperty = "";
            [ConfigurationProperty(ContainersProperty, IsDefaultCollection = true, IsRequired = true)]
            [ConfigurationCollection(typeof(StorageResourceDefinition), AddItemName = "container")]
            public StorageResourceDefinitionCollection Containers
            {
                get { return (StorageResourceDefinitionCollection)this[ContainersProperty]; }
            }
        }
    }

    #endregion

    #region Interfaces

    public interface IBlobProvider : IStorageService
    {
        IBlobContainer NewContainer(string name, StorageResourceDefinition config);
        IBlobSpec NewSpec(IBlobContainer container, string path, StorageResourceDefinition config);
        IBlobContainerCollection Containers { get; }
        string NormalizeContainerName(string name);
        string NormalizeBlobPath(string path);
        bool Copy(IBlobSpec source, IBlobSpec target, BlobOptions options, BlobCopyConditionDelegate condition);
    }

    public interface IBlobResource
    {
        bool? Found { get; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        bool Exists { get; }

        string ETag { get; set; }
        DateTime? LastModified { get; set; }
    }

    public interface IBlobContainer : IBlobResource
    {
        string Name { get; }
        IBlobProvider Provider { get; }
        IBlobSpecCollection Blobs { get; }
        void Create();
        void CreateIfNotExists();
        void Delete();
        void DeleteIfExists();
    }

    public interface IBlobContainerCollection : IEnumerable<IBlobContainer>
    {
        IEnumerable<IBlobContainer> FindAll();
        IBlobContainer this[string name] { get; set; }
    }

    public interface IBlobSpec : IBlobResource
    {
        IBlobContainer Container { get; }
        string Path { get; }
        string ContentType { get; set; }
        string ContentLanguage { get; set; }
        long? ContentLength { get; set; }
        string ContentEncoding { get; set; }
        string ContentMD5 { get; set; }

        NameValueCollection MetaData { get; set; }
        void WriteMetaData();

        Stream Read(BlobOptions options);
        void Write(Stream stream, BlobOptions options);
        void Delete();
        void DeleteIfExists();
    }

    public interface IBlobSpecCollection : IEnumerable<IBlobSpec>
    {
        IEnumerable<IBlobSpec> Find(BlobSelect select);
        IBlobSpec this[string path] { get; }
    }

    #endregion

    #region Exceptions

    public abstract class ContainerException : Exception
    {
        public IBlobContainer Container { get; private set; }

        #region .ctor

        protected ContainerException(IBlobContainer container, string format, Exception innerException)
            : base(string.Format(format, container.Name), innerException)
        {
        }

        #endregion
    }

    public class ContainerAlreadyExistsException : ContainerException
    {
        #region .ctor

        public ContainerAlreadyExistsException(IBlobContainer container, Exception innerException)
            : base(container, "Container '{0}' already exists", innerException)
        {
        }

        #endregion
    }

    public class ContainerDoesNotExistsException : ContainerException
    {
        #region .ctor

        public ContainerDoesNotExistsException(IBlobContainer container, Exception innerException)
            : base(container, "Container '{0}' does not exists", innerException)
        {
        }

        #endregion
    }

    public abstract class BlobException : Exception
    {
        public IBlobSpec Spec { get; private set; }

        #region .ctor

        protected BlobException(IBlobSpec spec, string format, Exception innerException)
            : base(string.Format(format, spec.Path), innerException)
        {
        }

        #endregion
    }

    public class BlobDoesNotExistsException : BlobException
    {
        #region .ctor

        public BlobDoesNotExistsException(IBlobSpec spec, Exception innerException)
            : base(spec, "Blob '{0}' does not exists", innerException)
        {
        }

        #endregion
    }

    public class BlobAlreadyExistsException : BlobException
    {
        #region .ctor

        public BlobAlreadyExistsException(IBlobSpec spec, Exception innerException)
            : base(spec, "Blob '{0}' already exists", innerException)
        {
        }

        #endregion
    }

    #endregion

    #region Base classes

    public abstract class BlobResource
    {
        public bool? Found { get; protected set; }
        public string ETag { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public abstract class BlobContainer<TProvider, TBlobSpecCollection> : BlobResource, IBlobContainer
        where TProvider : IBlobProvider
        where TBlobSpecCollection : IBlobSpecCollection
    {
        #region .ctor

        protected BlobContainer(TProvider provider, string name)
        {
            this.Provider = provider;
            this.Name = name;
        }

        #endregion

        public override string ToString()
        {
            return Name;
        }

        public TProvider Provider { get; protected set; }
        public string Name { get; protected set; }
        public TBlobSpecCollection Blobs { get; protected set; }

        public abstract void Create();
        public virtual void CreateIfNotExists()
        {
            if (!Exists)
                Create();
        }

        public abstract void Delete();
        public virtual void DeleteIfExists()
        {
            if (Exists)
                Delete();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public abstract bool Exists { get; }

        #region IBlobContainer

        IBlobProvider IBlobContainer.Provider
        {
            get { return this.Provider; }
        }

        IBlobSpecCollection IBlobContainer.Blobs
        {
            get { return this.Blobs; }
        }

        #endregion
    }

    public abstract class BlobContainerCollection<TProvider, TContainer> : Dictionary<string, TContainer>, IBlobContainerCollection
        where TProvider : IBlobProvider
        where TContainer : IBlobContainer
    {
        #region .ctor

        protected BlobContainerCollection(TProvider provider)
        {
            this.Provider = provider;
        }

        #endregion

        public TProvider Provider { get; set; }

        public abstract IEnumerable<TContainer> FindAll();

        public new IEnumerator<TContainer> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public new TContainer this[string name]
        {
            get
            {
                TContainer container;
                if (!TryGetValue(name, out container))
                    container = (TContainer)Provider.NewContainer(name, null);
                return container;
            }
            set { base[name] = value; }
        }

        #region IBlobContainerCollection

        IEnumerable<IBlobContainer> IBlobContainerCollection.FindAll()
        {
            return this.FindAll().Cast<IBlobContainer>();
        }

        IEnumerator<IBlobContainer> IEnumerable<IBlobContainer>.GetEnumerator()
        {
            return new Enumerator<TContainer, IBlobContainer>(Values);
        }

        IBlobContainer IBlobContainerCollection.this[string name]
        {
            get { return this[name]; }
            set { this[name] = (TContainer)value; }
        }

        #endregion
    }

    public abstract class BlobSpec<TProvider, TContainer, TBlobSpecCollection> : BlobResource, IBlobSpec
        where TProvider : IBlobProvider
        where TContainer : BlobContainer<TProvider, TBlobSpecCollection>
        where TBlobSpecCollection : IBlobSpecCollection
    {
        #region .ctor

        protected BlobSpec(TContainer container, string path)
        {
            this.Container = container;
            this.Path = path;
        }

        #endregion

        public override string ToString()
        {
            return Path;
        }

        public TContainer Container { get; protected set; }
        public string Path { get; protected set; }
        public string ContentType { get; set; }
        public string ContentLanguage { get; set; }
        public long? ContentLength { get; set; }
        public string ContentEncoding { get; set; }
        public string ContentMD5 { get; set; }

        public NameValueCollection MetaData { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public virtual bool Exists
        {
            get
            {
                if (!Found.HasValue)
                    Read(BlobOptions.MetaDataOnly);
                return Found.Value;
            }
        }

        public abstract Stream Read(BlobOptions options);
        public abstract void Write(Stream stream, BlobOptions options);
        public abstract void Delete();

        public virtual void DeleteIfExists()
        {
            if (Exists)
                Delete();
        }

        public virtual void WriteMetaData()
        {
            Write(null, BlobOptions.MetaDataOnly);
        }

        #region IBlobSpec

        IBlobContainer IBlobSpec.Container
        {
            get { return this.Container; }
        }

        #endregion
    }

    public abstract class BlobSpecCollection<TContainer, TSpec> : Dictionary<string, TSpec>, IBlobSpecCollection
        where TContainer : IBlobContainer
        where TSpec : IBlobSpec
    {
        #region .ctor

        protected BlobSpecCollection(TContainer container)
        {
            this.Container = container;
        }

        #endregion

        public TContainer Container { get; protected set; }

        public abstract IEnumerable<TSpec> FindAll(BlobSelect select);

        public new IEnumerator<TSpec> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public new TSpec this[string path]
        {
            get
            {
                TSpec spec;
                if (!TryGetValue(path, out spec))
                    spec = (TSpec)Container.Provider.NewSpec(Container, path, null);
                return spec;
            }
        }

        #region IBlobSpecCollection Members

        IEnumerator<IBlobSpec> IEnumerable<IBlobSpec>.GetEnumerator()
        {
            return new Enumerator<TSpec, IBlobSpec>(Values);
        }

        IEnumerable<IBlobSpec> IBlobSpecCollection.Find(BlobSelect select)
        {
            return this.FindAll(select).Cast<IBlobSpec>();
        }

        IBlobSpec IBlobSpecCollection.this[string path]
        {
            get { return this[path]; }
        }

        #endregion
    }

    public enum BlobCondition
    {
        None,
        IfMatch,
        IfNoneMatch,
        IfModifiedSince,
        IfNotModifiedSince,
    }

    public class BlobOptions : ICloneable
    {
        public bool IncludeMetadata { get; set; }
        public bool IncludeData { get; set; }
        public BlobCondition Condition { get; set; }
        public long? RangeStart { get; set; }
        public long? RangeEnd { get; set; }

        public static readonly BlobOptions Default = new BlobOptions
                                                         {
                                                             IncludeMetadata = true,
                                                             IncludeData = true,
                                                         };

        public static readonly BlobOptions MetaDataOnly = new BlobOptions
                                                              {
                                                                  IncludeMetadata = true
                                                              };

        public BlobOptions Clone()
        {
            return (BlobOptions)this.MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return this.Clone();
        }
    }

    public class BlobSelect
    {
        public string Prefix { get; set; }
        public string Marker { get; set; }
        public int? Take { get; set; }
        public string Delimiter { get; set; }

        public static readonly BlobSelect All = new BlobSelect();
    }

    #endregion

    #region Diagnostics

    namespace Diagnostics
    {
        public class BlobRequestLog : WebRequestLog
        {
            public IBlobContainer BlobContainer { get; set; }
            public IBlobSpec BlobSpec { get; set; }
        }
    }

    partial class BlobStorage
    {
        public static TraceSource Trace { get; private set; }

        private const int ProviderNameMaxSize = 32;
        private const int ContainerNameMaxSize = 32;
        private const int PathMaxSize = 256;

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobContainerList", "Listing blob containers")]
        [Binding("{0}: List containers")]
        public static void OnListingContainers(
            [DataQuery(Name = "Provider", Query = "Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            this IBlobProvider provider)
        {
            Trace.TraceInformation("{0}: LIST containers", provider.Name);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobContainerExists", "blob container exists ?")]
        [Binding("{0}: {1} container exists={2}")]
        public static void OnContainerExists(
            [DataQuery(Name = "Provider", Query = "Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            this IBlobContainer container,
            [DataQuery]
            bool found)
        {
            Trace.TraceInformation("{0}: container={1} EXISTS={2} -> Etag={3} Last-Modified={4} T={5}ms",
                                   container.Provider.Name,
                                   container.Name, found, container.ETag, container.LastModified,
                                   Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobContainerCreated", "blob container created")]
        [Binding("{0}: {1} container created")]
        public static void OnContainerCreated(
            [DataQuery(Name = "Provider", Query = "Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            this IBlobContainer container)
        {
            Trace.TraceInformation("{0}: container={1} CREATED -> Etag={2} Last-Modified={3} T={4}ms", container.Provider.Name, container.Name, container.ETag, container.LastModified, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobContainerDeleted", "blob container deleted")]
        [Binding("{0}: {1} container deleted")]
        public static void OnContainerDeleted(
            [DataQuery(Name = "Provider", Query = "Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            this IBlobContainer container)
        {
            Trace.TraceInformation("{0}: container={1} DELETED -> T={2}ms", container.Provider.Name, container.Name, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobList", "container's blobs listing was requested")]
        [Binding("{0}: {1} container list blobs")]
        public static void OnListingBlobs(
            [DataQuery(Name = "Provider", Query = "Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            this IBlobContainer container)
        {
            Trace.TraceInformation("{0}: container={1} LIST blobs", container.Provider.Name, container.Name);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobExists", "does this blob exist ?")]
        [Binding("{0}: {1}/{2} exists={3}")]
        public static void OnBlobExists(
            [DataQuery(Name = "Provider", Query = "Container.Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Container.Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            [DataQuery(Name = "Path", Query = "Path", Type = typeof(string), MaxSize = PathMaxSize)]
            this IBlobSpec spec,
            [DataQuery]
            bool found)
        {
            Trace.TraceInformation("{0}: container={1} blob={2} EXISTS={3} -> Etag={4} Last-Modified={5} T={6}ms",
                                   spec.Container.Provider.Name,
                                   spec.Container.Name, spec.Path, spec.ETag, spec.LastModified,
                                   Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobCreated", "A blob was created")]
        [Binding("{0}: {1}/{2} was created")]
        public static void OnBlobCreated(
            [DataQuery(Name = "Provider", Query = "Container.Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Container.Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            [DataQuery(Name = "Path", Query = "Path", Type = typeof(string), MaxSize = PathMaxSize)]
            this IBlobSpec spec)
        {
            Trace.TraceInformation("{0}: container={1} blob={2} CREATED -> Etag={3} Last-Modified={4} T={5}ms", spec.Container.Provider.Name,
                                    spec.Container.Name, spec.Path, spec.ETag, spec.LastModified, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobDeleted", "A blob was deleted")]
        [Binding("{0}: {1}/{2} was deleted")]
        public static void OnBlobDeleted(
            [DataQuery(Name = "Provider", Query = "Container.Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Container.Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            [DataQuery(Name = "Path", Query = "Path", Type = typeof(string), MaxSize = PathMaxSize)]
            this IBlobSpec spec)
        {
            Trace.TraceInformation("{0}: container={1} blob={2} DELETED -> T={3}ms", spec.Container.Provider.Name,
                                    spec.Container.Name, spec.Path, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        [Conditional("TRACE")]
        [DiagnosticEvent("BlobRead", "A blob was read")]
        [Binding("{0}: {1}/{2}  was read")]
        public static void OnBlobRead(
            [DataQuery(Name = "Provider", Query = "Container.Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Container.Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            [DataQuery(Name = "Path", Query = "Path", Type = typeof(string), MaxSize = PathMaxSize)]
            this IBlobSpec spec)
        {
            Trace.TraceInformation("{0}: container={1} blob={2} READ -> Etag={3} Last-Modified={4} T={5}ms", spec.Container.Provider.Name,
                                    spec.Container.Name, spec.Path, spec.ETag, spec.LastModified, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }


        [Conditional("TRACE")]
        [DiagnosticEvent("BlobWrite", "A blob was written")]
        [Binding("{0}: {1}/{2} was written")]
        public static void OnBlobWrite(
            [DataQuery(Name = "Provider", Query = "Container.Provider.Name", Type = typeof(string), MaxSize = ProviderNameMaxSize)]
            [DataQuery(Name = "Container", Query = "Container.Name", Type = typeof(string), MaxSize = ContainerNameMaxSize)]
            [DataQuery(Name = "Path", Query = "Path", Type = typeof(string), MaxSize = PathMaxSize)]
            this IBlobSpec spec)
        {
            Trace.TraceInformation("{0}: container={1} blob={2} WRITE -> Etag={3} Last-Modified={4} T={5}ms", spec.Container.Provider.Name,
                                    spec.Container.Name, spec.Path, spec.ETag, spec.LastModified, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }
    }

    #endregion

    public delegate bool? BlobCopyConditionDelegate(IBlobSpec source, IBlobSpec target);

    public static partial class BlobStorage
    {
        internal class BlobContainerCollection : BlobContainerCollection<IBlobProvider, IBlobContainer>
        {
            #region .ctor

            internal BlobContainerCollection()
                : base(null)
            {
            }

            #endregion

            public override IEnumerable<IBlobContainer> FindAll()
            {
                throw new NotSupportedException();
            }
        }

        private static bool _initialized;
        private static BlobStorageSection _configurationSection;
        private static BlobContainerCollection _containers;

        public static void Initialize()
        {
            if (_initialized)
                return;
            lock (typeof(BlobStorage))
            {
                if (_initialized)
                    return;

                Trace = new TraceEventSource(typeof(BlobStorage), SourceLevels.All, 0);

                _configurationSection = StorageSection.Load<BlobStorageSection>("system.storageModel/blobs");
                _containers = new BlobContainerCollection();

                _initialized = true;

                var defaultProvider = string.IsNullOrEmpty(ConfigurationSection.DefaultProvider)
                                          ? null
                                          : GetProvider(ConfigurationSection.DefaultProvider);
                _containers.Provider = defaultProvider;
                foreach (StorageResourceDefinition containerDef in ConfigurationSection.Containers)
                {
                    var provider = string.IsNullOrEmpty(containerDef.Provider)
                                       ? defaultProvider
                                       : GetProvider(containerDef.Provider);
                    if (provider == null)
                        throw new ConfigurationErrorsException("At least a default blob provider must be defined");
                    var container = provider.NewContainer(containerDef.Name, containerDef);
                    container.CreateIfNotExists();
                    Containers[containerDef.Name] = container;
                    provider.Containers[containerDef.Name] = container;
                }
            }
        }

        public static BlobStorageSection ConfigurationSection
        {
            get
            {
                Initialize();
                return _configurationSection;
            }
        }

        public static IBlobContainerCollection Containers
        {
            get
            {
                Initialize();
                return _containers;
            }
        }

        #region Extension Methods

        public static void ParseLastModified(this IBlobResource resource, string lastModified)
        {
            DateTime dt;
            resource.LastModified = DateTime.TryParseExact(lastModified, "r", DateTimeFormatInfo.InvariantInfo,
                                                           DateTimeStyles.None,
                                                           out dt)
                                        ? dt
                                        : (DateTime?)null;
        }

        public static void Parse(this IBlobResource resource, WebHeaderCollection headers)
        {
            // LastModified
            ParseLastModified(resource, headers[HttpResponseHeader.LastModified]);
            // ETag
            resource.ETag = headers[HttpResponseHeader.ETag];
        }

        public static void ParseContentLength(this IBlobSpec spec, string contentLength)
        {
            long l;
            spec.ContentLength = long.TryParse(contentLength, out l)
                                     ? l
                                     : (long?)null;
        }

        public static void Parse(this IBlobSpec spec, WebHeaderCollection headers)
        {
            // LastModified+ETag
            Parse((IBlobResource)spec, headers);
            // ContentLength
            ParseContentLength(spec, headers[HttpResponseHeader.ContentLength]);

            spec.ContentType = headers[HttpResponseHeader.ContentType];
            spec.ContentLanguage = headers[HttpResponseHeader.ContentLanguage];
            spec.ContentEncoding = headers[HttpResponseHeader.ContentEncoding];
            spec.ContentMD5 = headers[HttpResponseHeader.ContentMd5];
        }

        public static void Apply(this IBlobSpec spec, BlobOptions options, WebHeaderCollection headers)
        {
            switch (options.Condition)
            {
                case BlobCondition.IfMatch:
                    headers[HttpRequestHeader.IfMatch] = spec.ETag;
                    break;
                case BlobCondition.IfNoneMatch:
                    headers[HttpRequestHeader.IfNoneMatch] = spec.ETag;
                    break;
                case BlobCondition.IfModifiedSince:
                    headers[HttpRequestHeader.IfModifiedSince] = spec.LastModified.Value.ToString("r");
                    break;
                case BlobCondition.IfNotModifiedSince:
                    headers[HttpRequestHeader.IfUnmodifiedSince] = spec.LastModified.Value.ToString("r");
                    break;
            }
            if (spec.ContentEncoding != null)
                headers[HttpRequestHeader.ContentEncoding] = spec.ContentEncoding;
            if (spec.ContentLanguage != null)
                headers[HttpRequestHeader.ContentLanguage] = spec.ContentLanguage;
        }

        public static void Edit(this IBlobSpec spec
            , Func<Stream, Stream> modify
            , Predicate<Exception> retry)
        {
            var options = new BlobOptions
                              {
                                  IncludeData = true,
                                  IncludeMetadata = true,
                              };
            while (true)
            {
                options.Condition = BlobCondition.None;
                using (var stream = spec.Read(options))
                {
                    var stream2 = modify(stream);
                    if (stream2 == null)
                        return;
                    using (stream2)
                    {
                        options.Condition = BlobCondition.IfNotModifiedSince;
                        try
                        {
                            spec.Write(stream2, options);
                            return;
                        }
                        catch (Exception error)
                        {
                            if (!retry(error))
                                throw;
                        }
                    }
                }
            }
        }

        public static Stream Read(this IBlobSpec spec)
        {
            return spec.Read(BlobOptions.Default);
        }

        public static void Write(this IBlobSpec spec, Stream stream)
        {
            spec.Write(stream, BlobOptions.Default);
        }

        public static string ReadAsString(this IBlobSpec spec)
        {
            using (var stream = spec.Read())
                return Encoding.UTF8.GetString(stream.ToArray());
        }

        public static void WriteAsString(this IBlobSpec spec, string data)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                spec.Write(stream);
        }

        public static void WriteBinary(this IBlobSpec spec, Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    write(writer);
                    spec.Write(stream);
                }
            }
        }

        public static void WriteXml(this IBlobSpec spec, XmlDocument doc)
        {
            using (var stream = new MemoryStream())
            {
                doc.Save(stream);
                stream.Position = 0;
                spec.Write(stream);
            }
        }

        public static void WriteXml(this IBlobSpec spec, Action<XmlWriter> write)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
                {
                    writer.Formatting = Formatting.Indented;
                    writer.Indentation = 4;
                    writer.WriteStartDocument(true);
                    write(writer);
                    writer.WriteEndDocument();
                    writer.Flush();
                    stream.Position = 0;
                    spec.Write(stream);
                }
            }
        }

        public static void ReadXml(this IBlobSpec spec, Action<XmlDocument> read)
        {
            try
            {
                using (var stream = spec.Read())
                {
                    var doc = new XmlDocument();
                    doc.Load(stream);
                    read(doc);
                }
            }
            catch (BlobDoesNotExistsException)
            {
                read(null);
            }
        }

        public static T ReadXml<T>(this IBlobSpec spec, Func<XmlDocument, T> read)
        {
            try
            {
                using (var stream = spec.Read())
                {
                    var doc = new XmlDocument();
                    doc.Load(stream);
                    return read(doc);
                }
            }
            catch (BlobDoesNotExistsException)
            {
                return read(null);
            }
        }

        public static T Read<T>(this IBlobSpec spec, Func<StreamReader, T> read)
        {
            try
            {
                using (var stream = spec.Read())
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return read(reader);
                    }
                }
            }
            catch (BlobDoesNotExistsException)
            {
                return read(null);
            }
        }

        public static void EditXml(this IBlobSpec spec, Func<XmlDocument, XmlDocument> edit)
        {
            spec.Edit(stream =>
            {
                var doc = new XmlDocument();
                doc.Load(stream);
                doc = edit(doc);
                if (doc == null)
                    return null;
                stream = new MemoryStream();
                doc.Save(stream);
                return stream;
            }, error => false);
        }

        public static void CopyTo(this IBlobSpec spec1, IBlobSpec spec2, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            if (spec1.Container.Provider.Copy(spec1, spec2, options, condition))
                return;
            var shallCopy = condition(spec1, spec2);
            if (shallCopy.HasValue && !shallCopy.Value)
                return;
            using (var stream = spec1.Read(options))
            {
                if (!shallCopy.HasValue)
                {
                    shallCopy = condition(spec1, spec2);
                    if (shallCopy.GetValueOrDefault(false) == false)
                        return;
                }
                stream.Position = 0;
                spec2.MetaData = spec1.MetaData.Clone();
                spec2.ContentType = spec1.ContentType;
                spec2.ContentLanguage = spec1.ContentLanguage;
                spec2.ContentLength = spec1.ContentLength;
                //spec2.ETag = spec1.ETag;
                //spec2.LastModified = spec1.LastModified;
                //spec2.Headers = spec1.Headers.Clone();
                spec2.Write(stream, options);
            }
        }

        public static IBlobSpec CopyTo(this IBlobSpec spec1, IBlobProvider targetProvider, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            var spec2 = targetProvider
                .Containers[targetProvider.NormalizeContainerName(spec1.Container.Name)]
                .Blobs[targetProvider.NormalizeBlobPath(spec1.Path)];
            spec1.CopyTo(spec2, options, condition);
            return spec2;
        }

        public static BlobRequestLog LogBlobRequests(this IBlobProvider provider, Action<BlobRequestLog> create)
        {
            return provider.Log(create);
        }

        public static WebRequestLog LogBlobRequests(this IBlobContainer container)
        {
            return container.Provider.LogBlobRequests(log => log.BlobContainer = container);
        }

        public static WebRequestLog LogBlobRequests(this IBlobSpec spec)
        {
            return spec.Container.Provider.LogBlobRequests(log =>
            {
                log.BlobContainer = spec.Container;
                log.BlobSpec = spec;
            });
        }

        #endregion

        public static IBlobProvider GetProvider(string nameOrConnectionString)
        {
            return Storage.GetProvider<IBlobProvider>(nameOrConnectionString);
        }
    }

    public static class BlobCopyCondition
    {
        public static bool? Always(IBlobSpec source, IBlobSpec target)
        {
            return true;
        }

        public static bool? IfNewer(IBlobSpec source, IBlobSpec target)
        {
            if (!source.LastModified.HasValue)
                return null;
            if (!target.LastModified.HasValue)
                target.Read(BlobOptions.MetaDataOnly);
            return !target.Found.Value || target.LastModified.Value < source.LastModified.Value;
        }
    }
}