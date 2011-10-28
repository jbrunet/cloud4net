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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.StorageModel.Configuration;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace System.StorageModel.AWS
{
    partial class AWSProvider : IQueueProvider
    {
        public AmazonSQSConfig SQSConfig { get; private set; }
        public AmazonSQS SQSClient { get; private set; }
        public AWSQueueCollection Queues { get; private set; }

        #region .ctor

        partial void InitializeQueues(NameValueCollection config)
        {
            SQSConfig = new AmazonSQSConfig();
            config.OnKey("ServiceURL", value =>
            {
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                    value = uri.Host;
                SQSConfig.ServiceURL = value;
            });
            config.OnKey("UserAgent", value => S3Config.UserAgent = value);
            config.OnKey("MaxErrorRetry", value => S3Config.MaxErrorRetry = int.Parse(value));
            config.OnKey("ProxyURL", value =>
            {
                var uri = new Uri(value, UriKind.Absolute);
                SQSConfig.ProxyHost = uri.Host;
                SQSConfig.ProxyPort = uri.Port;
            });
            SQSClient = new AmazonSQSClient(_accessKeyId, _secretAccessKey, SQSConfig);
            Queues = new AWSQueueCollection(this);
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
            return new AWSQueue(this, name, config);
        }

        #endregion
    }

    public sealed class AWSQueueCollection : MessageQueueCollection<AWSProvider, AWSQueue>
    {
        #region .ctor

        internal AWSQueueCollection(AWSProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AWSQueue> FindAll(string prefix)
        {
            var req = new ListQueuesRequest
                          {
                              QueueNamePrefix = prefix
                          };
            using (Provider.LogQueueRequests(null))
            {
                var res = Provider.SQSClient.ListQueues(req);
                foreach (var queueUrl in res.ListQueuesResult.QueueUrl)
                    yield return new AWSQueue(Provider, queueUrl, null);
            }
        }
    }

    public sealed class AWSQueue : MessageQueue<AWSProvider, AWSQueueMessage>
    {
        #region .ctor

        internal AWSQueue(AWSProvider provider, string queueUrl, StorageResourceDefinition config)
            : base(provider, queueUrl)
        {
        }

        #endregion

        public bool? Found { get; private set; }

        public override bool Exists
        {
            get
            {
                if (!Found.Value)
                    Found = Provider.Queues.FindAll(null).Any(queue => queue.Name == Name);
                return Found.Value;
            }
        }

        public override void Create()
        {
            var req = new CreateQueueRequest
                          {
                              QueueName = this.Name
                          };
            using (this.LogQueueRequests())
            {
                var res = Provider.SQSClient.CreateQueue(req);
            }
            Found = true;
        }

        public override void Delete()
        {
            var req = new DeleteQueueRequest { QueueUrl = this.Name };
            using (this.LogQueueRequests())
            {
                var res = Provider.SQSClient.DeleteQueue(req);
            }
            Found = false;
        }

        public override AWSQueueMessage NewMessage()
        {
            return new AWSQueueMessage(this);
        }

        public override IEnumerable<AWSQueueMessage> Dequeue(int take, TimeSpan visibilityTimeout)
        {
            var req = new ReceiveMessageRequest
                          {
                              MaxNumberOfMessages = take,
                              QueueUrl = this.Name,
                              VisibilityTimeout = (decimal)visibilityTimeout.TotalSeconds,
                          };
            using (this.LogQueueRequests())
            {
                var res = Provider.SQSClient.ReceiveMessage(req);
                foreach (var msg in res.ReceiveMessageResult.Message)
                    yield return new AWSQueueMessage(this) { Body = msg.Body, ID = msg.MessageId };
            }
        }
    }

    public sealed class AWSQueueMessage : QueueMessage<AWSQueue>
    {
        #region .ctor

        internal AWSQueueMessage(AWSQueue queue)
            : base(queue)
        {
        }

        #endregion

        public override void Enqueue()
        {
            var req = new SendMessageRequest
                          {
                              MessageBody = this.Body,
                              QueueUrl = Queue.Name,
                          };
            using (this.LogQueueRequests())
            {
                var res = Queue.Provider.SQSClient.SendMessage(req);
                this.ID = res.SendMessageResult.MessageId;
            }
        }
    }
}