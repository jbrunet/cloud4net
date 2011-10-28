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
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.StorageModel.Diagnostics;

namespace System.StorageModel
{
    #region Configuration

    using Configuration;

    namespace Configuration
    {
        public sealed class TableStorageSection : StorageSection
        {
            private const string TablesProperty = "";
            [ConfigurationProperty(TablesProperty, IsDefaultCollection = true, IsRequired = true)]
            [ConfigurationCollection(typeof(StorageResourceDefinition), AddItemName = "table")]
            public StorageResourceDefinitionCollection Tables
            {
                get { return (StorageResourceDefinitionCollection)this[TablesProperty]; }
            }
        }
    }

    //public class Table<T>
    //{
    //    internal Table _table;

    //    public Table<T> Add(T entity)
    //    {
    //        _table.Provider.Add(_table, entity);
    //        return this;
    //    }

    //    public Table<T> Add(params T[] entities)
    //    {
    //        _table.Provider.Add(_table, entities);
    //        return this;
    //    }

    //    public IEnumerable<T> FindAll(Expression<Func<T, bool>> filter)
    //    {
    //        return _table.Provider.FindAll(_table, filter);
    //    }

    //    public T Find(Expression<Func<T, bool>> filter)
    //    {
    //        return _table.Provider.Find(_table, filter);
    //    }
    //}

    #endregion

    #region Interfaces

    public interface ITableProvider : IStorageService
    {
        ITable NewTable(string name, StorageResourceDefinition config);
        ITableCollection Tables { get; }
        IDisposable GetTrackingContext();
    }

    public interface ITableCollection : IEnumerable<ITable>
    {
        IEnumerable<ITable> FindAll();
        ITable this[string name] { get; set; }
    }

    public interface ITable
    {
        string Name { get; }
        ITableProvider Provider { get; }
        ITable<T> Map<T>();
        bool Exists { get; }
        void Create();
        void CreateIfNotExist();
        void Delete();
        void DeleteIfExist();
    }

    public interface ITable<T> : ITable
    {
        T Find(Expression<Func<T, bool>> filter);
        IEnumerable<T> FindAll(Expression<Func<T, bool>> filter);
        void Attach(T entity);
        void Add(T entity);
        void Add(params T[] entities);
        void Delete(T entity);
        void Delete(params T[] entities);
        void Update(T entity);
        void Update(params T[] entities);
    }

    #endregion

    #region Base classes

    public abstract class Table<TProvider> : ITable
        where TProvider : ITableProvider
    {
        #region .ctor

        protected Table(TProvider provider, string name)
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
        public abstract ITable<T> Map<T>();
        public abstract void Create();
        public virtual void CreateIfNotExist()
        {
            if (!Exists)
                Create();
        }
        public abstract void Delete();
        public virtual void DeleteIfExist()
        {
            if (Exists)
                Delete();
        }

        #region ITableProvider

        ITableProvider ITable.Provider
        {
            get { return this.Provider; }
        }

        #endregion
    }

    public abstract class TableCollection<TProvider, TTable> : Dictionary<string, TTable>, ITableCollection
        where TProvider : ITableProvider
        where TTable : ITable
    {
        #region .ctor

        protected TableCollection(TProvider provider)
        {
            this.Provider = provider;
        }

        #endregion

        public TProvider Provider { get; set; }

        public abstract IEnumerable<TTable> FindAll();

        public new IEnumerator<TTable> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        public new TTable this[string name]
        {
            get
            {
                TTable table;
                if (!TryGetValue(name, out table))
                    table = (TTable)Provider.NewTable(name, null);
                return table;
            }
            set { base[name] = value; }
        }

        #region ITableCollection

        IEnumerable<ITable> ITableCollection.FindAll()
        {
            return this.FindAll().Cast<ITable>();
        }

        IEnumerator<ITable> IEnumerable<ITable>.GetEnumerator()
        {
            return new Enumerator<TTable, ITable>(Values);
        }

        ITable ITableCollection.this[string name]
        {
            get { return this[name]; }
            set { this[name] = (TTable)value; }
        }

        #endregion
    }

    #endregion

    #region Diagnostics

    namespace Diagnostics
    {
        public class TableRequestLog : WebRequestLog
        {
            public ITable Table { get; set; }
        }
    }

    partial class TableStorage
    {
        public static TraceSource Trace { get; private set; }

        public static void OnListingTables(this ITableProvider provider)
        {
            Trace.TraceInformation("{0}: LIST tables", provider.Name);
        }

        public static void OnTableExists(this ITable table, bool found)
        {
            Trace.TraceInformation("{0}: table={1} EXISTS={2}", table.Provider.Name,
                                   table.Name, found);
        }

        public static void OnTableCreated(this ITable table)
        {
            Trace.TraceInformation("{0}: table={1} CREATED -> T={2}ms", table.Provider.Name, table.Name, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        public static void OnTableDeleted(this ITable table)
        {
            Trace.TraceInformation("{0}: table={1} DELETED -> T={2}ms", table.Provider.Name, table.Name, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        //public static void OnListingBlobs(this IBlobContainer container)
        //{
        //    Trace.TraceInformation("{0}: container={1} LIST blobs", container.Provider.Name, container.Name);
        //}

        public static void OnFind(this ITable table)
        {
            Trace.TraceInformation("{0}: table={1} FIND -> T={2}ms", table.Provider.Name, table.Name, Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        public static void OnEntityCreated<T>(this ITable<T> table, T entity)
        {
            Trace.TraceInformation("{0}: table={1} entity={2} CREATED -> T={3}ms", table.Provider.Name, table.Name,
                                   entity.ToString(), Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        public static void OnEntityUpdated<T>(this ITable<T> table, T entity)
        {
            Trace.TraceInformation("{0}: table={1} entity={2} UPDATED -> T={3}ms", table.Provider.Name, table.Name,
                                   entity.ToString(), Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }

        public static void OnEntityDeleted<T>(this ITable<T> table, T entity)
        {
            Trace.TraceInformation("{0}: table={1} entity={2} DELETED -> T={3}ms", table.Provider.Name, table.Name,
                                   entity.ToString(), Storage.LastRequestLog.ElapsedTime.TotalMilliseconds);
        }
    }

    #endregion

    public static partial class TableStorage
    {
        internal class TableCollection : TableCollection<ITableProvider, ITable>
        {
            #region .ctor

            internal TableCollection()
                : base(null)
            {
            }

            #endregion

            public override IEnumerable<ITable> FindAll()
            {
                throw new NotSupportedException();
            }
        }

        private static bool _initialized;
        private static TableStorageSection _configurationSection;
        private static TableCollection _tables;

        public static void Initialize()
        {
            if (_initialized)
                return;
            lock (typeof(TableStorage))
            {
                if (_initialized)
                    return;

                Trace = new TraceEventSource(typeof(TableStorage), SourceLevels.All, 0);

                _configurationSection = StorageSection.Load<TableStorageSection>("system.storageModel/tables");
                _tables = new TableCollection();

                _initialized = true;

                var defaultProvider = string.IsNullOrEmpty(ConfigurationSection.DefaultProvider)
                                          ? null
                                          : Storage.GetProvider<ITableProvider>(ConfigurationSection.DefaultProvider);
                _tables.Provider = defaultProvider;
                foreach (StorageResourceDefinition tableDef in ConfigurationSection.Tables)
                {
                    var provider = string.IsNullOrEmpty(tableDef.Provider)
                                       ? defaultProvider
                                       : Storage.GetProvider<ITableProvider>(tableDef.Provider);
                    if (provider == null)
                        throw new ConfigurationErrorsException("At least a default table provider must be defined");
                    var table = provider.NewTable(tableDef.Name, tableDef);
                    table.CreateIfNotExist();
                    Tables[tableDef.Name] = table;
                    provider.Tables[tableDef.Name] = table;
                }
            }
        }

        public static TableStorageSection ConfigurationSection
        {
            get
            {
                Initialize();
                return _configurationSection;
            }
        }

        public static ITableCollection Tables
        {
            get
            {
                Initialize();
                return _tables;
            }
        }

        public static ITableProvider GetProvider(string nameOrConnectionString)
        {
            return Storage.GetProvider<ITableProvider>(nameOrConnectionString);
        }

        #region Extensions

        public static TableRequestLog LogTableRequests(this ITableProvider provider, Action<TableRequestLog> create)
        {
            return provider.Log(create);
        }

        public static TableRequestLog LogTableRequests(this ITable table)
        {
            return table.Provider.LogTableRequests(log => log.Table = table);
        }

        #endregion
    }
}