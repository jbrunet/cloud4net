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
using System.StorageModel.AWS;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.StorageModel.Tests
{
    [TestClass]
    public class AWSProviderTests : ProviderTests<AWSProvider>
    {
        #region .ctor

        public AWSProviderTests()
            : base("AWS")
        {
        }

        #endregion

        [TestInitialize]
        public new void TestInitialize()
        {
            base.TestInitialize();
        }

        [TestCleanup]
        public new void TestCleanup()
        {
            base.TestCleanup();
        }

        #region Blob Container Tests

        [TestMethod]
        public void AWSTest00_ContainerList()
        {
            base.Test00_ContainerList();
        }

        [TestMethod]
        public void AWSTest01_CreateContainerIfNotExists()
        {
            base.Test01_CreateContainerIfNotExists();
        }

        [TestMethod]
        public void AWSTest01a_ListBlobs()
        {
            base.Test01a_ListBlobs();
        }

        [TestMethod]
        public void AWSTest02_CreateContainerIfNotExist2()
        {
            base.Test02_CreateContainerIfNotExist2();
        }

        [TestMethod]
        public void AWSTest03_ContainerExists()
        {
            base.Test03_ContainerExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerAlreadyExistsException))]
        public void AWSTest04_CreateContainer()
        {
            base.Test04_CreateContainer();
        }

        [TestMethod]
        public void AWSTest05_DeleteContainerIfExists()
        {
            base.Test05_DeleteContainerIfExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerDoesNotExistsException))]
        public void AWSTest06_DeleteContainer()
        {
            base.Test06_DeleteContainer();
        }

        [TestMethod]
        public void AWSTest07_ContainerDoesNotExists()
        {
            base.Test07_ContainerDoesNotExists();
        }

        [TestMethod]
        public void AWSTest08_DeleteContainerIfExists2()
        {
            base.Test08_DeleteContainerIfExists2();
        }

        #endregion
    }
}