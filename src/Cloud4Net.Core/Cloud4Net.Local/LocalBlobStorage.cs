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

using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Trinet.Core.IO.Ntfs;

namespace System.StorageModel.Local
{
    using Configuration;

    partial class FileSystemProvider : IBlobProvider
    {
        public DirectoryInfo RootDirectory { get; private set; }
        public FileSystemBlobContainerCollection Containers { get; private set; }

        private static string _appPath;

        #region .ctor

        partial void InitializeBlobs(NameValueCollection config)
        {
            var path = config["BlobPath"] ?? Environment.CurrentDirectory;

            if (path.StartsWith("~/"))
            {
                if (_appPath == null)
                {
                    var httpRuntimeType = Type.GetType("System.Web.HttpRuntime, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", true);
                    var appPathProperty = httpRuntimeType.GetProperty("AppDomainAppPath");
                    _appPath = (string) appPathProperty.GetValue(null, null);
                }
                path = Path.Combine(_appPath, path.Substring(2));
            }

            RootDirectory = new DirectoryInfo(path);
            if (!RootDirectory.Exists)
                RootDirectory.Create();
            Containers = new FileSystemBlobContainerCollection(this);
            BlobStorage.Initialize();
        }

        #endregion

        #region IBlobProvider

        IBlobContainerCollection IBlobProvider.Containers
        {
            get { return this.Containers; }
        }

        IBlobContainer IBlobProvider.NewContainer(string name, StorageResourceDefinition config)
        {
            return new FileSystemBlobContainer(this, name, config);
        }

        IBlobSpec IBlobProvider.NewSpec(IBlobContainer container, string path, StorageResourceDefinition config)
        {
            var fsContainer = (FileSystemBlobContainer)container;
            return new FileSystemBlobSpec(fsContainer, path, config);
        }

        public string NormalizeContainerName(string name)
        {
            return name;
        }

        public string NormalizeBlobPath(string path)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        public bool Copy(IBlobSpec spec1, IBlobSpec target, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            return false;
        }

        #endregion
    }

    public sealed class FileSystemBlobContainerCollection : BlobContainerCollection<FileSystemProvider, FileSystemBlobContainer>
    {
        #region .ctor

        internal FileSystemBlobContainerCollection(FileSystemProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<FileSystemBlobContainer> FindAll()
        {
            Provider.OnListingContainers();
            using (Provider.LogBlobRequests(null))
                foreach (var subdir in Provider.RootDirectory.GetDirectories())
                {
                    if (subdir.Name.Contains("."))
                        continue;
                    yield return new FileSystemBlobContainer(Provider, subdir);
                }
        }
    }

    public sealed class FileSystemBlobContainer : BlobContainer<FileSystemProvider, FileSystemBlobSpecCollection>
    {
        public DirectoryInfo Directory { get; private set; }

        #region .ctor

        internal FileSystemBlobContainer(FileSystemProvider provider, string name, StorageResourceDefinition config)
            : this(provider, new DirectoryInfo(Path.Combine(provider.RootDirectory.FullName, name)))
        {
        }

        internal FileSystemBlobContainer(FileSystemProvider provider, DirectoryInfo dir)
            : base(provider, dir.Name)
        {
            Directory = dir;
            this.ReadMeta(false);
            this.Blobs = new FileSystemBlobSpecCollection(this);
        }

        #endregion

        void ReadMeta(bool readHeaders)
        {
            if (Directory.Exists)
            {
                this.Found = true;
                if (readHeaders)
                {
                    var lastModified = (string)null;
                    Directory.Read("headers", (name, value) =>
                    {
                        switch (name)
                        {
                            case Headers.ETag:
                                this.ETag = value;
                                break;
                            case Headers.LastModified:
                                lastModified = value;
                                break;
                        }
                    });
                    this.ParseLastModified(lastModified);
                }
            }
            else
                this.Found = false;
        }

        void WriteMeta(bool writeHeaders)
        {
            if (!writeHeaders) return;
            var headers = new NameValueCollection();
            if (ETag != null)
                headers.Add(Headers.ETag, ETag);
            if (LastModified.HasValue)
                headers.Add(Headers.LastModified, LastModified.Value.ToString("r"));
            Directory.Write("headers", headers);
        }

        public override bool Exists
        {
            get
            {
                if (!Found.HasValue)
                {
                    using (this.LogBlobRequests())
                        Directory.Refresh();
                    Found = Directory.Exists;
                    this.OnContainerExists(Found.Value);
                }
                return Found.Value;
            }
        }

        public override void Create()
        {
            Directory.Refresh();
            if (Directory.Exists)
                throw new ContainerAlreadyExistsException(this, null);
            using (this.LogBlobRequests())
                Directory = Provider.RootDirectory.CreateSubdirectory(Name);
            this.ReadMeta(false);
            this.OnContainerCreated();
        }

        public override void Delete()
        {
            Directory.Refresh();
            if (!Directory.Exists)
                throw new ContainerDoesNotExistsException(this, new DirectoryNotFoundException());
            using (this.LogBlobRequests())
                Directory.Delete(true);
            Found = false;
            this.OnContainerDeleted();
        }
    }

    public sealed class FileSystemBlobSpecCollection : BlobSpecCollection<FileSystemBlobContainer, FileSystemBlobSpec>
    {
        #region .ctor

        internal FileSystemBlobSpecCollection(FileSystemBlobContainer container)
            : base(container)
        {
        }

        #endregion

        public override IEnumerable<FileSystemBlobSpec> FindAll(BlobSelect select)
        {
            Container.OnListingBlobs();
            var di = Container.Directory;
            var take = select.Take.GetValueOrDefault(int.MaxValue);
            var basePath = di.FullName;
            var basePathLen = basePath.Length;
            if (basePath[basePathLen - 1] != Path.DirectorySeparatorChar)
                basePathLen++;
            var prefix = (select.Prefix == null) ? null : Container.Provider.NormalizeBlobPath(select.Prefix);
            foreach (var file in di.GetFiles("*.*", SearchOption.AllDirectories))
            {
                if (take-- == 0)
                    yield break;
                var path = Container.Provider.NormalizeBlobPath(file.FullName.Substring(basePathLen));
                if (prefix != null && !path.StartsWith(prefix))
                    continue;
                yield return new FileSystemBlobSpec(Container, path, file);
            }
        }
    }

    public sealed class FileSystemBlobSpec : BlobSpec<FileSystemProvider, FileSystemBlobContainer, FileSystemBlobSpecCollection>
    {
        public FileInfo File { get; private set; }

        #region .ctor

        internal FileSystemBlobSpec(FileSystemBlobContainer container, string path, StorageResourceDefinition config)
            : this(container, path, new FileInfo(IO.Path.Combine(container.Directory.FullName, path)))
        {
        }

        internal FileSystemBlobSpec(FileSystemBlobContainer container, string path, FileInfo file)
            : base(container, path)
        {
            this.File = file;
            this.ReadMeta(true, true);
        }

        #endregion

        void ReadMeta(bool readHeaders, bool readMetaData)
        {
            if (File.Exists)
            {
                this.Found = true;
                if (readHeaders)
                {
                    var lastModified = (string)null;
                    var contentLength = (string)null;
                    File.Read("headers", (name, value) =>
                    {
                        switch (name)
                        {
                            case Headers.ETag:
                                this.ETag = value;
                                break;
                            case Headers.LastModified:
                                lastModified = value;
                                break;
                            case Headers.ContentType:
                                this.ContentType = value;
                                break;
                            case Headers.ContentLanguage:
                                this.ContentLanguage = value;
                                break;
                            case Headers.ContentLength:
                                contentLength = value;
                                break;
                            case Headers.ContentEncoding:
                                this.ContentEncoding = value;
                                break;
                            case Headers.ContentMD5:
                                this.ContentMD5 = value;
                                break;
                        }
                    });
                    this.ParseLastModified(lastModified);
                    this.ParseContentLength(contentLength);
                }

                if (readMetaData)
                    File.Read("meta", (name, value) => this.MetaData.Add(name, value));
            }
            else
                this.Found = false;
        }

        void WriteMeta(bool writeHeaders, bool writeMetadata)
        {
            if (writeHeaders)
            {
                var headers = new NameValueCollection();
                if (this.ETag != null)
                    headers.Add(Headers.ETag, this.ETag);
                if (this.LastModified.HasValue)
                    headers.Add(Headers.LastModified, this.LastModified.Value.ToString("r"));
                if (this.ContentType != null)
                    headers.Add(Headers.ContentType, this.ContentType);
                if (this.ContentLanguage != null)
                    headers.Add(Headers.ContentLanguage, this.ContentLanguage);
                if (this.ContentLength.HasValue)
                    headers.Add(Headers.ContentLength, this.ContentLength.Value.ToString());
                if (this.ContentEncoding != null)
                    headers.Add(Headers.ContentEncoding, this.ContentEncoding);
                if (this.ContentMD5 != null)
                    headers.Add(Headers.ContentMD5, this.ContentMD5);
                File.Write("headers", headers);
            }
            if (writeMetadata)
                File.Write("meta", this.MetaData);
        }

        public override Stream Read(BlobOptions options)
        {
            this.ReadMeta(true, options.IncludeMetadata);
            if (!options.IncludeData || !Found.Value)
                return null;

            try
            {
                using (var fs = File.OpenRead())
                {
                    if (options.RangeStart.HasValue)
                        fs.Position = options.RangeStart.Value;
                    var length = options.RangeEnd.HasValue
                                     ? options.RangeEnd.Value - fs.Position
                                     : (long?) null;
                    var ms = length.HasValue
                                 ? new MemoryStream((int) Math.Min(length.Value, int.MaxValue))
                                 : new MemoryStream();
                    try
                    {
                        using (this.LogBlobRequests())
                            fs.CopyTo(ms, length);
                        ms.Position = 0;
                        this.OnBlobRead();
                        return ms;
                    }
                    catch
                    {
                        ms.Dispose();
                        throw;
                    }
                }
            }
            catch (FileNotFoundException inner)
            {
                throw new BlobDoesNotExistsException(this, inner);
            }
        }

        public override void Write(Stream stream, BlobOptions options)
        {
            if (options.IncludeData)
            {
                var di = File.Directory;
                if (!di.Exists)
                    di.Create();
                using (var fs = File.Create())
                {
                    using (this.LogBlobRequests())
                        stream.CopyTo(fs, null);
                }
                File.Refresh();
            }
            else
                File.LastWriteTimeUtc = DateTime.UtcNow;

            this.LastModified = File.LastWriteTimeUtc;
            this.ETag = this.LastModified.Value.Ticks.ToString();
            this.WriteMeta(true, options.IncludeMetadata);
            this.OnBlobWrite();
        }

        public override void Delete()
        {
            using (this.LogBlobRequests())
                File.Delete();
            Found = false;
            this.OnBlobDeleted();
        }
    }

    internal static class Headers
    {
        public const string ETag = "ETag";
        public const string LastModified = "Last-Modified";
        public const string ContentType = "Content-Type";
        public const string ContentLanguage = "Content-Language";
        public const string ContentLength = "Content-Length";
        public const string ContentEncoding = "Content-Encoding";
        public const string ContentMD5 = "Content-MD5";
    }

    public static class FileSystemExtensions
    {
        internal static void Read(this FileSystemInfo info, string streamName, Action<string, string> setPair)
        {
            var stream = info.GetAlternateDataStream(streamName, FileMode.OpenOrCreate);
            if (stream.Exists)
                using (var reader = stream.OpenText())
                {
                    while (true)
                    {
                        var line = reader.ReadLine();
                        if (line == null)
                            break;
                        var parts = line.Split(new[] { '=' }, 2);
                        var name = parts[0];
                        var value = parts[1];
                        setPair(name, value);
                    }
                }
        }

        internal static void Write(this FileSystemInfo info, string streamName, NameValueCollection pairs)
        {
            if (pairs==null)
                return;
            var mdFile = info.GetAlternateDataStream(streamName, FileMode.Create);
            using (var fs = mdFile.OpenWrite())
            {
                using (var writer = new StreamWriter(fs))
                {
                    foreach (string name in pairs)
                    {
                        writer.Write(name);
                        writer.Write('=');
                        writer.WriteLine(pairs[name]);
                    }
                }
            }
        }
    }
}