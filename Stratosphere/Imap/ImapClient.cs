﻿// Copyright (c) 2009 7Clouds

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

        public IEnumerable<string> List(string reference, string wildcard)
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

        public IEnumerable<ImapMessage> Fetch(int beginNumber, int endNumber)
        {
            List<ImapMessage> messages = new List<ImapMessage>();
            SendReceiveResult result = SendReceive(string.Format("FETCH {0}:{1} (ENVELOPE FLAGS BODYSTRUCTURE)", beginNumber, endNumber));

            if (result.Status == SendReceiveStatus.OK)
            {
                foreach (string line in result.Lines)
                {
                    ImapList list = ImapList.Parse(line);

                    if (list.Count >= 3 && list.GetStringAt(0) == "*")
                    {
                        if (list.GetStringAt(2) == "FETCH")
                        {
                            messages.Add(new ImapMessage(list));
                        }
                    }
                }
            }
            else if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }

            return messages;
        }

        public void SetDeleted(int beginNumber, int endNumber)
        {
            StoreFlag(beginNumber, endNumber, "\\Deleted", FlagOperation.Set);
        }

        private enum FlagOperation
        {
            Set,
            Reset
        }

        private void StoreFlag(int beginNumber, int endNumber, string flag, FlagOperation operation)
        {
            string sign = operation == FlagOperation.Set ? "+" : "-";
            SendReceiveResult result = SendReceive(string.Format("STORE {0}:{1} {2}FLAGS ({3})", beginNumber, endNumber, sign, flag));

            if (result.Status == SendReceiveStatus.Bad)
            {
                throw new InvalidOperationException();
            }
        }
        
        public object FetchSection(int number, ImapBodyPart part)
        {
            SendReceiveResult result = SendReceive(string.Format("FETCH {0} BODY[{1}]", number, part.Section));
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
                        int sectionNumber;

                        if (list.GetStringAt(0) == "*" &&
                            list.GetStringAt(2) == "FETCH" &&
                            int.TryParse(list.GetStringAt(1), out sectionNumber) &&
                            sectionNumber == number)
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
                        if (part.Encoding.Equals("BASE64", StringComparison.InvariantCultureIgnoreCase))
                        {
                            bytes = Convert.FromBase64String(sectionLine.Replace("\r\n", string.Empty));
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

            string commandId = string.Format("IMAP{0}", _nextCommandNumber++);

            _writer.Write("{0} {1}\r\n", commandId, command);
            _writer.Flush();

            bool expectingLine = true;

            do
            {
                string line = _reader.ReadLine();
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