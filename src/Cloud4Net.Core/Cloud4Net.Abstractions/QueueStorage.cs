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
using System.Configuration;
using System.IO;
using System.Linq;
using System.StorageModel.Diagnostics;
using System.Xml.Serialization;

namespace System.StorageModel
{
    #region Configuration

    using Configuration;

    namespace Configuration
    {
        public sealed class QueueStorageSection : StorageSection
        {
            private const string QueuesProperty = "";
            [ConfigurationProperty(QueuesProperty, IsDefaultCollection = true, IsRequired = true)]
            [ConfigurationCollection(typeof(StorageResourceDefinition), AddItemName = "queue")]
            public StorageResourceDefinitionCollection Queues
            {
                get { return (StorageResourceDefinitionCollection)this[QueuesProperty]; }
            }
        }
    }

    #endregion

    #region Interfaces

    public interface IQueueProvider : IStorageService
    {
        IMessageQueueCollection Queues { get; }
        IMessageQueue NewQueue(string name, StorageResourceDefinition config);
    }

    public interface IMessageQueueCollection : IEnumerable<IMessageQueue>
    {
        IMessageQueue this[string name] { get; set; }
    }

    public interface IMessageQueue
    {
        IQueueProvider Provider { get; }
        string Name { get; }
        bool Exists { get; }
        void Create();
        void Delete();
        void CreateIfNotExist();
        void DeleteIfExist();
        IQueueMessage NewMessage();
        IEnumerable<IQueueMessage> Dequeue(int take, TimeSpan visibilityTimeout);
    }

    public interface IQueueMessage
    {
        IMessageQueue Queue { get; }
        string ID { get; }
        string Body { get; set; }
        void Enqueue();
    }

    #endregion

    #region Base classes

    public abstract class MessageQueue<TProvider, TMessage> : IMessageQueue
        where TProvider : IQueueProvider
        where TMessage : IQueueMessage
    {
        #region .ctor

        protected MessageQueue(TProvider provider, string name)
        {
            this.Provider = provider;
            this.Name = name;
        }

        #endregion

        public override string ToString()
        {
            return Name;
        }

        public TProvider Provider { get; protected set; }
        public string Name { get; protected set; }

        public abstract bool Exists { get; }
        public abstract void Create();
        public abstract void Delete();
        public virtual void CreateIfNotExist()
        {
            if (!Exists)
                Create();
        }
        public virtual void DeleteIfExist()
        {
            if (Exists)
                Delete();
        }

        public abstract TMessage NewMessage();
        public abstract IEnumerable<TMessage> Dequeue(int take, TimeSpan visibilityTimeout);

        #region IMessageQueue

        IQueueProvider IMessageQueue.Provider
        {
            get { return this.Provider; }
        }


        IQueueMessage IMessageQueue.NewMessage()
        {
            return this.NewMessage();
        }

        IEnumerable<IQueueMessage> IMessageQueue.Dequeue(int take, TimeSpan visibilityTimeout)
        {
            return this.Dequeue(take, visibilityTimeout).Cast<IQueueMessage>();
        }

        #endregion
    }

    public abstract class MessageQueueCollection<TProvider, TQueue> : Dictionary<string, TQueue>, IMessageQueueCollection
        where TProvider : IQueueProvider
        where TQueue : IMessageQueue
    {
        #region .ctor

        protected MessageQueueCollection(TProvider provider)
        {
            this.Provider = provider;
        }

        #endregion

        public TProvider Provider { get; set; }

        public abstract IEnumerable<TQueue> FindAll(string prefix);

        public new TQueue this[string name]
        {
            get
            {
                TQueue container;
                if (!TryGetValue(name, out container))
                    container = (TQueue)Provider.NewQueue(name, null);
                return container;
            }
            set { base[name] = value; }
        }

        #region IMessageQueueCollection

        IMessageQueue IMessageQueueCollection.this[string name]
        {
            get { return this[name]; }
            set { this[name] = (TQueue)value; }
        }

        public new IEnumerator<TQueue> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator<IMessageQueue> IEnumerable<IMessageQueue>.GetEnumerator()
        {
            return new Enumerator<TQueue, IMessageQueue>(Values);
        }

        #endregion
    }

    public abstract class QueueMessage<TQueue> : IQueueMessage
        where TQueue : IMessageQueue
    {
        #region .ctor

        protected QueueMessage(TQueue queue)
        {
            this.Queue = queue;
        }

        #endregion

        public TQueue Queue { get; protected set; }
        public string ID { get; set; }
        public string Body { get; set; }

        public abstract void Enqueue();

        #region IQueueMessage

        IMessageQueue IQueueMessage.Queue
        {
            get { return this.Queue; }
        }

        #endregion
    }

    #endregion

    #region Diagnostics

    namespace Diagnostics
    {
        public class QueueRequestLog : WebRequestLog
        {
            public IMessageQueue Queue { get; set; }
            public IQueueMessage Message { get; set; }
        }
    }

    #endregion

    public static class QueueStorage
    {
        internal class MessageQueueCollection : MessageQueueCollection<IQueueProvider, IMessageQueue>
        {
            #region .ctor

            internal MessageQueueCollection()
                : base(null)
            {
            }

            #endregion


            public override IEnumerable<IMessageQueue> FindAll(string prefix)
            {
                throw new NotSupportedException();
            }
        }

        private static bool _initialized;
        private static QueueStorageSection _configurationSection;
        private static MessageQueueCollection _queues;

        public static void Initialize()
        {
            if (_initialized)
                return;
            lock (typeof(TableStorage))
            {
                if (_initialized)
                    return;

                _configurationSection = StorageSection.Load<QueueStorageSection>("system.storageModel/queues");
                _queues = new MessageQueueCollection();

                _initialized = true;

                var defaultProvider = string.IsNullOrEmpty(ConfigurationSection.DefaultProvider)
                                          ? null
                                          : Storage.GetProvider<IQueueProvider>(ConfigurationSection.DefaultProvider);
                _queues.Provider = defaultProvider;
                foreach (StorageResourceDefinition queueDef in ConfigurationSection.Queues)
                {
                    var provider = string.IsNullOrEmpty(queueDef.Provider)
                                       ? defaultProvider
                                       : Storage.GetProvider<IQueueProvider>(queueDef.Provider);
                    if (provider == null)
                        throw new ConfigurationErrorsException("At least a default queue provider must be defined");
                    var queue = provider.NewQueue(queueDef.Name, queueDef);
                    queue.CreateIfNotExist();
                    Queues[queueDef.Name] = queue;
                    provider.Queues[queueDef.Name] = queue;
                }
            }
        }

        public static QueueStorageSection ConfigurationSection
        {
            get
            {
                Initialize();
                return _configurationSection;
            }
        }

        public static IMessageQueueCollection Queues
        {
            get
            {
                Initialize();
                return _queues;
            }
        }

        public static IQueueProvider GetProvider(string nameOrConnectionString)
        {
            return Storage.GetProvider<IQueueProvider>(nameOrConnectionString);
        }

        #region Extension Methods

        public static void Push<T>(this IMessageQueue queue, T content)
        {
            var s = content as string;
            if (s == null)
            {
                var ser = new XmlSerializer(typeof(T));
                using (var sw = new StringWriter())
                {
                    ser.Serialize(sw, content);
                    s = sw.GetStringBuilder().ToString();
                }
            }
            var msg = queue.NewMessage();
            msg.Body = s;
            msg.Enqueue();
        }

        public static QueueRequestLog LogQueueRequests(this IQueueProvider provider, Action<QueueRequestLog> create)
        {
            return provider.Log(create);
        }

        public static WebRequestLog LogQueueRequests(this IMessageQueue queue)
        {
            return queue.Provider.LogQueueRequests(log => log.Queue = queue);
        }

        public static WebRequestLog LogQueueRequests(this IQueueMessage message)
        {
            return message.Queue.Provider.LogQueueRequests(log =>
            {
                log.Queue = message.Queue;
                log.Message = message;
            });
        }

        #endregion
    }
}