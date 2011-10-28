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
using System.Data.Services.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.StorageModel.Configuration;
using System.StorageModel.Reflection;
using Amazon.SimpleDB;
using Amazon.SimpleDB.Model;

namespace System.StorageModel.AWS
{
    partial class AWSProvider : ITableProvider
    {
        public AmazonSimpleDBConfig SimpleDBConfig { get; private set; }
        public AmazonSimpleDB SimpleDBClient { get; private set; }
        public AWSTableCollection Tables { get; private set; }

        #region .ctor

        partial void InitializeTables(NameValueCollection config)
        {
            SimpleDBConfig = new AmazonSimpleDBConfig();
            config.OnKey("ServiceURL", value =>
            {
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                    value = uri.Host;
                SimpleDBConfig.ServiceURL = value;
            });
            config.OnKey("UserAgent", value => SimpleDBConfig.UserAgent = value);
            config.OnKey("MaxErrorRetry", value => SimpleDBConfig.MaxErrorRetry = int.Parse(value));
            config.OnKey("ProxyURL", value =>
            {
                var uri = new Uri(value, UriKind.Absolute);
                SimpleDBConfig.ProxyHost = uri.Host;
                SimpleDBConfig.ProxyPort = uri.Port;
            });
            SimpleDBClient = new AmazonSimpleDBClient(_accessKeyId, _secretAccessKey, SimpleDBConfig);
            Tables = new AWSTableCollection(this);
            TableStorage.Initialize();
        }

        #endregion

        #region ITableProvider

        ITableCollection ITableProvider.Tables
        {
            get { return this.Tables; }
        }

        ITable ITableProvider.NewTable(string name, StorageResourceDefinition config)
        {
            return new AWSTable(this, name, config);
        }

        public IDisposable GetTrackingContext()
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    public sealed class AWSTableCollection : TableCollection<AWSProvider, AWSTable>
    {
        #region .ctor

        internal AWSTableCollection(AWSProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AWSTable> FindAll()
        {
            using (Provider.LogTableRequests(null))
            {
                var req = new ListDomainsRequest
                              {
                              };
                var res = Provider.SimpleDBClient.ListDomains(req);
                foreach (var domain in res.ListDomainsResult.DomainName)
                    yield return new AWSTable(Provider, domain, null);
            }
        }
    }

    public class AWSTable : Table<AWSProvider>
    {
        #region .ctor

        internal AWSTable(AWSProvider provider, string domain, StorageResourceDefinition config)
            : base(provider, domain)
        {
        }

        #endregion

        public bool? Found { get; private set; }

        public override bool Exists
        {
            get
            {
                if (!Found.Value)
                    Found = Provider.Tables.FindAll().Any(table => table.Name == Name);
                return Found.Value;
            }
        }

        public override void Create()
        {
            var req = new CreateDomainRequest
                          {
                              DomainName = this.Name,
                          };
            using (this.LogTableRequests())
            {
                var res = Provider.SimpleDBClient.CreateDomain(req);
            }
            Found = true;
            this.OnTableCreated();
        }

        public override void Delete()
        {
            var req = new DeleteDomainRequest
                          {
                              DomainName = Name,
                          };
            using (this.LogTableRequests())
            {
                var res = Provider.SimpleDBClient.DeleteDomain(req);
            }
            Found = false;
            this.OnTableDeleted();
        }

        public override ITable<T> Map<T>()
        {
            return new AWSTable<T>(this);
        }
    }

    public class AWSTable<T> : AWSTable, ITable<T>
    {
        private static readonly Dictionary<string, PropertyInfo> _map;
        private static readonly DataServiceKeyAttribute _keyAttr;

        static AWSTable()
        {
            var props = typeof (T).GetProperties();
            _map = props.ToDictionary(prop => prop.Name);

            if (!typeof (T).TryGetCustomAttribute(true, out _keyAttr))
                throw new Exception("[DataServiceKey] attribute required");
        }

        #region .ctor

        public AWSTable(AWSTable table)
            : base(table.Provider, table.Name, null)
        {
        }

        #endregion

        #region ITable<T> Members

        public T Find(Expression<Func<T, bool>> filter)
        {
            return FindAll(filter).Single();
        }

        public IEnumerable<T> FindAll(Expression<Func<T, bool>> filter)
        {
            var req = new SelectRequest
                          {
                              SelectExpression = "",
                          };
            using (Provider.LogTableRequests(null))
            {
                var res = Provider.SimpleDBClient.Select(req);
                foreach (var item in res.SelectResult.Item)
                {
                    var entity = Activator.CreateInstance<T>();
                    foreach (var attribute in item.Attribute)
                    {
                        PropertyInfo prop;
                        if (_map.TryGetValue(attribute.Name, out prop))
                            prop.SetValue(entity, Convert.ChangeType(attribute.Value, prop.PropertyType), null);
                    }
                    yield return entity;
                }
            }
        }

        public void Attach(T entity)
        {
            throw new NotImplementedException();
        }

        private static string GetItemNameAndAttributes<TAttr>(T entity, List<TAttr> attributes, Func<string, string, TAttr> createAttr)
        {
            var itemName = string.Join(";",
                                       _keyAttr.KeyNames.Select(
                                           keyName => _map[keyName].GetValue(entity, null).ToString()).
                                           ToArray());
            foreach (var prop in _map.Values)
                attributes.Add(createAttr(prop.Name,
                                          (string) Convert.ChangeType(prop.GetValue(entity, null), TypeCode.String)));
            return itemName;
        }

        private static string GetItemNameAndAttributes(T entity, List<ReplaceableAttribute> attributes)
        {
            return GetItemNameAndAttributes(entity, attributes,
                                            (name, value) =>
                                            new ReplaceableAttribute {Name = name, Replace = true, Value = value});
        }

        private static string GetItemNameAndAttributes(T entity, List<Amazon.SimpleDB.Model.Attribute> attributes)
        {
            return GetItemNameAndAttributes(entity, attributes,
                                            (name, value) =>
                                            new Amazon.SimpleDB.Model.Attribute { Name = name, Value = value });
        }

        public void Add(T entity)
        {
            Update(entity);
        }

        public void Add(params T[] entities)
        {
            Update(entities);
        }

        public void Delete(T entity)
        {
            var req = new DeleteAttributesRequest();
            req.ItemName = GetItemNameAndAttributes(entity, req.Attribute);
            using (this.LogTableRequests())
            {
                var res = Provider.SimpleDBClient.DeleteAttributes(req);
            }
        }

        public void Delete(params T[] entities)
        {
            foreach(var entity in entities)
                Delete(entity);
        }

        public void Update(T entity)
        {
            var req = new PutAttributesRequest
            {
                DomainName = Name,
            };
            req.ItemName = GetItemNameAndAttributes(entity, req.Attribute);
            using (this.LogTableRequests())
            {
                var res = Provider.SimpleDBClient.PutAttributes(req);
            }
        }

        public void Update(params T[] entities)
        {
            var req = new BatchPutAttributesRequest
            {
                DomainName = Name,
            };
            foreach (var entity in entities)
            {
                var item = new ReplaceableItem();
                item.ItemName = GetItemNameAndAttributes(entity, item.Attribute);
                req.Item.Add(item);
            }
            using (this.LogTableRequests())
            {
                var res = Provider.SimpleDBClient.BatchPutAttributes(req);
            }
        }

        #endregion
    }
}