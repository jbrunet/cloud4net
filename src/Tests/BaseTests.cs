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

using System.Diagnostics;
using System.Linq;
using System.StorageModel.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.StorageModel.Billing;

namespace System.StorageModel.Tests
{
    public class TestTraceListener : TraceListener
    {
        public static TestContext Context;

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            WriteLine(source + ": " + string.Format(format, args));
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            WriteLine(source + ": " + message);
        }

        public override void Write(string message)
        {
            if (Context != null)
                Context.WriteLine(message);
        }

        public override void WriteLine(string message)
        {
            if (Context != null)
                Context.WriteLine(message);
        }
    }

    public class ProviderTests<TProvider>
        where TProvider : IBlobProvider
    {
        protected readonly TProvider Provider;

        #region .ctor

        protected ProviderTests(string connectionString)
        {
            Provider = Storage.GetProvider<TProvider>(connectionString);
        }

        #endregion

        #region Internals

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        protected void Trace(string format, params object[] args)
        {
            Debug.WriteLine(string.Format(format, args));
            this.TestContext.WriteLine(format, args);
        }

        #endregion

        private static TProvider _provider;
        private static WebRequestLog _log;

        protected void TestInitialize()
        {
            var listener = BlobStorage.Trace.EnsureListener<TestTraceListener>();
            TestTraceListener.Context = TestContext;

            WebRequestMonitor.Enable();
            BandwithMonitor.Enable();
            _log = new WebRequestLog();
            _provider = this.Provider;
        }

        protected void TestCleanup()
        {
            var billing = _provider as IStorageBilling;
            if (billing != null)
            {
                var records = _log.GetAllRecords().Repeat(1.Million() + 10.Kilo(), true);
                var bill = billing.ComputeBilling(records);
                var price = bill.Price;
            }
            _log.Dispose();
            _log = null;
        }

        #region Blob Container Tests

        static readonly string TestContainerName;

        static ProviderTests()
        {
            TestContainerName = "cloud4net-test-" + Environment.TickCount;
        }

        // 0 - get container list
        protected void Test00_ContainerList()
        {
            var containers = Provider.Containers.FindAll().ToArray();
            //if (Provider is AWSProvider)
            //    foreach (var container in containers)
            //    {
            //        if (container.Name.StartsWith("cloud4net-test-"))
            //        {
            //            Trace("Deleting {0}", container.Name);
            //            container.Delete();
            //        }
            //    }
        }

        // 1 - create container if not exists
        protected void Test01_CreateContainerIfNotExists()
        {
            var container = Provider.Containers[TestContainerName];
            container.CreateIfNotExists();
        }

        // 1a - list blobs
        protected void Test01a_ListBlobs()
        {
            var container = Provider.Containers[TestContainerName];
            var blobs = container.Blobs.Find(BlobSelect.All).ToArray();
        }

        // 2 - create container if not exists (should not throw any exception)
        protected void Test02_CreateContainerIfNotExist2()
        {
            var container = Provider.Containers[TestContainerName];
            container.CreateIfNotExists();
        }

        // 3 - tests if container exists (should be true)
        protected void Test03_ContainerExists()
        {
            var container = Provider.Containers[TestContainerName];
            Assert.IsTrue(container.Exists, "Exists should be true after CreateIfNotExists()");
        }

        // 4 - create container again (should throw an exception)
        [ExpectedException(typeof(ContainerAlreadyExistsException))]
        protected void Test04_CreateContainer()
        {
            var container = Provider.Containers[TestContainerName];
            container.Create();
        }

        // 5 - delete container if exists
        protected void Test05_DeleteContainerIfExists()
        {
            var container = Provider.Containers[TestContainerName];
            container.DeleteIfExists();
            Assert.IsFalse(container.Exists, "container still exists after DeleteIfExists()");
        }

        // 6 - delete container (should throw an exception)
        [ExpectedException(typeof(ContainerDoesNotExistsException))]
        protected void Test06_DeleteContainer()
        {
            var container = Provider.Containers[TestContainerName];
            container.Delete();
            Assert.IsFalse(container.Exists, "container still exists after Delete()");
        }

        // 7 - tests if container exists (should be false)
        protected void Test07_ContainerDoesNotExists()
        {
            var container = Provider.Containers[TestContainerName];
            Assert.IsFalse(container.Exists, "Exists should be false after Delete()");
        }

        // 8 - delete container if exists (should not throw an exception)
        protected void Test08_DeleteContainerIfExists2()
        {
            var container = Provider.Containers[TestContainerName];
            container.DeleteIfExists();
            Assert.IsFalse(container.Exists, "container still exists after DeleteIfExists()");
        }

        #endregion
    }
}