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
using System.IO;
using System.Linq;
using System.StorageModel.Diagnostics;
using System.StorageModel.Local;
using System.StorageModel.WindowsAzure;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using System.Text;

namespace System.StorageModel.Build
{
    public class DumpAzureLogs : CloudTask
    {
        #region Parameters

        [Required]
        public string Storage { get; set; }

        private AzureProvider _provider;
        private AzureProvider Provider
        {
            get { return _provider ?? (_provider = System.StorageModel.Storage.GetProvider<AzureProvider>(Storage)); }
        }

        [Required]
        public string SqlConnectionString { get; set; }

        public string LocalDirectory { get; set; }

        #endregion

        private FileSystemProvider _fsProvider;
        private System.Data.Linq.Table<WADIISFailedRequestLog> _frl;
        private System.Data.Linq.Table<WADIISLog> _iis;

        public override bool Execute()
        {
            _fsProvider = new FileSystemProvider();
            _fsProvider.Initialize("FileSystem", LocalDirectory);

            try
            {
                using (var tc = Provider.GetTrackingContext())
                {
                    var conn = System.StorageModel.Storage.GetDbConnection(SqlConnectionString);
                    using (var dc = new WADLogsDataContext(conn))
                    {
                        if (!dc.DatabaseExists())
                            dc.CreateDatabase();

                        using (var log = new WebRequestLog())
                        {
                            #region Control Container

                            Log.LogMessage("Dump: ControlContainers...");
                            var container = Provider.Containers["wad-control-container"];
                            if (container.Exists)
                                foreach (var blob in container.Blobs.FindAll(BlobSelect.All))
                                {
                                    Log.LogMessage("  {0}", blob.Path);
                                    blob.CopyTo(_fsProvider, BlobOptions.Default, BlobCopyCondition.IfNewer);
                                }

                            #endregion

                            #region Windows Event Logs

                            Log.LogMessage("Dump: WindowsEventLogs...");
                            Pump(
                                Provider.Tables["WADWindowsEventLogsTable"].Map<WADWindowsEventLog>(),
                                dc.WADWindowsEventLogs,
                                PumpAction.Move);

                            #endregion

                            #region Logs

                            Log.LogMessage("Dump: Logs...");
                            Pump(
                                Provider.Tables["WADLogsTable"].Map<WADLog>(),
                                dc.WADLogs,
                                PumpAction.Move);

                            #endregion

                            #region Diagnostic Infrastructure Logs

                            Log.LogMessage("Dump: Diagnostic Infrastructure Logs...");
                            Pump(
                                Provider.Tables["WADDiagnosticInfrastructureLogsTable"].Map
                                    <WADDiagnosticInfrastructureLog>(),
                                dc.WADDiagnosticInfrastructureLogs,
                                PumpAction.Move);

                            #endregion

                            #region Directories

                            _frl = dc.WADIISFailedRequestLogs;
                            _iis = dc.WADIISLogs;

                            Log.LogMessage("Dump: Directories...");
                            Pump(
                                Provider.Tables["WADDirectoriesTable"].Map<WADDirectory>(),
                                dc.WADDirectories,
                                PumpAction.Move, PumpDirectory);

                            #endregion
                        }
                    }
                }

                return true;
            }
            catch (Exception error)
            {
                Log.LogErrorFromException(error, true);
                return false;
            }
        }

        private readonly List<string> _directoriesProcessed = new List<string>();

        private void PumpDirectory(ITable<WADDirectory> table, WADDirectory storageRow, WADDirectory sqlRow)
        {
            var key = sqlRow.Container + ':' + sqlRow.RelativePath;
            if (_directoriesProcessed.Contains(key))
                return;
            _directoriesProcessed.Add(key);

            Log.LogMessage("  {0}", storageRow.RelativePath);
            var blob = Provider.Containers[sqlRow.Container].Blobs[sqlRow.RelativePath];
            FileSystemBlobSpec file;
            try
            {
                file = (FileSystemBlobSpec)blob.CopyTo(_fsProvider, BlobOptions.Default, BlobCopyCondition.IfNewer);
            }
            catch (BlobDoesNotExistsException)
            {
                Log.LogWarning("Missing blob " + sqlRow.RelativePath);
                return;
            }
            switch (sqlRow.Container)
            {
                case "wad-iis-failedreqlogfiles":

                    #region IIS Failed Request Logs

                    if (blob.Exists)
                    {
                        if (Path.GetExtension(file.File.Name) == ".xml")
                        {
                            string text;
                            using (var stream = file.Read())
                            {
                                using (var tr = new StreamReader(stream, Encoding.UTF8))
                                {
                                    text = tr.ReadToEnd();
                                    foreach (var c in new[] { '\u0010', '\u001a' })
                                        text = text.Replace(c, '?');
                                }
                            }
                            using (var sr = new StringReader(text))
                            {
                                try
                                {
                                    var doc = XDocument.Load(sr);
                                    sqlRow.RawXml = doc.Root;
                                }
                                catch (XmlException ex)
                                {
                                    Log.LogErrorFromException(ex);
                                    return;
                                }
                            }

                            var sqlFailedRequestLogRow =
                                _frl.Where(
                                    frl =>
                                    frl.PartitionKey == sqlRow.PartitionKey && frl.RowKey == sqlRow.RowKey)
                                    .FirstOrDefault();
                            if (sqlFailedRequestLogRow == null)
                            {
                                sqlFailedRequestLogRow = new WADIISFailedRequestLog();
                                sqlRow.CopyTo(sqlFailedRequestLogRow);
                                _frl.InsertOnSubmit(sqlFailedRequestLogRow);
                            }
                            else
                                sqlRow.CopyTo(sqlFailedRequestLogRow);
                            _frl.Context.SubmitChanges();
                        }
                        blob.Delete();
                    }
                    //table.Delete(storageRow);

                    #endregion

                    break;

                case "wad-iis-logfiles":

                    #region IIS W3C Logs

                    if (blob.Exists)
                    {
                        var log = new W3CLogFile { File = file.File };
                        var ticks = 0;
                        int ticks2;
                        foreach (var entry in log.Parse(null, null))
                        {
                            do
                            {
                                ticks2 = Environment.TickCount;
                                if (ticks2 != ticks)
                                    break;
                                Threading.Thread.Sleep(1);
                            } while (true);
                            ticks = ticks2;
                            var row = new WADIISLog();
                            sqlRow.CopyTo(row);
                            row.EventTickCount = entry.Date.Ticks + entry.Time.Ticks;
                            row.Timestamp = new DateTime(row.EventTickCount, DateTimeKind.Utc);
                            row.RowKey += "_" + ticks;
                            row.IPAddress = entry.ClientIPAddress.ToString();
                            row.UserName = entry.UserName;
                            row.ServiceName = entry.ServiceName;
                            row.ServerName = entry.ServerName;
                            row.ServerIPAddress = entry.ServerIPAddress.ToString();
                            row.ServerPort = entry.ServerPort;
                            row.Method = entry.HttpMethod;
                            row.URIStem = entry.URIStem;
                            row.URIQuery = entry.URIQuery;
                            row.ProtocolStatus = (int)entry.ProtocolStatus;
                            row.ProtocolSubStatus = entry.ProtocolSubstatus;
                            row.Win32Status = entry.Win32Status;
                            row.BytesSent = entry.BytesSent;
                            row.BytesReceived = entry.BytesReceived;
                            row.TimeTaken = (int)entry.TimeTaken.TotalMilliseconds;
                            _iis.InsertOnSubmit(row);
                            _iis.Context.SubmitChanges();
                        }
                        blob.Delete();
                    }
                    //table.Delete(storageRow);

                    #endregion

                    break;
            }
        }

        enum PumpAction
        {
            Move,
            Echo,
        }

        private void Pump<T>(ITable<T> source, System.Data.Linq.Table<T> target, PumpAction action)
            where T : class, IAzureEntity, ICopyable<T>, new()
        {
            Pump(source, target, action, null);
        }

        private void Pump<T>(ITable<T> source, System.Data.Linq.Table<T> target, PumpAction action, Action<ITable<T>, T, T> editRow)
            where T : class, IAzureEntity, ICopyable<T>, new()
        {
            if (!source.Exists)
            {
                Log.LogWarning("Azure table " + source.Name + " does not exist, skipping.");
                return;
            }
            using (source.Provider.GetTrackingContext())
            {
                foreach (var storageRow in source.FindAll(null))
                {
                    Log.LogMessage("  {0} {1}", storageRow.PartitionKey, storageRow.RowKey);
                    T sqlRow;

                    bool insert;
                    if (action == PumpAction.Echo)
                    {
                        sqlRow =
                            target.Where(r => r.PartitionKey == storageRow.PartitionKey && r.RowKey == storageRow.RowKey)
                                .FirstOrDefault();
                        if (sqlRow == null)
                            insert = true;
                        else
                        {
                            storageRow.CopyTo(sqlRow);
                            if (editRow != null)
                                editRow(source, storageRow, sqlRow);
                            insert = false;
                        }
                    }
                    else
                        insert = true;

                    if (insert)
                    {
                        sqlRow = new T();
                        storageRow.CopyTo(sqlRow);
                        if (editRow != null)
                            editRow(source, storageRow, sqlRow);
                        target.InsertOnSubmit(sqlRow);
                    }

                    try
                    {
                        target.Context.SubmitChanges();
                    }
                    catch (Exception sqlError)
                    {
                        Log.LogErrorFromException(sqlError);
                    }
                    if (action == PumpAction.Move)
                        source.Delete(storageRow);
                }
            }
        }
    }
}