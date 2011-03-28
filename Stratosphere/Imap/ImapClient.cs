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

        public ImapClient(string hostName, int portNumber, bool enableSsl, NetworkCredential credentials)
        {
            _hostName = hostName;
            _portNumber = portNumber;
            _enableSsl = enableSsl;
            _credentials = credentials;
        }

        public bool TryLogin()
        {
            _tcpClient = new TcpClient(_hostName, _portNumber);

            if (_enableSsl)
            {
                SslStream sslStream = new SslStream(_tcpClient.GetStream(), false);
                sslStream.AuthenticateAsClient(_hostName, null, SslProtocols.Tls, false);

                _reader = new StreamReader(sslStream, Encoding.ASCII);
                _writer = new StreamWriter(sslStream, Encoding.ASCII);
            }
            else
            {
                _reader = new StreamReader(_tcpClient.GetStream(), Encoding.ASCII);
                _writer = new StreamWriter(_tcpClient.GetStream(), Encoding.ASCII);
            }

            string response = _reader.ReadLine();

            if (response.StartsWith("* OK"))
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
        
        public ImapFolder SelectFolder(string folderName)
        {
            ImapFolder folder = null;
            SendReceiveResult result = SendReceive(string.Format("SELECT \"{0}\"", folderName));

            if (result.Status == SendReceiveStatus.OK)
            {
                folder = new ImapFolder(result.Lines);
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            return folder;
        }

        private static string FormatSequence(long begin, long end)
        {
            return string.Format("{0}:{1}", 
                                 begin == -1 ? "*" : begin.ToString(), 
                                 end == -1 ? "*" : end.ToString());
        }

        public IEnumerable<long> FetchUids(long beginNumber, long endNumber)
        {
            List<long> uids = new List<long>();
            SendReceiveResult result = SendReceive(string.Format("FETCH {0} (UID)", 
                                                                 FormatSequence(beginNumber, endNumber)));

            if (result.Status == SendReceiveStatus.OK)
            {
                foreach (string line in result.Lines)
                {
                    ImapList list = ImapList.Parse(line);

                    if (list.GetStringAt(0) == "*" &&
                        list.GetStringAt(2) == "FETCH" &&
                        list.IsStringAt(1) &&                        
                        list.IsListAt(3))
                    {
                        uids.Add(long.Parse(list.GetListAt(3).GetStringAt(1)));
                    }
                }
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            return uids;
        }

        public IEnumerable<ImapMessage> FetchMessages(long beginUid, long endUid, ImapFetchOption option)
        {
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

            commandBuilder.Append(')');

            SendReceiveResult result = SendReceive(commandBuilder.ToString());

            if (result.Status == SendReceiveStatus.OK)
            {
                foreach (string line in result.Lines)
                {
                    ImapList list = ImapList.Parse(line);

                    if (list.GetStringAt(0) == "*" && 
                        list.GetStringAt(2) == "FETCH" &&
                        list.IsStringAt(1) &&
                        list.IsListAt(3))
                    {
                        messages.Add(new ImapMessage(long.Parse(list.GetStringAt(1)), list.GetListAt(3)));
                    }
                }
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

        public object FetchSection(long uid, ImapBodyPart part, bool peek)
        {
            SendReceiveResult result = SendReceive(string.Format("UID FETCH {0} BODY{1}[{2}]", 
                uid, peek ? ".PEEK" : string.Empty, part.Section));

            byte[] bytes = null;

            if (result.Status == SendReceiveStatus.OK)
            {
                ImapList sectionList = null;
                string sectionLine = null;

                foreach (string line in result.Lines)
                {                   
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
                                bytes = Encoding.ASCII.GetBytes(sectionLine);
                            }
                        }

                        break;
                    }
                }
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            if (bytes != null && part.ContentType.CharSet != null)
            {
                try
                {
                    return Encoding.GetEncoding(part.ContentType.CharSet.Trim(new char[] { '"' })).GetString(bytes);
                }
                catch (ArgumentException) { }
            }

            return bytes;
        }

        private enum SendReceiveStatus
        {
            OK = 0,
            No,
            Bad
        }

        private struct SendReceiveResult
        {
            public SendReceiveResult(SendReceiveStatus status, IEnumerable<string> lines)
            {
                Status = status;
                Lines = lines;
            }

            public readonly SendReceiveStatus Status;
            public readonly IEnumerable<string> Lines;
        }

        private SendReceiveResult SendReceive(string command)
        {
            SendReceiveStatus status = SendReceiveStatus.OK;
            List<string> lines = new List<string>();

            string commandId = string.Format("{0}", _nextCommandNumber++);

            _writer.Write("{0} {1}\r\n", commandId, command);
            _writer.Flush();

            bool expectingLine = true;

            do
            {
                string line = _reader.ReadLine();

                if (string.IsNullOrEmpty(line))
                {
                    throw new IOException();
                }

                int begin;

                if (line.Length != 0 && line[line.Length - 1] == '}' && (begin = line.LastIndexOf('{')) != -1)
                {
                    int length;

                    if (int.TryParse(line.Substring(begin + 1, line.Length - begin - 2), out length))
                    {
                        char[] buffer = new char[length];

                        _reader.ReadBlock(buffer, 0, length);

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

            return new SendReceiveResult(status, lines);
        }

        public void Dispose()
        {
            if (_tcpClient != null && _tcpClient.Connected)
            {
                _tcpClient.Close();
            }
        }
    }
}