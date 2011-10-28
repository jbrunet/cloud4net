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
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.WindowsAzure.StorageClient;

namespace System.StorageModel.WindowsAzure
{
    using Configuration;

    partial class AzureProvider : IBlobProvider
    {
        public CloudBlobClient BlobClient { get; private set; }
        public AzureBlobContainerCollection Containers { get; private set; }

        #region .ctor

        partial void InitializeBlobs(NameValueCollection config)
        {
            BlobClient = Account.CreateCloudBlobClient();
            Containers = new AzureBlobContainerCollection(this);
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
            return new AzureBlobContainer(this, name, config);
        }

        IBlobSpec IBlobProvider.NewSpec(IBlobContainer container, string path, StorageResourceDefinition config)
        {
            return new AzureBlobSpec((AzureBlobContainer)container, path, config);
        }

        public string NormalizeContainerName(string name)
        {
            return name.ToLowerInvariant();
        }

        public string NormalizeBlobPath(string path)
        {
            return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        bool IBlobProvider.Copy(IBlobSpec spec1, IBlobSpec target, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            var aSpec1 = spec1 as AzureBlobSpec;
            var aSpec2 = target as AzureBlobSpec;
            if (aSpec1 == null || aSpec2 == null ||
                aSpec1.Container.Provider.AccountName != aSpec2.Container.Provider.AccountName)
                return false;
            using (this.LogBlobRequests(null))
                aSpec2.Impl.CopyFromBlob(aSpec1.Impl, aSpec2.ApplyOptions(options));
            return true;
        }

        #endregion
    }

    public sealed class AzureBlobContainerCollection : BlobContainerCollection<AzureProvider, AzureBlobContainer>
    {
        #region .ctor

        internal AzureBlobContainerCollection(AzureProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AzureBlobContainer> FindAll()
        {
            Provider.OnListingContainers();
            using (Provider.LogBlobRequests(null))
                foreach (var impl in Provider.BlobClient.ListContainers(string.Empty, ContainerListingDetails.All))
                {
                    yield return new AzureBlobContainer(Provider, impl, true);
                }
        }
    }

    public sealed class AzureBlobContainer : BlobContainer<AzureProvider, AzureBlobSpecCollection>
    {
        public CloudBlobContainer Impl { get; private set; }

        #region .ctor

        internal AzureBlobContainer(AzureProvider provider, string name, StorageResourceDefinition config)
            : this(provider, provider.BlobClient.GetContainerReference(name), null)
        {
        }

        internal AzureBlobContainer(AzureProvider provider, CloudBlobContainer impl, bool? found)
            : base(provider, impl.Name)
        {
            Impl = impl;
            this.Blobs = new AzureBlobSpecCollection(this);
            Refresh(found);
        }

        #endregion

        #region Internals

        private void Refresh(bool? found)
        {
            Found = found;
            Name = Impl.Name;
            MetaData = Impl.Attributes.Metadata.Clone();

            if (found.GetValueOrDefault(false))
            {
                var bp = Impl.Attributes.Properties;
                ETag = bp.ETag;
                LastModified = bp.LastModifiedUtc;
            }
        }

        internal string GetPath(CloudBlob blob)
        {
            var containerPath = Impl.Uri.PathAndQuery + '/';
            var blobPath = blob.Uri.PathAndQuery;
            return blobPath.Substring(containerPath.Length);
        }

        internal Exception TranslateException(StorageException ex)
        {
            switch (ex.ErrorCode)
            {
                case StorageErrorCode.ContainerAlreadyExists:
                    return new ContainerAlreadyExistsException(this, ex);
                case StorageErrorCode.ContainerNotFound:
                case StorageErrorCode.ResourceNotFound:
                    return new ContainerDoesNotExistsException(this, ex);
                default:
                    return ex;
            }
        }

        #endregion

        public override bool Exists
        {
            get
            {
                if (!Found.HasValue)
                {
                    try
                    {
                        using (this.LogBlobRequests())
                            Impl.FetchAttributes();
                        Refresh(true);
                    }
                    catch (StorageClientException ex)
                    {
                        var ex2 = TranslateException(ex);
                        if (ex2 is ContainerDoesNotExistsException)
                            Refresh(false);
                        else
                            throw ex2;
                    }
                }
                this.OnContainerExists(Found.Value);
                return Found.Value;
            }
        }

        public override void Create()
        {
            try
            {
                Impl.Metadata.ReplaceWith(MetaData);
                using (this.LogBlobRequests())
                    Impl.Create();
                Refresh(true);
                this.OnContainerCreated();
            }
            catch (StorageClientException ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void CreateIfNotExists()
        {
            try
            {
                Impl.Metadata.ReplaceWith(MetaData);
                using (this.LogBlobRequests())
                    if (!Impl.CreateIfNotExist())
                    {
                        if (!Found.GetValueOrDefault(false))
                            Impl.FetchAttributes(); // to sync LastModified/ETag (not handled by CreateINotExist)
                        this.OnContainerCreated();
                    }
                Refresh(true);
            }
            catch (StorageClientException ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void Delete()
        {
            try
            {
                using (this.LogBlobRequests())
                {
                    Impl.FetchAttributes(); // to ensure container exists (and to throw exception if not)
                    Impl.Delete();
                }
                Refresh(false);
                this.OnContainerDeleted();
            }
            catch (StorageClientException ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void DeleteIfExists()
        {
            try
            {
                using (this.LogBlobRequests())
                    Impl.Delete();
                this.OnContainerDeleted();
                Refresh(false);
            }
            catch (StorageClientException ex)
            {
                throw TranslateException(ex);
            }
        }

        // Azure-specific : Container MetaData

        public NameValueCollection MetaData { get; private set; }

        public void WriteMetaData()
        {
            try
            {
                Impl.Metadata.ReplaceWith(MetaData);
                using (this.LogBlobRequests())
                {
                    Impl.SetMetadata();
                    Impl.FetchAttributes();
                }
                Refresh(true);
            }
            catch (StorageClientException ex)
            {
                throw TranslateException(ex);
            }
        }
    }

    public sealed class AzureBlobSpecCollection : BlobSpecCollection<AzureBlobContainer, AzureBlobSpec>
    {
        #region .ctor

        internal AzureBlobSpecCollection(AzureBlobContainer container)
            : base(container)
        {

        }

        #endregion

        public override IEnumerable<AzureBlobSpec> FindAll(BlobSelect select)
        {
            Container.OnListingBlobs();
            var options = Container.ApplyOptions(BlobOptions.Default);
            options.UseFlatBlobListing = true;
            using (Container.LogBlobRequests())
                foreach (var item in Container.Impl.ListBlobs(options))
                {
                    var blob = item as CloudBlob;
                    if (blob == null)
                        continue;
                    var path = blob.Uri.ToString().Substring(Container.Impl.Uri.ToString().Length + 1);
                    if (select.Prefix == null || path.StartsWith(select.Prefix))
                        yield return new AzureBlobSpec(Container, path, blob, true);
                }
        }
    }

    public sealed class AzureBlobSpec : BlobSpec<AzureProvider, AzureBlobContainer, AzureBlobSpecCollection>
    {
        public CloudBlob Impl { get; private set; }

        #region .ctor

        internal AzureBlobSpec(AzureBlobContainer container, string path, StorageResourceDefinition config)
            : this(container, path, container.Impl.GetBlobReference(path), null)
        {
        }

        internal AzureBlobSpec(AzureBlobContainer container, string path, CloudBlob impl, bool? found)
            : base(container, path ?? container.GetPath(impl))
        {
            Impl = impl;
            Refresh(found);
        }

        #endregion

        #region Internals

        private void Refresh(bool? found)
        {
            Found = found;
            MetaData = Impl.Attributes.Metadata.Clone();

            if (found.GetValueOrDefault(false))
            {
                var bp = Impl.Attributes.Properties;
                ETag = bp.ETag;
                LastModified = (bp.LastModifiedUtc == DateTime.MinValue)
                                   ? (DateTime?) null
                                   : bp.LastModifiedUtc;
                ContentType = bp.ContentType;
                ContentLanguage = bp.ContentLanguage;
                ContentLength = bp.Length;
                ContentEncoding = bp.ContentEncoding;
                ContentMD5 = bp.ContentMD5;
            }
        }

        internal Exception TranslateException(StorageException ex)
        {
            switch (ex.ErrorCode)
            {
                case StorageErrorCode.BlobNotFound:
                case StorageErrorCode.ResourceNotFound:
                    return new BlobDoesNotExistsException(this, ex);
                case StorageErrorCode.BlobAlreadyExists:
                    return new BlobAlreadyExistsException(this, ex);
                default:
                    return Container.TranslateException(ex);
            }
        }

        #endregion

        public override Stream Read(BlobOptions options)
        {
            var stream = (MemoryStream)null;
            try
            {
                if (options.IncludeData)
                {
                    stream = new MemoryStream();
                    using (this.LogBlobRequests())
                        Impl.DownloadToStream(stream, this.ApplyOptions(options));
                    stream.Position = 0;
                }
                else
                {
                    if (!options.IncludeMetadata)
                        return null;
                    using (this.LogBlobRequests())
                        Impl.FetchAttributes(this.ApplyOptions(options));
                }
                Refresh(true);
                this.OnBlobRead();
                return stream;
            }
            catch (StorageException ex)
            {
                if (stream != null)
                    stream.Dispose();
                var ex2 = TranslateException(ex);
                if (ex2 is BlobDoesNotExistsException)
                {
                    Found = false;
                    if (options.IncludeMetadata && !options.IncludeData)
                        return null;
                }
                throw ex2;
            }
            catch (Exception)
            {
                if (stream != null)
                    stream.Dispose();
                throw;
            }
        }

        public override void Write(Stream stream, BlobOptions options)
        {
            try
            {
                stream.Position = 0;

                Impl.Metadata.ReplaceWith(MetaData);
                Impl.Properties.ContentEncoding = this.ContentEncoding;
                Impl.Properties.ContentLanguage = this.ContentLanguage;
                Impl.Properties.ContentMD5 = this.ContentMD5;
                Impl.Properties.ContentType = this.ContentType;
                using (this.LogBlobRequests())
                    Impl.UploadFromStream(stream, this.ApplyOptions(options));
                Refresh(true);
                this.OnBlobWrite();
            }
            catch (StorageException ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void Delete()
        {
            try
            {
                using (this.LogBlobRequests())
                    Impl.Delete();
                Found = false;
                this.OnBlobDeleted();
            }
            catch (StorageException ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void DeleteIfExists()
        {
            try
            {
                using (this.LogBlobRequests())
                    Impl.DeleteIfExists();
                Found = false;
                this.OnBlobDeleted();
            }
            catch (StorageException ex)
            {
                throw TranslateException(ex);
            }
        }
    }

    public static class AzureExtensions
    {
        public static BlobRequestOptions ApplyOptions(this IBlobResource resource, BlobOptions options)
        {
            var value = new BlobRequestOptions();
            if (options.IncludeMetadata)
                value.BlobListingDetails |= BlobListingDetails.Metadata;
            switch (options.Condition)
            {
                case BlobCondition.IfMatch:
                    value.AccessCondition = AccessCondition.IfMatch(resource.ETag);
                    break;
                case BlobCondition.IfNoneMatch:
                    value.AccessCondition = AccessCondition.IfNoneMatch(resource.ETag);
                    break;
                case BlobCondition.IfModifiedSince:
                    value.AccessCondition = AccessCondition.IfModifiedSince(resource.LastModified.Value);
                    break;
                case BlobCondition.IfNotModifiedSince:
                    value.AccessCondition = AccessCondition.IfNotModifiedSince(resource.LastModified.Value);
                    break;
            }
            return value;
        }
    }
}