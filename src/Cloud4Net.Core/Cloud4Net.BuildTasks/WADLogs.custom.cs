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
using System.Data.Services.Common;
using System.Xml.Linq;

namespace System.StorageModel.Build
{
    public interface IAzureEntity
    {
        string PartitionKey { get; }
        string RowKey { get; }
    }

    public interface ICopyable<T>
    {
        void CopyTo(T other);
    }

    [DataServiceKey("PartitionKey", "RowKey")]
    partial class WADWindowsEventLog : IAzureEntity, ICopyable<WADWindowsEventLog>
    {
        public void CopyTo(WADWindowsEventLog other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
            other.ProviderGuid = this.ProviderGuid;
            other.ProviderName = this.ProviderName;
            other.EventId = this.EventId;
            other.Level = this.Level;
            other.Pid = this.Pid;
            other.Tid = this.Tid;
            other.Channel = this.Channel;
            other.RawXml = XElement.Parse(this.RawXml.ToString());
        }
    }

    [DataServiceKey("PartitionKey", "RowKey")]
    partial class WADLog : IAzureEntity, ICopyable<WADLog>
    {
        public void CopyTo(WADLog other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
            other.Level = this.Level;
            other.EventId = this.EventId;
            other.Pid = this.Pid;
            other.Tid = this.Tid;
            other.Message = this.Message;
        }
    }

    [DataServiceKey("PartitionKey", "RowKey")]
    partial class WADDiagnosticInfrastructureLog : IAzureEntity, ICopyable<WADDiagnosticInfrastructureLog>
    {
        public void CopyTo(WADDiagnosticInfrastructureLog other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
            other.Level = this.Level;
            other.Pid = this.Pid;
            other.Tid = this.Tid;
            other.Function = this.Function;
            other.MDRESULT = this.MDRESULT;
            other.ErrorCode = this.ErrorCode;
            other.ErrorCodeMsg = this.ErrorCodeMsg;
            other.Message = this.Message;
        }
    }

    [DataServiceKey("PartitionKey", "RowKey")]
    partial class WADDirectory : IAzureEntity
        , ICopyable<WADDirectory>
        , ICopyable<WADIISFailedRequestLog>
        , ICopyable<WADIISLog>
    {
        public void CopyTo(WADDirectory other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
            other.AbsolutePath = this.AbsolutePath;
            other.RelativePath = this.RelativePath;
            other.Container = this.Container;
            other.RootDirectory = this.RootDirectory;
        }

        public XElement RawXml;

        public void CopyTo(WADIISFailedRequestLog other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
            other.Url = (string)RawXml.Attribute("url");
            other.AppPoolId = new Guid((string)RawXml.Attribute("appPoolId"));
            other.Pid = int.Parse((string)RawXml.Attribute("processId"));
            other.Verb = (string)RawXml.Attribute("verb");
            other.RemoteUserName = (string)RawXml.Attribute("remoteUserName");
            other.UserName = (string)RawXml.Attribute("userName");
            other.AuthenticationType = (string)RawXml.Attribute("authenticationType");

            var status = (string) RawXml.Attribute("statusCode");
            var p = status.IndexOf('.');
            if (p == -1)
                other.StatusCode = int.Parse(status);
            else
            {
                other.StatusCode = int.Parse(status.Substring(0, p));
                other.SubStatusCode = int.Parse(status.Substring(p + 1));
            }
            other.TimeTaken = int.Parse((string)RawXml.Attribute("timeTaken"));
            other.RawXml = RawXml;
        }

        public void CopyTo(WADIISLog other)
        {
            other.PartitionKey = this.PartitionKey;
            other.RowKey = this.RowKey;
            other.Timestamp = this.Timestamp;
            other.EventTickCount = this.EventTickCount;
            other.DeploymentId = this.DeploymentId;
            other.Role = this.Role;
            other.RoleInstance = this.RoleInstance;
        }
    }

    [DataServiceKey("PartitionKey", "RowKey")]
    partial class WADIISLog : IAzureEntity
    {
    }
}