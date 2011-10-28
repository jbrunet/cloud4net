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
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Net;
using System.StorageModel.Diagnostics;
using System.Text;
using System.Xml.Linq;

namespace System.StorageModel
{
    /// <summary>
    /// Common interface for storage providers (see <see cref="IBlobProvider"/>, <see cref="IQueueProvider"/>, <see cref="ITableProvider"/>)
    /// </summary>
    public interface IStorageService
    {
        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        /// <value>The name of the provider.</value>
        string Name { get; }

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        /// <param name="name">The name of the provider.</param>
        /// <param name="config">The configuration for the provider</param>
        void Initialize(string name, NameValueCollection config);
    }

    /// <summary>
    /// Base class for storage providers
    /// </summary>
    public abstract class StorageProvider : IStorageService
    {
        /// <summary>
        /// Gets the name of the provider.
        /// </summary>
        /// <value>The name of the provider.</value>
        public string Name { get; private set; }

        /// <summary>
        /// Initializes the provider.
        /// </summary>
        /// <param name="name">The name of the provider.</param>
        /// <param name="config">The configuration for the provider</param>
        public virtual void Initialize(string name, NameValueCollection config)
        {
            Name = name;
        }
    }

    public class Enumerator<T1, T2> : IEnumerator<T2>
        where T1 : T2
    {
        private readonly IEnumerator<T1> _inner;

        public Enumerator(IEnumerable<T1> inner)
        {
            _inner = inner.GetEnumerator();
        }

        #region IEnumerator<T> Members

        public T2 Current
        {
            get { return _inner.Current; }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            _inner.Dispose();
        }

        #endregion

        #region IEnumerator Members

        object IEnumerator.Current
        {
            get { return _inner.Current; }
        }

        public bool MoveNext()
        {
            return _inner.MoveNext();
        }

        public void Reset()
        {
            _inner.Reset();
        }

        #endregion
    }

    /// <summary>
    /// Factory class to instanciate <see cref="IStorageService">providers</see>
    /// </summary>
    public static class Storage
    {
        private static readonly Dictionary<string, IStorageService> _providers = new Dictionary<string, IStorageService>();

        #region Connection Strings

        private static Func<string, string> _getConnectionString;

        /// <summary>
        /// Set up an alternate connection string publisher.
        /// </summary>
        /// <param name="publisher">The connection string publisher delegate. Must returns the connectionstring of the specified connection name</param>
        public static void SetConnectionStringPublisher(Func<string, string> publisher)
        {
            _getConnectionString = publisher;
        }

        static Storage()
        {
            _getConnectionString = UsingConnectionStrings;
        }

        private static ConnectionStringsSection _configSection;

        /// <summary>
        /// Default connection string publisher. Uses connectionStrings section of the current app.
        /// </summary>
        /// <param name="storage">The connection name.</param>
        /// <returns>The connectionstring of the given connection name</returns>
        public static string UsingConnectionStrings(string storage)
        {
            var settings = _configSection == null
                               ? ConfigurationManager.ConnectionStrings[storage]
                               : _configSection.ConnectionStrings[storage];
            if (settings == null)
                throw new ConfigurationErrorsException("Undefined connection string " + storage);

            var config = GetConfig(settings.ConnectionString);
            if (!string.IsNullOrEmpty(settings.Name))
                config["Name"] = settings.Name;
            if (!string.IsNullOrEmpty(settings.ProviderName))
                config["Provider"] = settings.ProviderName;
            return config.ToConnectionString();
        }

        /// <summary>
        /// Loads the connection strings from another configuration file.
        /// </summary>
        /// <param name="configFile">The configuration file</param>
        public static void LoadConnectionStrings(string configFile)
        {
            var doc = XDocument.Load(configFile);
            _configSection = new ConnectionStringsSection();
            foreach (var add in doc.Root.Descendants("connectionStrings").Descendants("add"))
                _configSection.ConnectionStrings.Add(
                    new ConnectionStringSettings(
                        (string)add.Attribute("name"),
                        (string)add.Attribute("connectionString"),
                        (string)add.Attribute("providerName")
                        ));
        }

        /// <summary>
        /// Gets provider information for a given connection name.
        /// </summary>
        /// <param name="storage">The connection name</param>
        /// <param name="name">The provider name</param>
        /// <param name="providerType">Type of the provider</param>
        /// <param name="config">The provider configuration</param>
        public static void GetProviderInfo(string storage, out string name, out string providerType, out NameValueCollection config)
        {
            config = GetConfig(_getConnectionString(storage));
            name = config["Name"];
            providerType = config["Provider"];
            config.Remove("Name");
            config.Remove("Provider");
            switch (providerType)
            {
                case "AWS":
                    providerType = "System.StorageModel.AWS.AWSProvider, Cloud4Net.AWS";
                    break;
                case "Azure":
                    providerType = "System.StorageModel.WindowsAzure.AzureProvider, Cloud4Net.Azure";
                    break;
                case "FileSystem":
                    providerType = "System.StorageModel.FileSystem.FileSystemProvider, Cloud4Net.FileSystem";
                    break;
                case "AspNetCache":
                    providerType = "System.StorageModel.Caching.AspNetCacheProvider, Cloud4Net.Caching";
                    break;
            }
        }

        #endregion

        /// <summary>
        /// Gets a <see cref="IDbConnection"/> instance for the given connction name.
        /// </summary>
        /// <param name="storage">The connection name</param>
        /// <returns>A configured <see cref="IDbConnection"/></returns>
        public static DbConnection GetDbConnection(string storage)
        {
            string name, providerTypeName;
            NameValueCollection config;
            GetProviderInfo(storage, out name, out providerTypeName, out config);

            var factory = DbProviderFactories.GetFactory(providerTypeName);
            var conn = factory.CreateConnection();
            conn.ConnectionString = config.ToConnectionString();
            return conn;
        }

        /// <summary>
        /// Gets a provider instance for the given connection name.
        /// </summary>
        /// <remarks>
        /// Instances are instanciated just-in-time and stored in a global dictionary.
        /// </remarks>
        /// <typeparam name="TService">The type of the service to retrieve. May be a generic provider <see cref="IStorageService">service</see> or a concrete provider (AzureProvider,AWSProvider...)</typeparam>
        /// <param name="nameOrConnectionString">The connection name</param>
        /// <returns>The provider instance</returns>
        public static TService GetProvider<TService>(string nameOrConnectionString)
            where TService : IStorageService
        {
            IStorageService provider;
            if (!_providers.TryGetValue(nameOrConnectionString, out provider))
                lock (_providers)
                {
                    if (!_providers.TryGetValue(nameOrConnectionString, out provider))
                    {
                        string name, providerTypeName;
                        NameValueCollection config;
                        GetProviderInfo(nameOrConnectionString, out name, out providerTypeName, out config);
                        var providerType = Type.GetType(providerTypeName, true, true);
                        provider = (StorageProvider)Activator.CreateInstance(providerType);
                        _providers.Add(nameOrConnectionString, provider);
                        try
                        {
                            provider.Initialize(name, config);
                        }
                        catch
                        {
                            _providers.Remove(nameOrConnectionString);
                            throw;
                        }
                    }
                }
            return (TService)provider;
        }

        private static NameValueCollection GetConfig(string connectionString)
        {
            var config = new NameValueCollection();
            foreach (var setting in connectionString.Split(';'))
            {
                var pair = setting.Split(new[] { '=' }, 2);
                config.Add(pair[0], pair.Length < 2 ? string.Empty : pair[1]);
            }
            return config;
        }

        public static string ToConnectionString(this NameValueCollection config)
        {
            var sb = new StringBuilder();
            var next = false;
            foreach (string name in config)
            {
                if (next)
                    sb.Append(';');
                else
                    next = true;
                sb.Append(name);
                sb.Append('=');
                sb.Append(config[name]);
            }
            return sb.ToString();
        }

        public static bool? GetBool(this NameValueCollection config, string name)
        {
            var value = config[name];
            return string.IsNullOrEmpty(value)
                ? (bool?)null
                : bool.Parse(value);
        }

        public static T[] ZeroIfNull<T>(this T[] items)
        {
            return items ?? new T[0];
        }

        public static NameValueCollection Clone(this NameValueCollection metadata)
        {
            return (metadata == null)
                       ? new NameValueCollection()
                       : new NameValueCollection(metadata);
        }

        public static WebHeaderCollection Clone(this WebHeaderCollection headers)
        {
            var value = new WebHeaderCollection();
            if (headers != null)
                foreach (string name in headers)
                    value.Add(name, headers[name]);
            return value;
        }

        public static void ReplaceWith(this NameValueCollection target, NameValueCollection source)
        {
            target.Clear();
            target.Add(source);
        }

        public static void OnKey(this NameValueCollection config, string key, Action<string> ifSet)
        {
            OnKey(config, key, ifSet, null);
        }

        public static void OnKey(this NameValueCollection config, string key, Action<string> ifSet, Action @ifNotSet)
        {
            try
            {
                var value = config[key];
                if (!string.IsNullOrEmpty(value))
                    ifSet(value);
                else if (ifNotSet != null)
                    ifNotSet();
            }
            catch (Exception inner)
            {
                throw new ArgumentException(inner.Message, key, inner);
            }
        }

        [ThreadStatic]
        internal static WebRequestLog LastRequestLog;

        public static TLog Log<TLog, TProvider>(this TProvider provider, Action<TLog> create)
            where TLog : WebRequestLog, new()
            where TProvider : IStorageService
        {
            var log = new TLog { Provider = provider as StorageProvider };
            if (create != null)
                create(log);
            LastRequestLog = log;
            return log;
            //return WebRequestLog.TryCreate(() =>
            //    {
            //        var log = new TLog { Provider = provider as StorageProvider };
            //        if (create != null)
            //            create(log);
            //        LastRequestLog = log;
            //        return log;
            //    });
        }
    }
}