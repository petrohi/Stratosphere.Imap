// Copyright (c) 2009 7Clouds

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Security.Authentication;

namespace Stratosphere.Imap
{
    [Flags]
    public enum ImapFetchOption
    {
        Envelope = 0x1,
        Flags = 0x2,
        BodyStructure = 0x4
    }

    public class ParseFailureDetail
    {
        public ParseFailureDetail(Exception ex, string line) 
        { 
            Exception = ex;
            Line = line;
        }
        public readonly Exception Exception;
        public readonly string Line;
    }

    public class ParseFailureEventArgs : EventArgs
    {
        public ParseFailureEventArgs(IEnumerable<ParseFailureDetail> details) 
        { 
            if (null != details)
            {
                Details = details;
            }
            else
            {
                Details = __empty;
            }
        }

        public readonly IEnumerable<ParseFailureDetail> Details;

        private static readonly ParseFailureDetail[] __empty = new ParseFailureDetail[]{};
    }

    public sealed class ImapClient : IDisposable
    {
        private readonly NetworkCredential _credentials;
        private readonly string _hostName;
        private readonly int _portNumber;
        private readonly bool _enableSsl;

        private TcpClient _tcpClient;
        private StreamReader _reader;
        private StreamWriter _writer;

        private long _nextCommandNumber;
        public long NextCommandNumber { get { return _nextCommandNumber; } }

        public EventHandler<ParseFailureEventArgs> ParseFailures;

        private void OnParseFailures(IEnumerable<ParseFailureDetail> details)
        {
            if (null != ParseFailures)
            {
                ParseFailures(this, new ParseFailureEventArgs(details));
            }
        }

        public ImapClient(string hostName, int portNumber, bool enableSsl, NetworkCredential credentials)
        {
            _hostName = hostName;
            _portNumber = portNumber;
            _enableSsl = enableSsl;
            _credentials = credentials;
        }

        public bool TryLogin()
        {
            if (InitializeConnection())
            {
                SendReceiveResult result = SendReceive(string.Format("LOGIN {0} {1}", _credentials.UserName, _credentials.Password));

                if (result.Status == SendReceiveStatus.OK)
                {
                    return true;
                }
                else if (result.Status == SendReceiveStatus.Bad)
                {
                    throw new InvalidOperationException();
                }
            }

            Dispose();
            return false;
        }

        public bool TrySaslLogin(string mechanism, string data)
        {
            if (InitializeConnection())
            {
                SendReceiveResult result = SendReceive(string.Format("AUTHENTICATE {0} {1}", mechanism, data));

                if (result.Status == SendReceiveStatus.OK)
                {
                    return true;
                }
                else //if (result.Status == SendReceiveStatus.Bad)
                {
                    StringBuilder linesBuilder = new StringBuilder();
                    foreach (var line in result.Lines)
                    {
                        linesBuilder.AppendLine(line);
                    }
                    throw new ArgumentException(linesBuilder.ToString(), "data");
                }
            }

            Dispose();
            return false;
        }

        private bool InitializeConnection()
        {
            bool isOk = false;

            if (null == _tcpClient)
            {
                _tcpClient = new TcpClient(_hostName, _portNumber);

                if (_enableSsl)
                {
                    SslStream sslStream = new SslStream(_tcpClient.GetStream(), false);
                    _disposables.Add(sslStream);

                    sslStream.AuthenticateAsClient(_hostName, null, SslProtocols.Tls, false);

                    _reader = new StreamReader(sslStream, Encoding.ASCII);
                    _writer = new StreamWriter(sslStream, Encoding.ASCII);
                }
                else
                {
                    NetworkStream stream = _tcpClient.GetStream();
                    _disposables.Add(stream);

                    _reader = new StreamReader(stream, Encoding.ASCII);
                    _writer = new StreamWriter(stream, Encoding.ASCII);
                }

                string response = _reader.ReadLine();

                isOk = (response.StartsWith("* OK"));
            }

            return isOk;
        }

        public IEnumerable<string> ListFolders(string reference, string wildcard)
        {
            SendReceiveResult result = SendReceive(string.Format("LIST \"{0}\" \"{1}\"", reference, wildcard));

            if (result.Status == SendReceiveStatus.OK)
            {
                foreach (string line in result.Lines)
                {
                    ImapList list = ImapList.Parse(line);
                    yield return list.GetStringAt(4);
                }
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }
        }

        public IEnumerable<KeyValuePair<string, IEnumerable<string>>> ListFoldersWithFlags(string reference, string wildcard, bool useXList)
        {
            SendReceiveResult result = SendReceive(string.Format("{0}LIST \"{1}\" \"{2}\"", 
                (useXList ? "X" : string.Empty), reference, wildcard));

            if (result.Status == SendReceiveStatus.OK)
            {
                foreach (string line in result.Lines)
                {
                    ImapList list = ImapList.Parse(line);
                    List<string> flags = new List<string>();

                    var flagsList = list.GetListAt(2);
                    for (int i = 0; i < flagsList.Count; ++i)
                    {
                        if (flagsList.IsStringAt(i))
                        {
                            flags.Add(flagsList.GetStringAt(i));
                        }
                    }

                    yield return new KeyValuePair<string, IEnumerable<string>>(list.GetStringAt(4), flags);
                }
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }
        }

        public ImapFolder SelectedFolder { get; private set; }

        public ImapFolder SelectFolder(string folderName)
        {
            return SelectFolder(folderName, false);
        }

        public ImapFolder SelectFolder(string folderName, bool isReadonly)
        {
            ImapFolder folder = null;
            if (string.IsNullOrEmpty(folderName))
            {
                SendReceive("UNSELECT");
            }
            else
            {
                string selectionKeyword = isReadonly ? "EXAMINE" : "SELECT";
                SendReceiveResult result = SendReceive(string.Format("{0} \"{1}\"", selectionKeyword, folderName));

                if (result.Status == SendReceiveStatus.OK)
                {
                    folder = new ImapFolder(folderName, result.Lines);
                }
                else if (result.Status == SendReceiveStatus.Bad)
                {
                    throw new InvalidOperationException();
                }
            }

            SelectedFolder = folder;
            return folder;
        }

        private static string FormatSequence(long begin, long end)
        {
            return FormatSequence(begin, end, false);
        }

        private static string FormatSequence(long begin, long end, bool isUidSet)
        {
            return string.Format("{0}:{1}", 
                                 (!isUidSet && begin == -1) ? "*" : begin.ToString(), 
                                 (!isUidSet && end == -1) ? "*" : end.ToString());
        }

        public IEnumerable<long> FetchUids(long beginNumber, long endNumber)
        {
            return FetchUids(beginNumber, endNumber, false);
        }

        public IEnumerable<long> FetchUids(long beginNumber, long endNumber, bool isUidSet)
        {
            List<long> uids = new List<long>();
            SendReceiveResult result = SendReceive(
                string.Format("{0}FETCH {1} (UID)", isUidSet ? "UID " : string.Empty, FormatSequence(beginNumber, endNumber, isUidSet)));

            if (result.Status == SendReceiveStatus.OK)
            {
                SafeEnumerateLines(result.Lines, (line) =>
                {
                    ImapList list = ImapList.Parse(line);

                    if (list.GetStringAt(0) == "*" &&
                        list.GetStringAt(2) == "FETCH" &&
                        list.IsStringAt(1) &&
                        list.IsListAt(3))
                    {
                        uids.Add(long.Parse(list.GetListAt(3).GetStringAt(1)));
                    }

                    return true;
                });

            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            return uids;
        }

        public IEnumerable<ImapMessage> FetchMessages(long beginUid, long endUid, ImapFetchOption option)
        {
            return FetchMessages(beginUid, endUid, option, null);
        }

        public IEnumerable<ImapMessage> FetchMessages(long beginUid, long endUid, ImapFetchOption option, IEnumerable<string> extensionParameterNames)
        {
            List<KeyValuePair<Exception, string>> parseFailureLines = new List<KeyValuePair<Exception, string>>();

            List<ImapMessage> messages = new List<ImapMessage>();
            StringBuilder commandBuilder = new StringBuilder("UID FETCH ");
            commandBuilder.Append(FormatSequence(beginUid, endUid));
            commandBuilder.Append(" (UID");
            
            if ((option & ImapFetchOption.Envelope) != 0)
            {
                commandBuilder.Append(" ENVELOPE");
            }
            
            if ((option & ImapFetchOption.Flags) != 0)
            {
                commandBuilder.Append(" FLAGS");
            }
            
            if ((option & ImapFetchOption.BodyStructure) != 0)
            {
                commandBuilder.Append(" BODYSTRUCTURE");
            }

            if (null != extensionParameterNames)
            {
                foreach (var paramName in extensionParameterNames)
                {
                    commandBuilder.Append(" ");
                    commandBuilder.Append(paramName.Trim());
                }
            }

            commandBuilder.Append(')');

            SendReceiveResult result = SendReceive(commandBuilder.ToString());

            if (result.Status == SendReceiveStatus.OK)
            {
                SafeEnumerateLines(result.Lines, (line) =>
                {
                    ImapList list = ImapList.Parse(line);

                    if (list.GetStringAt(0) == "*" &&
                        list.GetStringAt(2) == "FETCH" &&
                        list.IsStringAt(1) &&
                        list.IsListAt(3))
                    {
                        messages.Add(new ImapMessage(long.Parse(list.GetStringAt(1)), list.GetListAt(3), extensionParameterNames));
                    }

                    return true;
                });
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            return messages;
        }

        public void SetDeleted(long beginUid, long endUid)
        {
            StoreFlag(beginUid, endUid, "\\Deleted", FlagOperation.Set);
        }

        private enum FlagOperation
        {
            Set,
            Reset
        }

        private void StoreFlag(long beginUid, long endUid, string flag, FlagOperation operation)
        {
            string sign = operation == FlagOperation.Set ? "+" : "-";
            SendReceiveResult result = SendReceive(string.Format("UID STORE {0} {1}FLAGS ({2})", FormatSequence(beginUid, endUid), sign, flag));

            if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }
        }

        public object FetchSection(long uid, ImapBodyPart part)
        {
            return FetchSection(uid, part, false);
        }

        /// <summary>
        /// Enumerates the lines, and if there is a handler specified for ParseFailures event, will
        /// just accumulate failure details, skipping to next line.. then once all lines are processed,
        /// fires the ParseFailures event with the details...   if no handler is registered, then 
        /// any exception will be thrown as-is, enumeration will stop, and no event is fired.
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="iterationOperation"></param>
        private void SafeEnumerateLines(IEnumerable<string> lines, Func<string, bool> iterationOperation)
        {
            if (null != lines)
            {
                List<ParseFailureDetail> failureDetails = null;
                if (null != ParseFailures)
                {
                    failureDetails = new List<ParseFailureDetail>();
                }

                foreach (var line in lines)
                {
                    try
                    {
                        bool keepGoing = iterationOperation(line);
                        if (!keepGoing)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (null == failureDetails)
                        {
                            throw;
                        }
                        else
                        {
                            failureDetails.Add(new ParseFailureDetail(ex, line));
                        }
                    }
                }

                if (null != failureDetails && failureDetails.Count > 0)
                {
                    OnParseFailures(failureDetails);
                }
            }
        }

        public object FetchSection(long uid, ImapBodyPart part, bool peek)
        {
            SendReceiveResult result = SendReceive(string.Format("UID FETCH {0} BODY{1}[{2}]", 
                uid, peek ? ".PEEK" : string.Empty, part.Section));

            byte[] bytes = null;
            Encoding enc = null;
            try
            {
                enc = Encoding.GetEncoding(part.ContentType.CharSet.Trim(new char[] { '"' }));
            }
            catch (ArgumentException) { }

            if (null == enc)
            {
                // Fall back on ASCII if we have any trouble getting the encoding for
                // specified character set.
                enc = ASCIIEncoding.ASCII;
            }

            if (result.Status == SendReceiveStatus.OK)
            {
                ImapList sectionList = null;
                string sectionLine = null;

                SafeEnumerateLines(result.Lines, (line) =>
                {
                    bool keepGoing = true;
                    if (sectionList == null)
                    {
                        ImapList list = ImapList.Parse(line);

                        if (list.GetStringAt(0) == "*" &&
                            list.GetStringAt(2) == "FETCH")
                        {
                            sectionList = list;
                        }
                    }
                    else if (sectionLine == null)
                    {
                        sectionLine = line;
                    }
                    else if (line == ")")
                    {
                        if (!string.IsNullOrEmpty(sectionLine))
                        {
                            if (part.Encoding.Equals("BASE64", StringComparison.InvariantCultureIgnoreCase))
                            {
                                try
                                {
                                    bytes = Convert.FromBase64String(sectionLine);
                                }
                                catch
                                {
                                }
                            }
                            else if (
                                part.Encoding.Equals("7BIT", StringComparison.InvariantCultureIgnoreCase) ||
                                part.Encoding.Equals("8BIT", StringComparison.InvariantCultureIgnoreCase))
                            {
                                bytes = Encoding.ASCII.GetBytes(sectionLine);
                            }
                            else if (
                                part.Encoding.Equals("QUOTED-PRINTABLE", StringComparison.InvariantCultureIgnoreCase))
                            {
                                var qpDecoded = RFC2047Decoder.ParseQuotedPrintable(enc, sectionLine);
                                bytes = enc.GetBytes(qpDecoded);
                            }
                            else if (
                                part.Encoding.Equals("BINARY", StringComparison.InvariantCultureIgnoreCase))
                            {
                                bytes = Encoding.ASCII.GetBytes(sectionLine);
                            }
                        }

                        keepGoing = false;
                    }

                    return keepGoing;
                });

            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            if (bytes != null && part.ContentType.CharSet != null)
            {
                try
                {
                    string intermediate = enc.GetString(bytes);
                    return intermediate;
                }
                catch (ArgumentException) { }
            }

            return bytes;
        }

        public enum SendReceiveStatus
        {
            OK = 0,
            No,
            Bad
        }

        public struct SendReceiveResult
        {
            public SendReceiveResult(SendReceiveStatus status, IEnumerable<string> lines)
            {
                Status = status;
                Lines = lines;
            }

            public readonly SendReceiveStatus Status;
            public readonly IEnumerable<string> Lines;
        }

        public SendReceiveResult SendReceive(string command)
        {
            string commandId = string.Format("{0}", _nextCommandNumber++);

            _writer.Write("{0} {1}\r\n", commandId, command);
            _writer.Flush();

            return ReadResponse(_reader, commandId);
        }

        public static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMinutes(2.0);
        private TimeSpan _readTimeout = DefaultReadTimeout;
        public TimeSpan ReadTimeout
        {
            get { return _readTimeout; }
            set { _readTimeout = value; }
        }

        internal static SendReceiveResult ReadResponse(StreamReader reader, string commandId)
        {
            return ReadResponse(reader, commandId, DefaultReadTimeout);
        }

        internal static SendReceiveResult ReadResponse(StreamReader reader, string commandId, TimeSpan readTimeout)
        {
            DateTime startTime = DateTime.Now;

            SendReceiveStatus status = SendReceiveStatus.OK;
            List<string> lines = new List<string>();

            bool expectingLine = true;

            do
            {
                string line = reader.ReadLine();

                if (string.IsNullOrEmpty(line))
                {
                    if ( (DateTime.Now - startTime) > readTimeout )
                    {
                        throw new IOException(string.Format("Received empty responses from server longer than [{0}].  Aborting read attempt.", readTimeout.ToString()));
                    }
                    else
                    {
                        // Let's just skip blank lines instead of throwing empty IOException..
                        continue;
                    }
                }
                // Reset as we've gotten a line of data now.
                startTime = DateTime.Now;

                int begin = -1;

                if (IsLiteralSignalLine(line, out begin))
                {
                    int length;

                    if (int.TryParse(line.Substring(begin + 1, line.Length - begin - 2), out length))
                    {
                        char[] buffer = new char[length];

                        reader.ReadBlock(buffer, 0, length);

                        lines.Add(line);
                        line = new string(buffer);
                    }
                }
                else if (line.StartsWith(string.Format("{0} OK", commandId)))
                {
                    expectingLine = false;
                }
                else if (line.StartsWith(string.Format("{0} NO", commandId)))
                {
                    status = SendReceiveStatus.No;
                    expectingLine = false;
                }
                else if (line.StartsWith(string.Format("{0} BAD", commandId)))
                {
                    status = SendReceiveStatus.Bad;
                    expectingLine = false;
                }

                lines.Add(line);
            }
            while (expectingLine);

            var combinedLines = CombineSplitLines(commandId, lines);

            return new SendReceiveResult(status, combinedLines);
        }

        private static bool IsLiteralSignalLine(string line)
        {
            int dummy = -1; ;
            return IsLiteralSignalLine(line, out dummy);
        }

        private static bool IsLiteralSignalLine(string line, out int begin)
        {
            begin = -1;
            return (line.Length != 0 && line[line.Length - 1] == '}' && (begin = line.LastIndexOf('{')) != -1);
        }

        internal static IEnumerable<string> CombineSplitLines(string commandId, IEnumerable<string> lines)
        {
            List<string> result = new List<string>();
            StringBuilder accumulatedLine = new StringBuilder();
            bool isQuoted = false;
            bool isLiteralSignaled = false;
            int nonQuotedOpenBraceCount = 0;
            int nonQuotedCloseBraceCount = 0;

            foreach (var line in lines)
            {
                if (isLiteralSignaled)
                {
                    result.Add(accumulatedLine.ToString());
                    result.Add(line);
                    accumulatedLine = new StringBuilder();
                    isLiteralSignaled = false;
                    continue;
                }
                else if (!IsLineBalanced(isQuoted, nonQuotedOpenBraceCount, nonQuotedCloseBraceCount))
                {
                    accumulatedLine.Append(line);
                }
                else
                {
                    if (IsLineNonProtocolPrefix(line, commandId))
                    {
                        accumulatedLine.Append(line);
                    }
                    else
                    {
                        string accumulated = accumulatedLine.ToString();
                        if (!string.IsNullOrEmpty(accumulated))
                        {
                            result.Add(accumulated);
                        }
                        isQuoted = false;
                        nonQuotedOpenBraceCount = 0;
                        nonQuotedCloseBraceCount = 0;

                        accumulatedLine = new StringBuilder(line);
                    }
                }

                isLiteralSignaled = IsLiteralSignalLine(line);

                UpdateLineCounters(line, ref isQuoted, ref nonQuotedOpenBraceCount, ref nonQuotedCloseBraceCount);
            }

            string finalAccumulated = accumulatedLine.ToString();
            if (!string.IsNullOrEmpty(finalAccumulated))
            {
                result.Add(finalAccumulated);
            }

            return result;
        }

        private static bool IsLineBalanced(bool isQuoted, int nonQuotedOpenBraceCount, int nonQuotedCloseBraceCount)
        {
            return (nonQuotedCloseBraceCount == nonQuotedOpenBraceCount && !isQuoted);
        }

        private static bool IsLineNonProtocolPrefix(string line, string commandId)
        {
            return !line.StartsWith("*") && !line.StartsWith("+") && !line.StartsWith(commandId);
        }

        private static void UpdateLineCounters(string line, ref bool isQuoted, ref int accumulatedOpenBraceCount, ref int accumulatedCloseBraceCount)
        {
            foreach (char c in line)
            {
                switch (c)
                {
                    case '\"':
                        isQuoted = !isQuoted;
                        break;

                    case '(':
                        if (!isQuoted)
                        {
                            ++accumulatedOpenBraceCount;
                        }
                        break;

                    case ')':
                        if (!isQuoted)
                        {
                            ++accumulatedCloseBraceCount;
                        }
                        break;

                    default:
                        // Ignore any other characters.
                        break;
                }
            }
        }

        public void Dispose()
        {
            ParseFailures = null;

            if (null != _reader)
            {
                _reader.Dispose();
                _reader = null;
            }

            if (null != _writer)
            {
                try
                {
                    _writer.Dispose();
                }
                catch (Exception) { }
                _writer = null;
            }

            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient = null;
            }
        }

        /// <summary>
        /// Member for gathering disposables that must be disposed during ImapClient's Dispose()
        /// implementation.
        /// </summary>
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
    }
}