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
using System.IO;
using System.Net;
using System.Threading;

namespace System.StorageModel.Diagnostics
{
    #region Base Classes

    public abstract class LogFile<TRecord>
    {
        public FileInfo File;
        public abstract Dictionary<string, Action<TRecord, string>> Mappings { get; }
        public abstract void Flush(TRecord record);
        public string[] Fields;
        public long Position;
    }

    public static class LogParser
    {
        public static IEnumerable<TRecord> Parse<TRecord>(this LogFile<TRecord> file, DateTime? from, DateTime? to)
            where TRecord : WebRecord, new()
        {
            var mappings = file.Mappings;
            using (var stream = file.File.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                stream.Position = file.Position;

                var headers = new Dictionary<string, string>();
                string line;
                var parse = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("#"))
                    {
                        var p = line.IndexOf(":");
                        var name = line.Substring(1, p - 1);
                        var value = line.Substring(p + 1).Trim();
                        headers[name] = value;
                        switch (name)
                        {
                            case "Date":
                                var date = DateTime.Parse(value);
                                parse = (!from.HasValue || date >= from.Value)
                                        && (!to.HasValue || date <= to.Value);
                                continue;
                            case "Fields":
                                file.Fields = value.Split(' ');
                                continue;
                        }
                        continue;
                    }
                    if (!parse)
                        continue;
                    var record = new TRecord();
                    var values = line.Split(' ');
                    for (var i = 0; i < values.Length; i++)
                    {
                        var mapping = mappings[file.Fields[i]];
                        mapping(record, values[i]);
                    }
                    file.Flush(record);
                    yield return record;
                }
                reader.DiscardBufferedData();
                file.Position = stream.Position;
            }
        }

        public static void Append<TRecord>(this LogFile<TRecord> logFile, WebRequestLog log, DateTime? from,
                                           DateTime? to)
            where TRecord : WebRecord, new()
        {
            foreach (var record in Parse(logFile, from, to))
                log.Records.Enqueue(record);
        }
    }

    public class LogMonitor<TLogFile, TRecord>
        where TLogFile : LogFile<TRecord>, new()
        where TRecord : WebRecord, new()
    {
        private readonly WebRequestLog _log;
        private readonly DirectoryInfo _dir;
        private readonly Dictionary<string, TLogFile> _files;
        private readonly Timer _timer;

        #region .ctor

        public LogMonitor(WebRequestLog log, string path)
        {
            _log = log;
            _dir = new DirectoryInfo(Environment.ExpandEnvironmentVariables(path));
            _files = new Dictionary<string, TLogFile>();
            _timer = new Timer(ScanLogFiles, this, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            _timer.Dispose();
        }

        #endregion

        #region Properties

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        #endregion

        void ScanLogFiles(object sender)
        {
            foreach (var file in _dir.GetFiles("*.log"))
                ReadLog(file);
        }

        void ReadLog(FileInfo info)
        {
            lock (this)
            {
                var path = info.FullName;
                TLogFile logFile;
                if (!_files.TryGetValue(path, out logFile))
                {
                    logFile = new TLogFile { File = info };
                    _files.Add(path, logFile);
                }
                info.Refresh();
                if (info.Length == logFile.Position)
                    return;
                logFile.Append(_log, From, To);
            }
        }
    }

    #endregion

    #region W3C Logs

    public class W3CLogFile : LogFile<W3CRecord>
    {
        #region .ctor

        private static readonly Dictionary<string, Action<W3CRecord, string>> _mappings;
        private static readonly DateTimeFormatInfo W3CDateTimeFormatInfo;

        static W3CLogFile()
        {
            W3CDateTimeFormatInfo = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;
            _mappings = new Dictionary<string, Action<W3CRecord, string>>
                            {
                                {"date", (log, value) =>
                                             {
                                                 log.Date = DateTime.Parse(value, W3CDateTimeFormatInfo,
                                                                           DateTimeStyles.AssumeUniversal|
                                                                           DateTimeStyles.AdjustToUniversal);
                                             }}, // 2009-11-17
                                {"time", (log, value) =>
                                             {
                                                 log.Time = DateTime.Parse(value, W3CDateTimeFormatInfo,
                                                                           DateTimeStyles.AssumeUniversal |
                                                                           DateTimeStyles.AdjustToUniversal|
                                                                           DateTimeStyles.NoCurrentDateDefault);
                                             }}, // 11:25:59
                                {"s-sitename", (log, value) => log.ServiceName = value}, // W3SVC12
                                {"s-computername", (log,value) => log.ServerName = value}, // JULIENB-PC
                                {"s-ip", (log,value) => log.ServerIPAddress = IPAddress.Parse(value)}, // ::1
                                {"cs-method", (log,value) => log.HttpMethod = value}, // DEBUG
                                {"cs-uri-stem", (log,value) => log.URIStem = value}, // /debugattach.aspx
                                {"cs-uri-query", (log,value) => log.URIQuery = value}, // -
                                {"s-port", (log,value) => log.ServerPort = int.Parse(value)}, // 8081
                                {"cs-username", (log,value) => log.UserName = value }, // -
                                {"c-ip", (log,value) => log.ClientIPAddress = IPAddress.Parse(value)}, // ::1
                                {"cs-version", (log,value) => log.ProtocolVersion = value}, // HTTP/1.1
                                {"cs(User-Agent)", (log,value) => log.UserAgent = value}, // -
                                {"cs(Cookie)", (log,value) => log.Cookie= value}, // -
                                {"cs(Referer)", (log,value) => log.Referrer= value}, // -
                                {"cs-host", (log,value) =>
                                                {
                                                    var p = value.IndexOf(":");
                                                    if (p!=-1)
                                                        log.Host = value.Substring(0, p);
                                                    else
                                                        log.Host = value;
                                                }}, // localhost:8081
                                {"sc-status", (log,value) => log.ProtocolStatus = (HttpStatusCode)int.Parse(value)}, // 401
                                {"sc-substatus", (log,value) => log.ProtocolSubstatus = int.Parse(value)}, // 0
                                {"sc-win32-status", (log,value) => log.Win32Status = int.Parse(value)}, // 0
                                {"sc-bytes", (log,value) => log.BytesSent = long.Parse(value)}, // 219
                                {"cs-bytes", (log,value) => log.BytesReceived = long.Parse(value)}, // 400
                                {"time-taken", (log,value) => log.TimeTaken = TimeSpan.FromMilliseconds(long.Parse(value))}, // 1597
                            };
        }

        #endregion

        public override Dictionary<string, Action<W3CRecord, string>> Mappings { get { return _mappings; } }

        public override void Flush(W3CRecord record)
        {
            var ub = new UriBuilder(record.ServerPort == 443 ? Uri.UriSchemeHttps : Uri.UriSchemeHttp
                , record.Host, record.ServerPort, record.URIStem);
            if (record.URIQuery != "-")
                ub.Query = record.URIQuery;

            record.Uri = ub.Uri;
        }
    }

    public class W3CRecord : WebRecord
    {
        public DateTime Date;
        public DateTime Time;
        public IPAddress ClientIPAddress;
        public string UserName;
        public string ServiceName;
        public string ServerName;
        public IPAddress ServerIPAddress;
        public int ServerPort;
        public string URIStem;
        public string URIQuery;
        public HttpStatusCode ProtocolStatus;
        public int ProtocolSubstatus;
        public int Win32Status;
        public string ProtocolVersion;
        public string Host;
        public string UserAgent;
        public string Cookie;
        public string Referrer;
    }

    #endregion
}