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

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Microsoft.WindowsAzure.StorageClient;

namespace System.StorageModel.WindowsAzure
{
    using Configuration;

    partial class AzureProvider : IQueueProvider
    {
        public CloudQueueClient QueueClient { get; private set; }
        public AzureQueueCollection Queues { get; private set; }

        #region .ctor

        partial void InitializeQueues(NameValueCollection config)
        {
            QueueClient = Account.CreateCloudQueueClient();
            Queues = new AzureQueueCollection(this);
            QueueStorage.Initialize();
        }

        #endregion

        #region IQueueProvider

        IMessageQueueCollection IQueueProvider.Queues
        {
            get { return this.Queues; }
        }

        IMessageQueue IQueueProvider.NewQueue(string name, StorageResourceDefinition config)
        {
            return new AzureQueue(this, name, config);
        }

        #endregion
    }

    public sealed class AzureQueueCollection : MessageQueueCollection<AzureProvider, AzureQueue>
    {
        #region .ctor

        internal AzureQueueCollection(AzureProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AzureQueue> FindAll(string prefix)
        {
            using (Provider.LogQueueRequests(null))
                foreach (var impl in Provider.QueueClient.ListQueues(prefix))
                    yield return new AzureQueue(Provider, impl);
        }
    }

    public sealed class AzureQueue : MessageQueue<AzureProvider, AzureQueueMessage>
    {
        public CloudQueue Impl { get; private set; }

        #region .ctor

        internal AzureQueue(AzureProvider provider, CloudQueue impl)
            : base(provider, impl.Name)
        {
            Impl = impl;
        }

        internal AzureQueue(AzureProvider provider, string name, StorageResourceDefinition config)
            : this(provider, provider.QueueClient.GetQueueReference(name))
        {
        }

        #endregion

        public bool? Found { get; private set; }

        public override bool Exists
        {
            get
            {
                if (!Found.Value)
                {
                    using (this.LogQueueRequests())
                    {
                        var queue = Provider.QueueClient.ListQueues(Name).FirstOrDefault();
                        Found = (queue != null);
                    }
                }
                return Found.Value;
            }
        }

        public override void Create()
        {
            using (this.LogQueueRequests())
                Impl.Create();
            Found = true;
        }

        public override void CreateIfNotExist()
        {
            using (this.LogQueueRequests())
                Impl.CreateIfNotExist();
            Found = true;
        }

        public override void Delete()
        {
            using (this.LogQueueRequests())
                Impl.Delete();
            Found = false;
        }

        public override AzureQueueMessage NewMessage()
        {
            return new AzureQueueMessage(this);
        }

        public override IEnumerable<AzureQueueMessage> Dequeue(int take, TimeSpan visibilityTimeout)
        {
            using (this.LogQueueRequests())
                foreach (var msg in Impl.GetMessages(take, visibilityTimeout))
                {
                    var m = new AzureQueueMessage(this) { Body = msg.AsString, ID = msg.Id };
                    yield return m;
                }
        }
    }

    public sealed class AzureQueueMessage : QueueMessage<AzureQueue>
    {
        #region .ctor

        internal AzureQueueMessage(AzureQueue queue)
            : base(queue)
        {
        }

        #endregion

        public override void Enqueue()
        {
            var msg = new CloudQueueMessage(Body);
            using (this.LogQueueRequests())
                Queue.Impl.AddMessage(msg);
            this.ID = msg.Id;
        }
    }
}