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

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.Services.Client;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using Microsoft.WindowsAzure.StorageClient;

namespace System.StorageModel.WindowsAzure
{
    using Configuration;

    partial class AzureProvider : ITableProvider
    {
        public CloudTableClient TableClient { get; private set; }
        public AzureTableCollection Tables { get; private set; }

        #region .ctor

        partial void InitializeTables(NameValueCollection config)
        {
            TableClient = Account.CreateCloudTableClient();
            Tables = new AzureTableCollection(this);
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
            return new AzureTable(this, name, config);
        }

        public IDisposable GetTrackingContext()
        {
            return new AzureTrackingContext(TableClient.GetDataServiceContext());
        }

        #endregion
    }

    internal sealed class AzureTrackingContext : IDisposable
    {
        private readonly TableServiceContext _prev;
        [ThreadStatic]
        internal static TableServiceContext _context;

        internal AzureTrackingContext(TableServiceContext context)
        {
            _prev = _context;
            _context = context;
        }

        public void Dispose()
        {
            _context = _prev;
        }
    }

    public class AzureTableCollection : TableCollection<AzureProvider, AzureTable>
    {
        #region .ctor

        internal AzureTableCollection(AzureProvider provider)
            : base(provider)
        {
        }

        #endregion

        public override IEnumerable<AzureTable> FindAll()
        {
            using (Provider.LogTableRequests(null))
                foreach (var table in Provider.TableClient.ListTables())
                    yield return new AzureTable(Provider, table, null);
        }
    }

    public class AzureTable : Table<AzureProvider>
    {
        #region .ctor

        internal AzureTable(AzureProvider provider, string name, StorageResourceDefinition config)
            : base(provider, name)
        {
        }

        #endregion

        public bool? Found { get; private set; }

        public override bool Exists
        {
            get
            {
                if (!Found.HasValue)
                {
                    using (this.LogTableRequests())
                        Found = Provider.TableClient.DoesTableExist(Name);
                }
                return Found.Value;
            }
        }

        public override ITable<T> Map<T>()
        {
            return new AzureTable<T>(this);
        }

        public override void Create()
        {
            using (this.LogTableRequests())
                Provider.TableClient.CreateTable(Name);
            Found = true;
            this.OnTableCreated();
        }

        public override void Delete()
        {
            using (this.LogTableRequests())
                Provider.TableClient.DeleteTable(Name);
            Found = false;
            this.OnTableDeleted();
        }

        public override void CreateIfNotExist()
        {
            using (this.LogTableRequests())
                Provider.TableClient.CreateTableIfNotExist(Name);
            Found = true;
            this.OnTableCreated();
        }

        public override void DeleteIfExist()
        {
            using (this.LogTableRequests())
                Provider.TableClient.DeleteTableIfExist(Name);
            Found = false;
            this.OnTableDeleted();
        }
    }

    public class AzureTable<T> : AzureTable, ITable<T>
    {
        #region .ctor

        public AzureTable(AzureTable table)
            : base(table.Provider, table.Name, null)
        {
        }

        #endregion

        #region ITable<T> Members

        public T Find(Expression<Func<T, bool>> filter)
        {
            var value = FindAll(filter).FirstOrDefault();
            this.OnFind();
            return value;
        }

        public IEnumerable<T> FindAll(Expression<Func<T, bool>> filter)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            IQueryable<T> query = dc.CreateQuery<T>(Name);
            if (filter != null)
                query = query.Where(filter);
            var dsq = query.AsTableServiceQuery();
            if (dsq == null)
                return query.AsEnumerable();
            var results = dsq.Execute();
            return new AzureResultEnumerable<T>(this, results);
        }

        public void Attach(T entity)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            dc.AttachTo(Name, entity, "*");
        }

        public void Add(T entity)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            dc.AddObject(Name, entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            this.OnEntityCreated(entity);
        }

        public void Add(params T[] entities)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            foreach (var entity in entities)
                dc.AddObject(Name, entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            foreach (var entity in entities)
                this.OnEntityCreated(entity);
        }

        public void Delete(T entity)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            dc.DeleteObject(entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            this.OnEntityDeleted(entity);
        }

        public void Delete(params T[] entities)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            foreach (var entity in entities)
                dc.DeleteObject(entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            foreach (var entity in entities)
                this.OnEntityDeleted(entity);
        }

        public void Update(T entity)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            dc.UpdateObject(entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            this.OnEntityUpdated(entity);
        }

        public void Update(params T[] entities)
        {
            var dc = AzureTrackingContext._context ?? Provider.TableClient.GetDataServiceContext();
            foreach (var entity in entities)
                dc.UpdateObject(entity);
            using (this.LogTableRequests())
                dc.SaveChangesWithRetries();
            foreach (var entity in entities)
                this.OnEntityUpdated(entity);
        }

        #endregion
    }

    #region Workaround for empty azure table enumerator

    internal class AzureResultEnumerable<T> : IEnumerable<T>
    {
        private readonly AzureTable<T> _table;
        private readonly IEnumerable<T> _inner;

        #region .ctor

        internal AzureResultEnumerable(AzureTable<T> table, IEnumerable<T> inner)
        {
            _table = table;
            _inner = inner;
        }

        #endregion

        public IEnumerator<T> GetEnumerator()
        {
            return new AzureResultEnumerator<T>(_table, _inner.GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    internal class AzureResultEnumerator<T> : IEnumerator<T>
    {
        private readonly AzureTable<T> _table;
        private readonly IEnumerator<T> _inner;

        #region .ctor

        internal AzureResultEnumerator(AzureTable<T> table, IEnumerator<T> inner)
        {
            _table = table;
            _inner = inner;
        }

        #endregion

        public T Current
        {
            get { return _inner.Current; }
        }

        public void Dispose()
        {
            _inner.Dispose();
        }

        object IEnumerator.Current
        {
            get { return _inner.Current; }
        }

        delegate Exception TranslateDataServiceClientExceptionDelegate(InvalidOperationException e);

        private static readonly TranslateDataServiceClientExceptionDelegate TranslateDataServiceClientException;

        static AzureResultEnumerator()
        {
            // unfortunately MS has made this method internal, so we find it through reflection
            var utilities = typeof(CloudTableClient).Assembly.GetType("Microsoft.WindowsAzure.StorageClient.Utilities");
            TranslateDataServiceClientException = (TranslateDataServiceClientExceptionDelegate)
                                                  Delegate.CreateDelegate(
                                                      typeof(TranslateDataServiceClientExceptionDelegate), utilities,
                                                      "TranslateDataServiceClientException", true, true);
        }

        public bool MoveNext()
        {
            try
            {
                using (_table.LogTableRequests())
                    return _inner.MoveNext();
            }
            catch (DataServiceQueryException ex)
            {
                if (ex.Response.StatusCode == (int)HttpStatusCode.NotFound)
                    return false;
                throw TranslateDataServiceClientException(ex);
            }
            catch (InvalidOperationException ex)
            {
                var dsc = ex.InnerException as DataServiceClientException;
                if (dsc != null)
                {
                    if (dsc.StatusCode == (int)HttpStatusCode.NotFound)
                        return false;
                }
                throw TranslateDataServiceClientException(dsc);
            }
        }

        public void Reset()
        {
            _inner.Reset();
        }
    }

    #endregion
}