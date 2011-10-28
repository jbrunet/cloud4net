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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.StorageModel.Diagnostics;

namespace System.StorageModel.Billing
{
    #region Interfaces

    public interface IStorageBilling
    {
        BillSummary ComputeBilling(IEnumerable<WebRecord> records);
    }

    public interface ICloneable<T>
    {
        T Clone();
    }

    #endregion

    #region Base classes

    public static class Currency
    {
        public static readonly NumberFormatInfo USD = CultureInfo.GetCultureInfo("en-US").NumberFormat;
        public static readonly NumberFormatInfo Euro = CultureInfo.GetCultureInfo("fr-FR").NumberFormat;

        public static readonly Dictionary<string, NumberFormatInfo> PerSymbol;

        static Currency()
        {
            PerSymbol = new Dictionary<string, NumberFormatInfo>();
            foreach (var currency in new[] { USD, Euro })
                PerSymbol.Add(currency.CurrencySymbol, currency);
        }
    }

    public struct Price
    {
        /// <summary>
        /// Amount
        /// </summary>
        public decimal Amount;

        /// <summary>
        /// Currency
        /// </summary>
        public NumberFormatInfo Currency;

        public static implicit operator Price(string text)
        {
            var p = text.IndexOfAny(".0123456789".ToCharArray());
            return new Price
                       {
                           Currency = Billing.Currency.PerSymbol[text.Substring(0, p)],
                           Amount = decimal.Parse(text.Substring(p), Billing.Currency.USD)
                       };
        }

        public static Price operator *(Price price, decimal factor)
        {
            return new Price
                       {
                           Amount = price.Amount * factor,
                           Currency = price.Currency
                       };
        }

        public static Price operator +(Price price, decimal operand)
        {
            return new Price
                       {
                           Amount = price.Amount + operand,
                           Currency = price.Currency
                       };
        }

        public static readonly Price Free;

        public override string ToString()
        {
            return Amount.ToString("C", Currency);
        }
    }

    public class BillingItem
    {
        #region .ctor

        protected BillingItem(string name, long bytesPerUnit, params Price[] unitPrices)
        {
            this.Name = name;
            this.BytesPerUnit = bytesPerUnit;
            this.UnitPrices = unitPrices;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Description
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unit price
        /// </summary>
        public Price[] UnitPrices { get; set; }

        /// <summary>
        /// Unit price
        /// </summary>
        public Price UnitPrice
        {
            get { return UnitPrices[0]; }
            set { UnitPrices = new[] { value }; }
        }

        /// <summary>
        /// Bytes per unit
        /// </summary>
        public long BytesPerUnit { get; protected set; }

        /// <summary>
        /// Included Bytes
        /// </summary>
        public long Included { get; set; }

        /// <summary>
        /// Total Bytes
        /// </summary>
        public long Bytes { get; protected set; }

        /// <summary>
        /// Volume (in Units)
        /// </summary>
        public decimal Volume
        {
            get { return (decimal)Bytes / BytesPerUnit; }
        }

        /// <summary>
        /// Non-Included Volume (in Units)
        /// </summary>
        public decimal NonIncludedVolume
        {
            get { return (decimal)Math.Max(0, Bytes - Included) / BytesPerUnit; }
        }

        /// <summary>
        /// Price
        /// </summary>
        public Price Price
        {
            get { return UnitPrice * NonIncludedVolume; }
        }

        #endregion

        public void Append(long bytes)
        {
            Bytes += bytes;
        }

        public override string ToString()
        {
            return string.Format(Price.Currency, "{0} Volume:{1:F5} Amount:{2:C3}", Name, Volume, Price.Amount);
        }
    }

    /// <summary>
    /// Base class for Billing slices
    /// </summary>
    public class BillingSlice : BillingItem
    {
        #region .ctor

        protected BillingSlice(string description, long bytesPerUnit, params Price[] unitPrices)
            : base(description, bytesPerUnit, unitPrices)
        {
        }

        protected BillingSlice(string description, long bytesPerUnit, Price[] unitPrices, long sliceLimit, BillingSlice nextSlice)
            : this(description, bytesPerUnit, unitPrices)
        {
            this.SliceLimit = sliceLimit;
            this.NextSlice = nextSlice;
        }

        protected BillingSlice(string description, long bytesPerUnit, Price unitPrice, long sliceLimit, BillingSlice nextSlice)
            : this(description, bytesPerUnit, new[] { unitPrice }, sliceLimit, nextSlice)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Slice limit in bytes
        /// </summary>
        public long SliceLimit { get; protected set; }

        /// <summary>
        /// Next billing slice to follow when this slice limit is attained
        /// </summary>
        public BillingSlice NextSlice { get; protected internal set; }

        #endregion

        public BillingSlice Append(long bytes, List<BillingItem> items)
        {
            base.Append(bytes);
            if (SliceLimit == 0 || bytes < SliceLimit)
                return this;
            var next = NextSlice;
            next.Bytes = Bytes - SliceLimit;
            this.Bytes = SliceLimit;
            items.Add(next);
            return next;
        }
    }

    public abstract class BillSummary
    {
        #region Properties

        public Price FixedPrice { get; set; }

        public abstract IEnumerable<BillingItem> Items { get; }

        public Price Price
        {
            get { return FixedPrice + Items.Sum(item => item.Price.Amount); }
        }

        #endregion

        public override string ToString()
        {
            return string.Format(Price.Currency, "Amount:{0:C3}", Price.Amount);
        }
    }

    #endregion

    public static class StorageBilling
    {
        #region Extensions

        //public static Dictionary<IBlobContainer, BillingItem> ComputeBillingPerContainer(this IStorageBilling provider, IEnumerable<WebRecord> records)
        //{
        //    return (from cRec in records
        //            let blobContainer = cRec.Log.BlobContainer
        //            where blobContainer != null
        //            group cRec by blobContainer
        //                into containerRecords
        //                select new { Container = containerRecords.Key, Item = provider.ComputeBilling(containerRecords) }
        //           ).ToDictionary(a => a.Container, a => a.Item);
        //}

        public const long KB = 1024;
        public const long MB = KB*1024;
        public const long GB = MB*1024;
        public const long TB = GB*1024;

        public static long Kilo(this int value) { return value*1000; }
        public static long Million(this int value) { return value.Kilo() * 1000; }

        public static long KiloBytes(this int value) { return value * KB; }
        public static long KiloBytes(this double value) { return (long)value * KB; }

        public static long MegaBytes(this int value) { return value * MB; }
        public static long MegaBytes(this double value) { return (long)value * MB; }

        public static long GigaBytes(this int value) { return value * GB; }
        public static long GigaBytes(this double value) { return (long)value * GB; }

        public static long TeraBytes(this int value) { return value * TB; }
        public static long TeraBytes(this double value) { return (long)value * TB; }

        public static void Bill<T>(this List<BillingItem> items, ref T slice, long bytes)
            where T : BillingSlice
        {
            slice = (T)slice.Append(bytes, items);
        }

        public static T Clone<T>(this T thiz, object clone)
            where T : BillingSlice, ICloneable<T>
        {
            var obj = (T) clone;
            if (obj.NextSlice != null)
                obj.NextSlice = ((ICloneable<T>) thiz.NextSlice).Clone();
            return obj;
        }

        public static IEnumerable<T> Repeat<T>(this IEnumerable<T> source, long occurences, bool collated)
        {
            var array = source.ToArray();
            if (collated)
            {
                while (occurences-- != 0)
                    foreach (var item in array)
                        yield return item;
            }
            else
            {
                foreach (var item in array)
                    for (var i = 0; i < occurences; i++)
                        yield return item;
            }
        }

        #endregion
    }
}