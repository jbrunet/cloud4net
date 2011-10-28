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

using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace System.StorageModel.Build
{
    public abstract class CloudTask : Task
    {
        public string ConfigFile
        {
            set
            {
                Storage.LoadConnectionStrings(value);
            }
        }
    }

    public class CopyBlob : CloudTask
    {
        private readonly BlobSelect _select = new BlobSelect();

        #region Parameters

        [Required]
        public string SourceStorage { get; set; }

        private IBlobProvider _sourceProvider;
        public IBlobProvider SourceProvider
        {
            get { return _sourceProvider ?? (_sourceProvider = Storage.GetProvider<IBlobProvider>(SourceStorage)); }
        }

        [Required]
        public string TargetStorage { get; set; }

        private IBlobProvider _targetProvider;
        public IBlobProvider TargetProvider
        {
            get { return _targetProvider ?? (_targetProvider = Storage.GetProvider<IBlobProvider>(TargetStorage)); }
        }

        public string Prefix
        {
            set { _select.Prefix = value; }
        }

        public string Delimiter
        {
            set { _select.Delimiter = value; }
        }

        public int Take
        {
            set { _select.Take = value; }
        }

        public bool IfNew { get; set; }

        public ITaskItem[] Containers { get; set; }

        #endregion

        public override bool Execute()
        {
            if (Containers == null)
                Log.LogMessage("{0}: Listing containers...", SourceProvider.Name);
            var containers = (Containers == null)
                                 ? SourceProvider.Containers.FindAll()
                                 : (from c in Containers
                                    select SourceProvider.NewContainer(c.ItemSpec, null)
                                    );

            foreach (var container in containers)
            {
                Log.LogMessage("{0}: Container {1}", TargetProvider.Name, container.Name);

                var container2 = TargetProvider.Containers[container.Name];
                container2.CreateIfNotExists();

                foreach (var blob in container.Blobs.Find(_select))
                {
                    Log.LogMessage("  Copying Blob {0}", blob.Path);
                    var blob2 = container2.Blobs[blob.Path];
                    blob.CopyTo(blob2, BlobOptions.Default, BlobCopyCondition.IfNewer);
                }
            }

            return true;
        }
    }
}