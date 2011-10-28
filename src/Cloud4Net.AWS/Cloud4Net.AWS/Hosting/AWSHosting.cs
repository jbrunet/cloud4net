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
using System.Globalization;
using System.Linq;
using System.Net;
using System.StorageModel.Hosting;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using Amazon.S3.Model;

namespace System.StorageModel.AWS.Hosting
{
    partial class AWSHostProvider
    {
        private AWSProvider _source;
        private IBlobProvider _target;

        public void Bridge(AWSProvider source, IBlobProvider target)
        {
            if (_source != null)
                throw new NotSupportedException("SourceProvider already bound");
            _source = source;
            if (_target != null)
                throw new NotSupportedException("TargetProvider already bound");
            _target = target;
        }

        private static Exception BadHttpMethod(IWebRequest req)
        {
            return new NotSupportedException("Bad HTTP method " + req.HttpMethod);
        }

        partial void SetupBlobs(StorageHostContext context)
        {
            var host = _source.S3Config.ServiceURL;
            var ub = new UriBuilder { Host = host, Path = "/" };
            switch (_source.S3Config.CommunicationProtocol)
            {
                case Protocol.HTTP:
                    ub.Scheme = Uri.UriSchemeHttp;
                    break;
                case Protocol.HTTPS:
                    ub.Scheme = Uri.UriSchemeHttps;
                    break;
                default:
                    throw new NotImplementedException("Protocol not implemented " +
                                                      _source.S3Config.CommunicationProtocol);
            }
            var uriPrefixes = new List<Uri> { ub.Uri };
            // add extra hosts for named containers
            foreach (var container in _source.Containers)
            {
                ub.Host = container.Name + '.' + host;
                uriPrefixes.Add(ub.Uri);
            }
            context.Register(ProcessS3Request, uriPrefixes.ToArray());
        }

        void ProcessS3Request(IWebContext context)
        {
            var req = context.Request;

            var host = req.Url.Host;
            var p = host.IndexOf("s3");
            switch (p)
            {
                case -1:
                    throw new NotSupportedException("invalid AWS S3 host name - must contains 's3'");
                case 0:

                    #region Service operations

                    if (req.HttpMethod != "GET")
                        throw BadHttpMethod(req);
                    if (req.Url.AbsolutePath != "/")
                        throw new NotSupportedException("Bad HTTP path");
                    ListAllBuckets(context);
                    break;

                    #endregion

                default:
                    {
                        var bucketName = host.Substring(0, p - 1);
                        var container = _target.Containers[bucketName];

                        var objectName = req.Url.AbsolutePath.TrimStart('/');
                        if (string.IsNullOrEmpty(objectName))
                        {
                            #region Bucket operations

                            switch (req.HttpMethod)
                            {
                                case "GET":
                                    GetBucket(context, container);
                                    break;
                                case "PUT":
                                    PutBucket(context, container);
                                    break;
                                case "DELETE":
                                    DeleteBucket(context, container);
                                    break;
                                default:
                                    throw BadHttpMethod(req);
                            }

                            #endregion
                        }
                        else
                        {
                            #region Object operations

                            var spec = container.Blobs[objectName];

                            switch (req.HttpMethod)
                            {
                                case "GET":
                                    GetObject(context, spec, true);
                                    break;
                                case "PUT":
                                    PutObject(context, spec);
                                    break;
                                case "HEAD":
                                    GetObject(context, spec, false);
                                    break;
                                case "DELETE":
                                    DeleteObject(context, spec);
                                    break;
                                default:
                                    throw BadHttpMethod(req);
                            }

                            #endregion
                        }
                    }
                    break;
            }
        }

        private const string S3Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";
        const string GMTDateFormat = @"ddd, dd MMM yyyy HH:mm:ss \G\M\T";
        const string iso8601DateFormat = @"yyyy-MM-dd\THH:mm:ss.fff\Z";

        private string _amazonId2 = "CIvkhODGi0fzCD7Yj8g1OhCd4Pt0ejRYL5kQpBG683yFh5nbLMykKg1o/7UUCBhu";
        private long _requestID;

        private void WriteResponse<T>(IWebContext context, T response, Func<T, XElement> transform)
            where T : S3Response
        {
            var amazonId2 = response.AmazonId2;
            if (string.IsNullOrEmpty(amazonId2))
                amazonId2 = _amazonId2;
            else
                response.AmazonId2 = null;
            context.Response.AppendHeader("x-amz-id-2", amazonId2);

            var requestId = response.RequestId;
            if (string.IsNullOrEmpty(requestId))
                requestId = Interlocked.Increment(ref _requestID).ToString("X16");
            else
                response.RequestId = null;
            context.Response.AppendHeader("x-amz-request-id", requestId);

            foreach (string name in response.Headers)
                context.Response.AppendHeader(name, response.Headers[name]);

            context.Response.AppendHeader("Date", DateTime.UtcNow.ToString(GMTDateFormat));
            context.Response.AppendHeader("Server", "AmazonS3");

            foreach (string name in response.Metadata)
                context.Response.AppendHeader("x-amz-meta-" + name, response.Metadata[name]);

            if (transform == null)
            {
                var ser = new XmlSerializer(typeof(T));
                ser.Serialize(context.Response.OutputStream, response);
            }
            else
            {
                if (response != null)
                {
                    var doc = new XDocument(
                        new XDeclaration("1.0", Encoding.UTF8.WebName, "yes"),
                        transform(response));
                    using (var writer = XmlWriter.Create(context.Response.OutputStream))
                        doc.WriteTo(writer);
                }
            }
            context.Response.OutputStream.Close();
        }

        private static string ExportDate(string date)
        {
            return DateTime.ParseExact(date, GMTDateFormat, DateTimeFormatInfo.CurrentInfo, DateTimeStyles.AssumeUniversal)
                .ToString(iso8601DateFormat);
        }

        #region ListAllMyBuckets

        private static readonly XName ListAllMyBucketsResult = XName.Get("ListAllMyBucketsResult", S3Namespace);
        private static readonly XName Buckets = XName.Get("Buckets", S3Namespace);
        private static readonly XName Bucket = XName.Get("Bucket", S3Namespace);
        private static readonly XName Name = XName.Get("Name", S3Namespace);
        private static readonly XName CreationDate = XName.Get("CreationDate", S3Namespace);

        private static readonly XName Owner = XName.Get("Owner", S3Namespace);
        private static readonly XName DisplayName = XName.Get("DisplayName", S3Namespace);
        private static readonly XName ID = XName.Get("ID", S3Namespace);

        private void ListAllBuckets(IWebContext context)
        {
            var res = new ListBucketsResponse
            {
                Owner = new Owner
                {
                    DisplayName = "amazon",
                    Id = "abcde"
                }
            };
            res.Buckets.AddRange(
                from container in _target.Containers.FindAll()
                where container.Exists
                select new S3Bucket
                {
                    BucketName = container.Name,
                    CreationDate = container.LastModified.Value.ToString(iso8601DateFormat)
                });

            WriteResponse(context, res, Transform);
        }

        private static XElement Transform(ListBucketsResponse r)
        {
            return new XElement(ListAllMyBucketsResult
                                , new XElement(Owner
                                               ,
                                               new XElement(DisplayName,
                                                            r.Owner.DisplayName)
                                               , new XElement(ID, r.Owner.Id))
                                , new XElement(Buckets
                                               ,
                                               from bucket in r.Buckets
                                               select new XElement(Bucket
                                                                   , new XElement(Name, bucket.BucketName)
                                                                   ,
                                                                   new XElement(CreationDate,
                                                                                ExportDate(bucket.CreationDate)))
                                      ) //Buckets
                ); //Result
        }

        #endregion

        #region GetBucket

        private void GetBucket(IWebContext context, IBlobContainer container)
        {
            if (context.Request.Url.Query.StartsWith("?location"))
            {
                //container.Exists;
                var res = new GetBucketLocationResponse
                {
                };
                WriteResponse(context, res, Transform);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static readonly XName LocationConstraint = XName.Get("LocationConstraint", S3Namespace);

        private static XElement Transform(GetBucketLocationResponse res)
        {
            return new XElement(LocationConstraint);
        }

        #endregion

        private void PutBucket(IWebContext context, IBlobContainer container)
        {
            throw new NotImplementedException();
        }

        #region DeleteBucket

        private void DeleteBucket(IWebContext context, IBlobContainer container)
        {
            container.Delete();
            context.Response.StatusCode = HttpStatusCode.NoContent;
            context.Response.StatusDescription = "No Content";
            WriteResponse<S3Response>(context, null, null);
        }

        #endregion

        private void GetObject(IWebContext context, IBlobSpec spec, bool data)
        {
            throw new NotImplementedException();
        }

        private void PutObject(IWebContext context, IBlobSpec spec)
        {
            throw new NotImplementedException();
        }

        private void DeleteObject(IWebContext context, IBlobSpec spec)
        {
            throw new NotImplementedException();
        }
    }
}