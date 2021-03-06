﻿#region License
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

using System.Collections.Specialized;

namespace System.StorageModel.Local
{
    public partial class FileSystemProvider : StorageProvider
    {
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            InitializeBlobs(config);
            InitializeQueues(config);
            InitializeTables(config);
        }

        public void Initialize(string name, string blobPath)
        {
            var config = new NameValueCollection { { "BlobPath", blobPath } };
            Initialize(name, config);
        }

        partial void InitializeBlobs(NameValueCollection config);
        partial void InitializeQueues(NameValueCollection config);
        partial void InitializeTables(NameValueCollection config);
    }
}