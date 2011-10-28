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
using System.Collections.Specialized;
using System.Configuration;
using System.Security;
using System.StorageModel.AWS.Hosting;
using System.StorageModel.Hosting;

namespace System.StorageModel.AWS
{
    public partial class AWSProvider : StorageProvider
    {
        private string _accessKeyId;
        private SecureString _secretAccessKey;

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            config.OnKey("AWSAccessKeyId"
                         , value => _accessKeyId = value
                         , () => { throw new ConfigurationErrorsException("AWSAccessKeyID is not specified"); });
            config.OnKey("SecretAccessKey"
                         , value =>
                             {
                                 _secretAccessKey = new SecureString();
                                 foreach (var c in value)
                                     _secretAccessKey.AppendChar(c);
                             }
                         , () => { throw new ConfigurationErrorsException("SecretAccessKey is not specified"); });

            InitializeBlobs(config);
            InitializeQueues(config);
            InitializeTables(config);
            InitializeBilling(config);
        }

        partial void InitializeBlobs(NameValueCollection config);
        partial void InitializeQueues(NameValueCollection config);
        partial void InitializeTables(NameValueCollection config);
        partial void InitializeBilling(NameValueCollection config);
    }

    namespace Hosting
    {
        public sealed partial class AWSHostProvider : IStorageHostProvider
        {
            public void Setup(StorageHostContext context)
            {
                SetupBlobs(context);
            }

            partial void SetupBlobs(StorageHostContext context);
        }
    }

    public static class AWSStorage
    {
        public static AWSHostProvider Bridge(this AWSProvider provider, string targetStorage)
        {
            var hostProvider = new AWSHostProvider();
            var targetProvider = Storage.GetProvider<StorageProvider>(targetStorage);
            var blobProvider = targetProvider as IBlobProvider;
            if (blobProvider != null)
                hostProvider.Bridge(provider, blobProvider);
            return hostProvider;
        }
    }
}