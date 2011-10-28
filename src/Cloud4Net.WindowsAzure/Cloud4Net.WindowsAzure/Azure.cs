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
using System.Collections.Specialized;
using System.Configuration;
using Microsoft.WindowsAzure;

namespace System.StorageModel.WindowsAzure
{
    public partial class AzureProvider : StorageProvider
    {
        public string AccountName { get; private set; }
        public CloudStorageAccount Account { get; private set; }

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            AccountName = config["AccountName"];
            if (string.IsNullOrEmpty(AccountName))
                throw new ConfigurationErrorsException("AccountName is not specified");
            var accountKey = config["AccountKey"];
            if (string.IsNullOrEmpty(accountKey))
                throw new ConfigurationErrorsException("AccountKey is not specified");

            var useHttps = true;
            var protocol = config["DefaultEndpointsProtocol"];
            if (!string.IsNullOrEmpty(protocol))
                switch (protocol)
                {
                    case "http":
                        useHttps = false;
                        break;
                    case "https":
                        break;
                    default:
                        throw new ConfigurationErrorsException("Unknown DefaultEndpointsProtocol value");
                }

            Account = new CloudStorageAccount(new StorageCredentialsAccountAndKey(AccountName, accountKey), useHttps);

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
}