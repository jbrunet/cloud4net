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
using System.Collections.Specialized;
using System.Linq;
using System.StorageModel.Diagnostics;

namespace System.StorageModel.WindowsAzure
{
    using Billing;
    using System.StorageModel.Billing;

    /// <summary>
    /// Updated 12 nov 2009 from http://www.microsoft.com/windowsazure/pricing/#windows
    /// </summary>
    namespace Billing
    {
        public enum AzureRegion
        {
            NorthAmericaAndEurope,
            AsiaPacific,
        }

        public sealed class AzureDataTransferIn : BillingItem, ICloneable<AzureDataTransferIn>
        {
            #region .ctor

            public AzureDataTransferIn(AzureRegion region, DateTime period)
                : base("Data Transfer In (GB/month)", 1.GigaBytes(), PricePerRegion[region])
            {
                if (period < AzurePricingPlans.OffPeak_Until_30_June_2010)
                    UnitPrice = Price.Free;
            }

            #endregion

            public AzureDataTransferIn Clone()
            {
                return (AzureDataTransferIn)this.MemberwiseClone();
            }

            public static readonly Dictionary<AzureRegion, Price> PricePerRegion = new Dictionary<AzureRegion, Price>
                                                                               {
                                                                                   {AzureRegion.NorthAmericaAndEurope,"$0.10"},
                                                                                   {AzureRegion.AsiaPacific, "$0.30"}
                                                                               };
        }

        public sealed class AzureDataTransferOut : BillingItem, ICloneable<AzureDataTransferOut>
        {
            #region .ctor

            public AzureDataTransferOut(AzureRegion region, DateTime period)
                : base("Data Transfer Out (GB/month)", 1.GigaBytes(), PricePerRegion[region])
            {
                if (period < AzurePricingPlans.OffPeak_Until_30_June_2010)
                    UnitPrice = Price.Free;
            }

            #endregion

            public AzureDataTransferOut Clone()
            {
                return (AzureDataTransferOut)this.MemberwiseClone();
            }

            public static readonly Dictionary<AzureRegion, Price> PricePerRegion = new Dictionary<AzureRegion, Price>
                                                                               {
                                                                                   {AzureRegion.NorthAmericaAndEurope,"$0.15"},
                                                                                   {AzureRegion.AsiaPacific, "$0.45"}
                                                                               };
        }

        public sealed class AzureTransactions : BillingItem, ICloneable<AzureTransactions>
        {
            #region .ctor

            public AzureTransactions()
                : base("Transactions (/10K/month)", 10.Kilo(), "$0.01")
            {
            }

            #endregion

            public AzureTransactions Clone()
            {
                return (AzureTransactions)this.MemberwiseClone();
            }
        }

        public sealed class AzureStorage : BillingItem, ICloneable<AzureStorage>
        {
            #region .ctor

            public AzureStorage()
                : base("Storage (GB/month)", 1.GigaBytes(), "$0.15")
            {
            }

            #endregion

            public AzureStorage Clone()
            {
                return (AzureStorage)this.MemberwiseClone();
            }
        }

        public sealed class AzureComputeTime : BillingItem, ICloneable<AzureComputeTime>
        {
            #region .ctor

            public AzureComputeTime()
                : base("Azure Compute Time (Hours/month)", 1, "$0.12")
            {
            }

            #endregion

            public AzureComputeTime Clone()
            {
                return (AzureComputeTime)this.MemberwiseClone();
            }
        }

        public class AzureBill : BillSummary
        {
            public AzureDataTransferIn DataTransferIn;
            public AzureDataTransferOut DataTransferOut;
            public AzureTransactions Transactions;
            public AzureStorage Storage;
            public AzureComputeTime ComputeTime;

            #region .ctor

            internal AzureBill()
            {
                Transactions = new AzureTransactions();
                Storage = new AzureStorage();
            }

            public AzureBill(AzureBill pricingPlan)
            {
                FixedPrice = pricingPlan.FixedPrice;
                ComputeTime = pricingPlan.ComputeTime.Clone();
                Storage = pricingPlan.Storage.Clone();
                Transactions = pricingPlan.Transactions.Clone();
                DataTransferIn = pricingPlan.DataTransferIn.Clone();
                DataTransferOut = pricingPlan.DataTransferOut.Clone();
            }

            #endregion

            public override IEnumerable<BillingItem> Items
            {
                get { return new BillingItem[] { DataTransferIn, DataTransferOut, Transactions, Storage }; }
            }

            public void Bill(AzureProvider provider, IEnumerable<WebRecord> records)
            {
                foreach (var record in records)
                {
                    if (record.Log.Provider != provider)
                        continue;
                    if (IsAzureBlobRequest(provider, record) ||
                        IsAzureQueueRequest(provider, record) ||
                        IsAzureTableRequest(provider, record))
                    {
                        DataTransferIn.Append(record.BytesSent);
                        DataTransferOut.Append(record.BytesReceived);
                        Transactions.Append(1);
                    }
                }
            }

            private static bool IsAzureBlobRequest(AzureProvider provider, WebRecord record)
            {
                return (record.Log is BlobRequestLog || (record.Uri.Host == provider.BlobClient.BaseUri.Host));
            }

            private static bool IsAzureQueueRequest(AzureProvider provider, WebRecord record)
            {
                return (record.Log is QueueRequestLog);// || (record.Uri.Host == provider.QueueClient.BaseUri.Host));
            }

            private static bool IsAzureTableRequest(AzureProvider provider, WebRecord record)
            {
                return (record.Log is TableRequestLog || (record.Uri.Host == provider.TableClient.BaseUri.Host));
            }
        }
    }

    public static class AzurePricingPlans
    {
        public static readonly DateTime OffPeak_Until_30_June_2010 = new DateTime(2010, 06, 30);

        public static AzureBill PayAsYouGo(AzureRegion region, DateTime period)
        {
            return new AzureBill
                       {
                           FixedPrice = Price.Free,
                           ComputeTime = new AzureComputeTime(),
                           Storage = new AzureStorage(),
                           Transactions = new AzureTransactions(),
                           DataTransferIn = new AzureDataTransferIn(region, period),
                           DataTransferOut = new AzureDataTransferOut(region, period),
                       };
        }

        public static AzureBill IntroductorySpecial(AzureRegion region, DateTime period)
        {
            var bill = PayAsYouGo(region, period);
            bill.ComputeTime.Included = 25;
            bill.Storage.Included = .5.MegaBytes();
            bill.Transactions.Included = 10.Kilo();
            bill.DataTransferIn.Included = .5.GigaBytes();
            bill.DataTransferOut.Included = .5.GigaBytes();
            return bill;
        }

        public static AzureBill DevelopmentAcceleratorCore(AzureRegion region, DateTime period)
        {
            var bill = PayAsYouGo(region, period);
            bill.FixedPrice = "$59.95";
            bill.ComputeTime.Included = 750;
            bill.Storage.Included = 10.GigaBytes();
            bill.Transactions.Included = 1.Million();
            switch (region)
            {
                case AzureRegion.NorthAmericaAndEurope:
                    bill.DataTransferIn.Included = 7.GigaBytes();
                    bill.DataTransferOut.Included = 14.GigaBytes();
                    break;
                case AzureRegion.AsiaPacific:
                    bill.DataTransferIn.Included = 2.5.GigaBytes();
                    bill.DataTransferOut.Included = 5.GigaBytes();
                    break;
            }
            return bill;
        }

        public static AzureBill DevelopmentAcceleratorExtended(AzureRegion region, DateTime period)
        {
            var bill = DevelopmentAcceleratorCore(region, period);
            bill.FixedPrice = "$109.95";
            return bill;
        }
    }

    partial class AzureProvider : IStorageBilling
    {
        public AzureBill Pricing;

        partial void InitializeBilling(NameValueCollection config)
        {
            var region = AzureRegion.NorthAmericaAndEurope;
            config.OnKey("Region"
                         , value => { region = (AzureRegion) Enum.Parse(typeof (AzureRegion), value); }
                         , () => { });
            Pricing = AzurePricingPlans.IntroductorySpecial(region, DateTime.Today);
        }

        #region IStorageBilling Members

        public BillSummary ComputeBilling(IEnumerable<WebRecord> records)
        {
            var summary = new AzureBill(Pricing);
            summary.Bill(this, records);
            return summary;
        }

        #endregion
    }
}