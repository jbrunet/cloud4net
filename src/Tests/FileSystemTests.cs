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
using System.StorageModel.Local;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.StorageModel.Tests
{
    [TestClass]
    public class FileSystemProviderTests : ProviderTests<FileSystemProvider>
    {
        #region .ctor

        public FileSystemProviderTests()
            : base("FileSystem")
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
        public void FileSystemTest00_ContainerList()
        {
            base.Test00_ContainerList();
        }

        [TestMethod]
        public void FileSystemTest01_CreateContainerIfNotExists()
        {
            base.Test01_CreateContainerIfNotExists();
        }

        [TestMethod]
        public void FileSystemTest02_CreateContainerIfNotExist2()
        {
            base.Test02_CreateContainerIfNotExist2();
        }

        [TestMethod]
        public void FileSystemTest03_ContainerExists()
        {
            base.Test03_ContainerExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerAlreadyExistsException))]
        public void FileSystemTest04_CreateContainer()
        {
            base.Test04_CreateContainer();
        }

        [TestMethod]
        public void FileSystemTest05_DeleteContainerIfExists()
        {
            base.Test05_DeleteContainerIfExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerDoesNotExistsException))]
        public void FileSystemTest06_DeleteContainer()
        {
            base.Test06_DeleteContainer();
        }

        [TestMethod]
        public void FileSystemTest07_ContainerDoesNotExists()
        {
            base.Test07_ContainerDoesNotExists();
        }

        [TestMethod]
        public void FileSystemTest08_DeleteContainerIfExists2()
        {
            base.Test08_DeleteContainerIfExists2();
        }

        #endregion
    }
}