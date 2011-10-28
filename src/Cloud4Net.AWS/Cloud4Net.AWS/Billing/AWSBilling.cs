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
using System.StorageModel.Diagnostics;
using Amazon.S3.Model;

namespace System.StorageModel.AWS
{
    using Billing;
    using System.StorageModel.Billing;

    namespace Billing
    {
        #region S3

        /// <summary>
        /// Updated Nov. 12, 2009 from http://aws.amazon.com/s3/#pricing
        /// </summary>
        public sealed class S3Pricing
        {
            public S3StoragePricing Storage;
            public S3DataTransferPricing DataTransferIn, DataTransferOut;
            public S3RequestsPricing OtherRequests, GetRequests;

            public static readonly Dictionary<S3Region, S3Pricing> PricePerRegion = new Dictionary<S3Region, S3Pricing>
            {
                {S3Region.US,new S3Pricing
                {
                    Storage = S3StoragePricing.PricePerRegion[S3Region.US],
                    DataTransferIn = S3DataTransferPricing.In,
                    DataTransferOut = S3DataTransferPricing.Out,
                    OtherRequests = S3RequestsPricing.US_EU_PutCopyPostList,
                    GetRequests = S3RequestsPricing.US_EU_Get,
                }},
                {S3Region.SFO,new S3Pricing
                {
                    Storage = S3StoragePricing.PricePerRegion[S3Region.SFO],
                    DataTransferIn = S3DataTransferPricing.In,
                    DataTransferOut = S3DataTransferPricing.Out,
                    OtherRequests = S3RequestsPricing.SFO_PutCopyPostList,
                    GetRequests = S3RequestsPricing.SFO_Get,
                }},
                {S3Region.EU,new S3Pricing
                {
                    Storage = S3StoragePricing.PricePerRegion[S3Region.EU],
                    DataTransferIn = S3DataTransferPricing.In,
                    DataTransferOut = S3DataTransferPricing.Out,
                    OtherRequests = S3RequestsPricing.US_EU_PutCopyPostList,
                    GetRequests = S3RequestsPricing.US_EU_Get,
                }}};
        }

        public sealed class S3StoragePricing : BillingSlice, ICloneable<S3StoragePricing>
        {
            #region .ctor

            public S3StoragePricing(string name, Price unitPrice, long sliceLimit, S3StoragePricing nextSlice)
                : base(name, 1.GigaBytes(), unitPrice, sliceLimit, nextSlice)
            {
            }

            #endregion

            public S3StoragePricing Clone()
            {
                return this.Clone(this.MemberwiseClone());
            }

            public static readonly Dictionary<S3Region, S3StoragePricing> PricePerRegion = new Dictionary<S3Region, S3StoragePricing>
            {
                {S3Region.US,new S3StoragePricing("S3 Storage Used (First 50 TB)", "$0.15", 50.TeraBytes(),
                    new S3StoragePricing("S3 Storage Used (Next 50 TB)", "$0.14", 50.TeraBytes(),
                        new S3StoragePricing("S3 Storage Used (Next 400 TB)", "$0.130", 400.TeraBytes(),
                            new S3StoragePricing("S3 Storage Used (Next 500 TB)", "$0.105", 500.TeraBytes(),
                                new S3StoragePricing("S3 Storage Used (Next 4000 TB)", "$0.80", 4000.TeraBytes(),
                                    new S3StoragePricing("S3 Storage Used (Over 5000 TB)", "$0.55", 0, null))))))},

                {S3Region.EU,new S3StoragePricing("S3 Storage Used (First 50 TB)", "$0.15", 50.TeraBytes(),
                    new S3StoragePricing("S3 Storage Used (Next 50 TB)", "$0.14", 50.TeraBytes(),
                        new S3StoragePricing("S3 Storage Used (Next 400 TB)", "$0.130", 400.TeraBytes(),
                            new S3StoragePricing("S3 Storage Used (Next 500 TB)", "$0.105", 500.TeraBytes(),
                                new S3StoragePricing("S3 Storage Used (Next 4000 TB)", "$0.80", 4000.TeraBytes(),
                                    new S3StoragePricing("S3 Storage Used (Over 5000 TB)", "$0.55", 0, null))))))},

                {S3Region.SFO,new S3StoragePricing("S3 Storage Used (First 50 TB)", "$0.165", 50.TeraBytes(),
                    new S3StoragePricing("S3 Storage Used (Next 50 TB)", "$0.155", 50.TeraBytes(),
                        new S3StoragePricing("S3 Storage Used (Next 400 TB)", "$0.145", 400.TeraBytes(),
                            new S3StoragePricing("S3 Storage Used (Next 500 TB)", "$0.120", 500.TeraBytes(),
                                new S3StoragePricing("S3 Storage Used (Next 4000 TB)", "$0.095", 4000.TeraBytes(),
                                    new S3StoragePricing("S3 Storage Used (Over 5000 TB)", "$0.070", 0, null))))))}
            };
        }

        public sealed class S3DataTransferPricing : BillingSlice, ICloneable<S3DataTransferPricing>
        {
            #region .ctor

            public S3DataTransferPricing(string name, Price unitPrice, long sliceLimit, S3DataTransferPricing nextSlice)
                : base(name, 1.GigaBytes(), unitPrice, sliceLimit, nextSlice)
            {
            }

            #endregion

            public S3DataTransferPricing Clone()
            {
                return this.Clone(this.MemberwiseClone());
            }

            // US - Standard
            // US - N. California
            // EU - Ireland
            public static readonly S3DataTransferPricing In = new S3DataTransferPricing("S3 Data Transfer In", "$0.100", 0, null);

            public static readonly S3DataTransferPricing Out =
                new S3DataTransferPricing("S3 Data Transfer Out (First 10 TB)", "$0.170", 10.TeraBytes(),
                    new S3DataTransferPricing("S3 Data Transfer Out (Next 40 TB)", "$0.130", 40.TeraBytes(),
                        new S3DataTransferPricing("S3 Data Transfer Out (Next 100 TB)", "$0.110", 100.TeraBytes(),
                            new S3DataTransferPricing("S3 Data Transfer Out (Over 150 TB)", "$0.100", 0, null))));
        }

        public sealed class S3RequestsPricing : BillingSlice, ICloneable<S3RequestsPricing>
        {
            #region .ctor

            public S3RequestsPricing(string name, Price unitPrice, long bytesPerUnit)
                : base(name, bytesPerUnit, unitPrice)
            {
            }

            #endregion

            public S3RequestsPricing Clone()
            {
                return this.Clone(this.MemberwiseClone());
            }

            // US - Standard
            // EU - Ireland
            public static readonly S3RequestsPricing US_EU_PutCopyPostList = new S3RequestsPricing("S3 Requests (PUT, COPY, POST, or LIST)", "$0.01", 1.Kilo());
            public static readonly S3RequestsPricing US_EU_Get = new S3RequestsPricing("S3 Requests (GET)", "$0.01", 10.Kilo());
            // US - N. California
            public static readonly S3RequestsPricing SFO_PutCopyPostList = new S3RequestsPricing("Requests (PUT, COPY, POST, or LIST)", "$0.011", 1.Kilo());
            public static readonly S3RequestsPricing SFO_Get = new S3RequestsPricing("S3 Requests (GET)", "$0.011", 10.Kilo());
        }

        #endregion

        #region SQS

        /// <summary>
        /// Updated Nov. 12, 2009 from http://aws.amazon.com/sqs/#pricing
        /// </summary>
        public class SQSPricing
        {
            public SQSDataTransferPricing DataTransferIn, DataTransferOut;
            public SQSRequestsPricing Requests;

            public SQSPricing Clone()
            {
                return new SQSPricing
                {
                    DataTransferIn = this.DataTransferIn.Clone(),
                    DataTransferOut = this.DataTransferOut.Clone(),
                    Requests = this.Requests.Clone(),
                };
            }

            public static readonly SQSPricing US = new SQSPricing
            {
                DataTransferIn = SQSDataTransferPricing.In,
                DataTransferOut = SQSDataTransferPricing.Out,
                Requests = SQSRequestsPricing.Default,
            };
        }

        public sealed class SQSDataTransferPricing : BillingSlice, ICloneable<SQSDataTransferPricing>
        {
            #region .ctor

            public SQSDataTransferPricing(string name, Price unitPrice, long sliceLimit, SQSDataTransferPricing nextSlice)
                : base(name, 1.GigaBytes(), unitPrice, sliceLimit, nextSlice)
            {
            }

            #endregion

            public SQSDataTransferPricing Clone()
            {
                return this.Clone(this.MemberwiseClone());
            }

            public static readonly SQSDataTransferPricing In =
                new SQSDataTransferPricing("SQS Data Transfer In", "$0.100", 0, null);

            public static readonly SQSDataTransferPricing Out =
                new SQSDataTransferPricing("SQS Data Transfer Out (First 10 TB)", "$0.17", 10.TeraBytes(),
                    new SQSDataTransferPricing("SQS Data Transfer Out (Next 40 TB)", "$0.13", 40.TeraBytes(),
                        new SQSDataTransferPricing("SQS Data Transfer Out (Next 100 TB)", "$0.11", 100.TeraBytes(),
                            new SQSDataTransferPricing("SQS Data Transfer Out (Over 150 TB)", "$0.10", 0, null))));
        }

        public sealed class SQSRequestsPricing : BillingSlice, ICloneable<SQSRequestsPricing>
        {
            #region .ctor

            public SQSRequestsPricing(string name, Price unitPrice)
                : base(name, 10.Kilo(), unitPrice)
            {
            }

            #endregion

            public SQSRequestsPricing Clone()
            {
                return this.Clone(this.MemberwiseClone());
            }

            public static readonly SQSRequestsPricing Default = new SQSRequestsPricing("SQS Requests", "$0.01");
        }

        #endregion

        public class AWSBill : BillSummary
        {
            public S3Pricing S3;
            public SQSPricing SQS;

            #region .ctor

            internal AWSBill()
            {
            }

            public AWSBill(AWSBill template)
            {
                S3 = template.S3;
                SQS = template.SQS;
            }

            #endregion

            public override IEnumerable<BillingItem> Items
            {
                get
                {
                    if (_items == null)
                        Reset();
                    return _items;
                }
            }

            private S3DataTransferPricing _s3dataIn, _s3dataOut;
            private S3RequestsPricing _s3GetRequests, _s3OtherRequests;
            private S3StoragePricing _s3storage;
            private SQSDataTransferPricing _sqsDataIn, _sqsDataOut;
            private SQSRequestsPricing _sqsRequests;

            public void Reset()
            {
                _s3dataIn = S3.DataTransferIn.Clone();
                _s3dataOut = S3.DataTransferOut.Clone();
                _s3GetRequests = S3.GetRequests.Clone();
                _s3OtherRequests = S3.OtherRequests.Clone();
                _s3storage = S3.Storage.Clone();
                _sqsDataIn = SQS.DataTransferIn.Clone();
                _sqsDataOut = SQS.DataTransferOut.Clone();
                _sqsRequests = SQS.Requests.Clone();
                _items = new List<BillingItem>
                {
                    _s3dataIn,
                    _s3dataOut,
                    _s3GetRequests,
                    _s3OtherRequests,
                    _s3storage,
                    _sqsDataIn,
                    _sqsDataOut,
                    _sqsRequests,
                };
            }

            private List<BillingItem> _items;

            public void Bill(AWSProvider provider, IEnumerable<WebRecord> records)
            {
                if (_items == null)
                    Reset();

                foreach (var record in records)
                {
                    if (record.Log.Provider != provider)
                        continue;
                    if (IsS3Request(provider, record))
                    {
                        _items.Bill(ref _s3dataIn, record.BytesSent);
                        _items.Bill(ref _s3dataOut, record.BytesReceived);
                        if (record.HttpMethod == "GET")
                            _items.Bill(ref _s3GetRequests, 1);
                        else
                            _items.Bill(ref _s3OtherRequests, 1);
                    }
                    else if (IsSQSRequest(provider, record))
                    {
                        _items.Bill(ref _sqsDataIn, record.BytesSent);
                        _items.Bill(ref _sqsDataOut, record.BytesReceived);
                        _items.Bill(ref _sqsRequests, 1);
                    }
                    else if (IsSimpleDBRequest(provider, record))
                    {
                    }
                }
            }

            private static bool IsS3Request(AWSProvider provider, WebRecord record)
            {
                return (record.Log is BlobRequestLog || (record.Uri.Host == provider.S3Config.ServiceURL));
            }

            private static bool IsSQSRequest(AWSProvider provider, WebRecord record)
            {
                return (record.Log is QueueRequestLog || (record.Uri.Host == provider.SQSConfig.ServiceURL));
            }

            private static bool IsSimpleDBRequest(AWSProvider provider, WebRecord record)
            {
                return (record.Log is TableRequestLog);// || (record.Uri.Host == provider.TableClient.BaseUri.Host));
            }
        }

        public static class AWSPricingPlans
        {
            public static AWSBill Standard(S3Region region)
            {
                return new AWSBill
                           {
                               S3 = S3Pricing.PricePerRegion[region],
                               SQS = SQSPricing.US,
                           };
            }
        }
    }

    partial class AWSProvider : IStorageBilling
    {
        public AWSBill Pricing;

        partial void InitializeBilling(NameValueCollection config)
        {
            Pricing = AWSPricingPlans.Standard(this.S3Region);
        }

        #region IStorageBilling Members

        public BillSummary ComputeBilling(IEnumerable<WebRecord> records)
        {
            var summary = new AWSBill(Pricing);
            summary.Bill(this, records);
            return summary;
        }

        #endregion
    }
}