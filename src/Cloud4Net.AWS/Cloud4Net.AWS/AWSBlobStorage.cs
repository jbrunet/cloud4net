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
#region AWS License
// This assembly links to parts of the AWS SDK.

/*******************************************************************************
 *  Copyright 2009 Amazon.com, Inc. or its affiliates. All Rights Reserved.
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *
 *  You may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at: http://aws.amazon.com/apache2.0
 *  This file is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 *  CONDITIONS OF ANY KIND, either express or implied. See the License for the
 *  specific language governing permissions and limitations under the License.
 * *****************************************************************************
 *    __  _    _  ___
 *   (  )( \/\/ )/ __)
 *   /__\ \    / \__ \
 *  (_)(_) \/\/  (___/
 *
 *  AWS SDK for .NET
 *  API Version: 2006-03-01
 *
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.StorageModel.Configuration;
using Amazon.S3;
using Amazon.S3.Model;

namespace System.StorageModel.AWS
{
    partial class AWSProvider : IBlobProvider
    {
        public AmazonS3Config S3Config { get; private set; }
        public S3Region S3Region { get; private set; }
        public AmazonS3 S3Client { get; private set; }
        public AWSBlobContainerCollection Containers { get; private set; }

        private Owner _s3Owner;
        public Owner S3Owner
        {
            get
            {
                if (_s3Owner == null)
                {
                    using (var res = S3Client.ListBuckets())
                    {
                        _s3Owner = res.Owner;
                    }
                }
                return _s3Owner;
            }
            internal set { _s3Owner = value; }
        }

        #region .ctor

        partial void InitializeBlobs(NameValueCollection config)
        {
            S3Config = new AmazonS3Config();
            config.OnKey("CommunicationProtocol", value =>
                                                      {
                                                          if (value == Uri.UriSchemeHttps)
                                                              S3Config.CommunicationProtocol = Protocol.HTTPS;
                                                          else if (value == Uri.UriSchemeHttp)
                                                              S3Config.CommunicationProtocol = Protocol.HTTP;
                                                      });
            config.OnKey("ServiceURL", value =>
                                           {
                                               Uri uri;
                                               if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                                               {
                                                   value = uri.Host;
                                                   if (uri.Scheme == Uri.UriSchemeHttps)
                                                       S3Config.CommunicationProtocol = Protocol.HTTPS;
                                                   else if (uri.Scheme == Uri.UriSchemeHttp)
                                                       S3Config.CommunicationProtocol = Protocol.HTTP;
                                                   else
                                                       throw new ArgumentException("Invalid uri scheme " + uri.Scheme);
                                               }
                                               S3Config.ServiceURL = value;
                                           });
            config.OnKey("UserAgent", value => S3Config.UserAgent = value);
            config.OnKey("MaxErrorRetry", value => S3Config.MaxErrorRetry = int.Parse(value));
            config.OnKey("ProxyURL", value =>
                                         {
                                             var uri = new Uri(value, UriKind.Absolute);
                                             S3Config.ProxyHost = uri.Host;
                                             S3Config.ProxyPort = uri.Port;
                                         });
            config.OnKey("Region"
                         , value => S3Region = (S3Region)Enum.Parse(typeof(S3Region), value, true)
                         , () =>
                               {
                                   //throw new ConfigurationErrorsException("Region must be specified");
                               });

            S3Client = new AmazonS3Client(_accessKeyId, _secretAccessKey, S3Config);
            Containers = new AWSBlobContainerCollection(this);
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
            return new AWSBlobContainer(this, name, config);
        }

        IBlobSpec IBlobProvider.NewSpec(IBlobContainer container, string path, StorageResourceDefinition config)
        {
            var awsContainer = (AWSBlobContainer)container;
            return new AWSBlobSpec(awsContainer, path, config);
        }

        public string NormalizeContainerName(string name)
        {
            return name.ToLowerInvariant();
        }

        public string NormalizeBlobPath(string path)
        {
            return path;
        }

        public bool Copy(IBlobSpec spec1, IBlobSpec target, BlobOptions options, BlobCopyConditionDelegate condition)
        {
            var req = new CopyObjectRequest
                          {
                              SourceBucket = spec1.Container.Name,
                              SourceKey = spec1.Path,
                              DestinationBucket = target.Container.Name,
                              DestinationKey = target.Path,
                          };
            switch (options.Condition)
            {
                case BlobCondition.IfMatch:
                    req.ETagToMatch = spec1.ETag;
                    break;
                case BlobCondition.IfNoneMatch:
                    req.ETagToNotMatch = spec1.ETag;
                    break;
                case BlobCondition.IfModifiedSince:
                    req.ModifiedSinceDate = spec1.LastModified.Value;
                    break;
                case BlobCondition.IfNotModifiedSince:
                    req.UnmodifiedSinceDate = spec1.LastModified.Value;
                    break;
            }
            using (this.LogBlobRequests(null))
            using (var res = S3Client.CopyObject(req))
            {
                return true;
            }
        }

        #endregion
    }

    public sealed class AWSBlobContainerCollection : BlobContainerCollection<AWSProvider, AWSBlobContainer>
    {
        #region .ctor

        internal AWSBlobContainerCollection(AWSProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AWSBlobContainer> FindAll()
        {
            using (Provider.LogBlobRequests(null))
            using (var res = Provider.S3Client.ListBuckets())
            {
                Provider.S3Owner = res.Owner;
                foreach (var bucket in res.Buckets)
                    yield return new AWSBlobContainer(Provider, bucket);
            }
        }
    }

    public sealed class AWSBlobContainer : BlobContainer<AWSProvider, AWSBlobSpecCollection>
    {
        public S3Bucket Bucket { get; private set; }

        #region .ctor

        internal AWSBlobContainer(AWSProvider provider, string name, StorageResourceDefinition config)
            : base(provider, name)
        {
            this.Blobs = new AWSBlobSpecCollection(this);
        }

        internal AWSBlobContainer(AWSProvider provider, S3Bucket bucket)
            : base(provider, bucket.BucketName)
        {
            Bucket = bucket;
            this.Blobs = new AWSBlobSpecCollection(this);
        }

        #endregion

        #region Internals

        private void Refresh(S3Response response)
        {
            this.Parse(response.Headers);
        }

        private Exception TranslateException(AmazonS3Exception ex)
        {
            switch (ex.ErrorCode)
            {
                case "NoSuchBucket":
                    return new ContainerDoesNotExistsException(this, ex);
                default:
                    return ex;
            }
        }

        #endregion

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public override bool Exists
        {
            get
            {
                if (!Found.HasValue)
                {
                    try
                    {
                        var req = new ListObjectsRequest
                                      {
                                          BucketName = this.Name,
                                          MaxKeys = 1
                                      };
                        using (this.LogBlobRequests())
                        using (var res = Provider.S3Client.ListObjects(req))
                        {
                            Found = true;
                        }
                    }
                    catch (AmazonS3Exception ex)
                    {
                        var ex2 = TranslateException(ex);
                        if (ex2 is ContainerDoesNotExistsException)
                            Found = false;
                        else
                            throw ex2;
                    }
                }
                return Found.Value;
            }
        }

        public override void CreateIfNotExists()
        {
            var req = new PutBucketRequest
            {
                BucketName = Name,
                BucketRegion = Provider.S3Region,
            };
            try
            {
                using (this.LogBlobRequests())
                using (var res = Provider.S3Client.PutBucket(req))
                {
                    Refresh(res);
                    Found = true;
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw TranslateException(ex);
            }
        }

        public override void Create()
        {
            if (Exists)
                throw new ContainerAlreadyExistsException(this, null);
            CreateIfNotExists();
        }

        public override void Delete()
        {
            try
            {
                // AWS requires that the bucket (container) is empty before deleting the container
                foreach (var blob in this.Blobs.FindAll(BlobSelect.All))
                    blob.Delete();

                var req = new DeleteBucketRequest
                              {
                                  BucketName = Name
                              };
                using (this.LogBlobRequests())
                using (var res = Provider.S3Client.DeleteBucket(req))
                {
                    Refresh(res);
                    Found = false;
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw TranslateException(ex);
            }
        }

        // AWS Specific

        public S3BucketLoggingConfig LoggingConfig
        {
            get
            {
                var req = new GetBucketLoggingRequest
                              {
                                  BucketName = Name
                              };
                using (this.LogBlobRequests())
                {
                    var res = Provider.S3Client.GetBucketLogging(req);
                    return res.BucketLoggingConfig;
                }
            }
            set
            {
                if (value != null)
                {
                    var req = new EnableBucketLoggingRequest { BucketName = Name, LoggingConfig = value };
                    using (this.LogBlobRequests())
                    {
                        var res = Provider.S3Client.EnableBucketLogging(req);
                    }
                }
                else
                {
                    var req = new DisableBucketLoggingRequest { BucketName = Name };
                    using (this.LogBlobRequests())
                    {
                        var res = Provider.S3Client.DisableBucketLogging(req);
                    }
                }
            }
        }
    }

    public sealed class AWSBlobSpecCollection : BlobSpecCollection<AWSBlobContainer, AWSBlobSpec>
    {
        #region .ctor

        internal AWSBlobSpecCollection(AWSBlobContainer container)
            : base(container)
        {
        }

        #endregion

        public override IEnumerable<AWSBlobSpec> FindAll(BlobSelect select)
        {
            var req = new ListObjectsRequest
            {
                BucketName = Container.Name,
                //Delimiter = select.Delimiter,
                //Marker = select.Marker,
                //Prefix = select.Prefix
            };
            if (select.Take.HasValue)
                req.MaxKeys = select.Take.Value;
            using (Container.LogBlobRequests())
            using (var res = Container.Provider.S3Client.ListObjects(req))
            {
                foreach (var obj in res.S3Objects)
                {
                    yield return new AWSBlobSpec(Container, obj);
                    //if (!res.IsTruncated)
                    //    yield break;

                    //select = new BlobSelect
                    //             {
                    //                 Delimiter = select.Delimiter,
                    //                 Marker = res.,
                    //                 Prefix = select.Prefix,
                    //                 Take = select.Take
                    //             };
                    //var nextList = GetBlobs(container, select);
                    //foreach (var next in nextList)
                    //    yield return next;
                }
            }
        }
    }

    public sealed class AWSBlobSpec : BlobSpec<AWSProvider, AWSBlobContainer, AWSBlobSpecCollection>
    {
        public S3Object Object { get; private set; }

        #region .ctor

        internal AWSBlobSpec(AWSBlobContainer container, string path, StorageResourceDefinition config)
            : base(container, path)
        {
            MetaData = new NameValueCollection();
        }

        internal AWSBlobSpec(AWSBlobContainer container, S3Object obj)
            : base(container, obj.Key)
        {
            Object = obj;

            ETag = obj.ETag;
            this.ParseLastModified(obj.LastModified);
            this.ContentLength = obj.Size;
            MetaData = new NameValueCollection();
        }

        #endregion

        private void Refresh(S3Response response)
        {
            this.Parse(response.Headers);
            this.MetaData = response.Metadata.Clone();
        }

        public override Stream Read(BlobOptions options)
        {
            if (options.IncludeMetadata)
            {
                var mdreq = new GetObjectMetadataRequest
                                {
                                    BucketName = Container.Name,
                                    Key = this.Path,
                                };
                switch (options.Condition)
                {
                    case BlobCondition.IfMatch:
                        mdreq.ETagToMatch = this.ETag;
                        break;
                    case BlobCondition.IfNoneMatch:
                        mdreq.ETagToNotMatch = this.ETag;
                        break;
                    case BlobCondition.IfModifiedSince:
                        mdreq.ModifiedSinceDate = this.LastModified.Value;
                        break;
                    case BlobCondition.IfNotModifiedSince:
                        mdreq.UnmodifiedSinceDate = this.LastModified.Value;
                        break;
                }
                using (this.LogBlobRequests())
                using (var res = Container.Provider.S3Client.GetObjectMetadata(mdreq))
                {
                    Refresh(res);
                    this.ContentLength = res.ContentLength;
                    this.ContentType = res.ContentType;
                    this.ETag = res.ETag;
                    Found = true;
                }
            }

            if (!options.IncludeData)
                return null;

            var req = new GetObjectRequest
                          {
                              BucketName = Container.Name,
                              Key = Path,
                          };
            switch (options.Condition)
            {
                case BlobCondition.IfMatch:
                    req.ETagToMatch = this.ETag;
                    break;
                case BlobCondition.IfNoneMatch:
                    req.ETagToNotMatch = this.ETag;
                    break;
                case BlobCondition.IfModifiedSince:
                    req.ModifiedSinceDate = this.LastModified.Value;
                    break;
                case BlobCondition.IfNotModifiedSince:
                    req.UnmodifiedSinceDate = this.LastModified.Value;
                    break;
            }
            if (options.RangeStart.HasValue)
                req.ByteRange.First = (int) options.RangeStart.Value;
            if (options.RangeEnd.HasValue)
                req.ByteRange.Second = (int) options.RangeEnd.Value;

            using (this.LogBlobRequests())
            {
                var res = Container.Provider.S3Client.GetObject(req);
                Refresh(res);
                this.ContentLength = res.ContentLength;
                this.ContentType = res.ContentType;
                this.ETag = res.ETag;
                Found = true;
                return new AWSBlobStream(res);
            }
        }

        private class AWSBlobStream : Stream
        {
            private readonly GetObjectResponse _response;
            private readonly Stream _responseStream;

            #region .ctor

            public AWSBlobStream(GetObjectResponse response)
            {
                _response = response;
                _responseStream = response.ResponseStream;
            }

            #endregion

            public override void Close()
            {
                base.Close();
                _response.Dispose();
            }

            public override bool CanRead
            {
                get { return _responseStream.CanRead; }
            }

            public override bool CanSeek
            {
                get { return _responseStream.CanSeek; }
            }

            public override bool CanWrite
            {
                get { return _responseStream.CanWrite; }
            }

            public override void Flush()
            {
                _responseStream.Flush();
            }

            public override long Length
            {
                get { return _responseStream.Length; }
            }

            public override long Position
            {
                get { return _responseStream.Position; }
                set { _responseStream.Position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _responseStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _responseStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _responseStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _responseStream.Write(buffer, offset, count);
            }
        }

        public override void Write(Stream stream, BlobOptions options)
        {
            var req = new PutObjectRequest
                          {
                              BucketName = Container.Name,
                              Key = Path,
                              InputStream = stream,
                              ContentType = this.ContentType
                          };
            var headers = new WebHeaderCollection();
            this.Apply(options, headers);
            req.AddHeaders(headers);

            using (this.LogBlobRequests())
            using (var res = Container.Provider.S3Client.PutObject(req))
            {
                Refresh(res);
            }
        }

        public override void Delete()
        {
            var req = new DeleteObjectRequest
                          {
                              BucketName = Container.Name,
                              Key = Path,
                          };
            using (this.LogBlobRequests())
            using (var res = Container.Provider.S3Client.DeleteObject(req))
            {
                Refresh(res);
                Found = false;
            }
        }
    }
}