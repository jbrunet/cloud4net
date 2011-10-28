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
using System.StorageModel.WindowsAzure;
using System.StorageModel.WindowsAzure.Billing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace System.StorageModel.Tests
{
    [TestClass]
    public class AzureProviderTests : ProviderTests<AzureProvider>
    {
        #region .ctor

        public AzureProviderTests()
            : base("Azure")
        {
            this.Provider.Pricing = AzurePricingPlans.DevelopmentAcceleratorExtended(AzureRegion.NorthAmericaAndEurope,
                                                                                     AzurePricingPlans.
                                                                                         OffPeak_Until_30_June_2010.
                                                                                         AddMonths(1));
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
        public void AzureTest00_ContainerList()
        {
            base.Test00_ContainerList();
        }

        [TestMethod]
        public void AzureTest01_CreateContainerIfNotExists()
        {
            base.Test01_CreateContainerIfNotExists();
        }

        [TestMethod]
        public void AzureTest02_CreateContainerIfNotExist2()
        {
            base.Test02_CreateContainerIfNotExist2();
        }

        [TestMethod]
        public void AzureTest03_ContainerExists()
        {
            base.Test03_ContainerExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerAlreadyExistsException))]
        public void AzureTest04_CreateContainer()
        {
            base.Test04_CreateContainer();
        }

        [TestMethod]
        public void AzureTest05_DeleteContainerIfExists()
        {
            base.Test05_DeleteContainerIfExists();
        }

        [TestMethod]
        [ExpectedException(typeof(ContainerDoesNotExistsException))]
        public void AzureTest06_DeleteContainer()
        {
            base.Test06_DeleteContainer();
        }

        [TestMethod]
        public void AzureTest07_ContainerDoesNotExists()
        {
            base.Test07_ContainerDoesNotExists();
        }

        [TestMethod]
        public void AzureTest08_DeleteContainerIfExists2()
        {
            base.Test08_DeleteContainerIfExists2();
        }

        #endregion
    }
}