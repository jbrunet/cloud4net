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

using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;

namespace System.StorageModel.Configuration
{
    [DebuggerDisplay("{Name}")]
    public sealed class StorageResourceDefinition : ConfigurationElement
    {
        #region .ctor

        public StorageResourceDefinition()
        {
            _properties = new ConfigurationPropertyCollection
                              {
                                  NameProperty,
                                  ProviderProperty,
                              };
        }

        #endregion

        #region Internals

        private readonly ConfigurationPropertyCollection _properties;

        protected override ConfigurationPropertyCollection Properties
        {
            get { return _properties; }
        }

        protected override bool OnDeserializeUnrecognizedAttribute(string name, string value)
        {
            var property = new ConfigurationProperty(name, typeof(string), value);
            _properties.Add(property);
            this[property] = value;
            Parameters[name] = value;
            return true;
        }

        #endregion

        #region .Name

        private static readonly ConfigurationProperty NameProperty = new ConfigurationProperty("name", typeof(string), null, ConfigurationPropertyOptions.IsKey | ConfigurationPropertyOptions.IsRequired);
        public string Name
        {
            get { return (string)this[NameProperty]; }
            set { this[NameProperty] = value; }
        }

        #endregion

        #region .DefaultProvider

        private static readonly ConfigurationProperty ProviderProperty = new ConfigurationProperty("provider", typeof(string), null);
        public string Provider
        {
            get { return (string)this[ProviderProperty]; }
            set { this[ProviderProperty] = value; }
        }

        #endregion

        public readonly NameValueCollection Parameters = new NameValueCollection();
    }

    public sealed class StorageResourceDefinitionCollection : ConfigurationElementCollection
    {
        #region Internals

        protected override ConfigurationElement CreateNewElement()
        {
            return new StorageResourceDefinition();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((StorageResourceDefinition)element).Name;
        }

        #endregion
    }

    public abstract class StorageSection : ConfigurationSection
    {
        #region .DefaultProvider

        private const string ProviderProperty = "defaultProvider";
        [ConfigurationProperty(ProviderProperty, IsRequired = true)]
        public string DefaultProvider
        {
            get { return (string)this[ProviderProperty]; }
            set { this[ProviderProperty] = value; }
        }

        #endregion

        public static TSection Load<TSection>(string sectionName)
            where TSection : StorageSection, new()
        {

            var section = (TSection)ConfigurationManager.GetSection(sectionName);
            if (section == null)
                section = new TSection(); // throw new ConfigurationErrorsException(sectionName + " section is missing");
            return section;
        }
    }
}