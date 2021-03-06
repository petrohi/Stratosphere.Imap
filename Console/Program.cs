﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;
using System.Net;

namespace Stratosphere.Imap.Console
{
    /// <summary>
    /// A very simple info dump sample.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            if (!CheckArgs(args))
            {
                return;
            }

            var hostAndPort = args[0].Split(':');
            string host = hostAndPort[0];
            int port = int.Parse(hostAndPort[1]);
            string user = args[1];
            string password = args[2];
            string folder = args[3];

            List<string> paramNames = new List<string>();
            if (args.Length > 4)
            {
                for (int i = 4; i < args.Length; ++i)
                {
                    paramNames.Add(args[i]);
                }
            }

            using (var client = new ImapClient(host, port, true, new NetworkCredential(user, password)))
            {
                client.ParseFailures += HandleParseFailures;

                bool loginOk = false;

                if (user.Equals("XOAUTH", StringComparison.InvariantCultureIgnoreCase))
                {
                    loginOk = client.TrySaslLogin(user, password);
                }
                else
                {
                    loginOk = client.TryLogin();
                }

                if (loginOk)
                {
                    System.Console.WriteLine("INFO: Login to [{0}:{1}] succeeded for user [{2}].", host, port, user);
                    ImapFolder f = client.SelectFolder(folder);
                    System.Console.WriteLine("INFO: 'Next UID value' is [{0}].", f.UidNext);
                    string cmd = null;
                    string upperCmd = null;

                    if (f != null)
                    {
                        System.Console.WriteLine();
                        System.Console.WriteLine(">> To exit, type 'EXIT';  To just dump messages in selected folder 'DUMP [lowUid[:highUid]]';");
                        System.Console.WriteLine(">> to just dump a message body 'BODY msgUid'; otherwise issue IMAP command with the interactive");
                        System.Console.WriteLine(">> console.");
                        System.Console.WriteLine();

                        while (true)
                        {
                            System.Console.Write("C: {0} ", client.NextCommandNumber);

                            cmd = System.Console.ReadLine().Trim();
                            upperCmd = cmd.ToUpperInvariant();
                            if (string.IsNullOrEmpty(cmd))
                            {
                                continue;
                            }
                            if (upperCmd.StartsWith("EXIT"))
                            {
                                return;
                            }
                            else if (upperCmd.StartsWith("DUMP"))
                            {
                                DumpMessages(paramNames, client, f, upperCmd);
                            }
                            else if (upperCmd.StartsWith("BODY"))
                            {
                                DumpBody(paramNames, client, f, upperCmd);
                            }
                            else
                            {
                                var resp = client.SendReceive(cmd);
                                foreach (var line in resp.Lines)
                                {
                                    System.Console.WriteLine("S: {0}", line);
                                }
                            }
                        }
                    }
                    else
                    {
                        Usage(string.Format("ERROR: Server [{0}:{1}] does not support folder [{2]}.", host, port, folder));
                    }
                }
                else
                {
                    Usage(string.Format("ERROR: Login to [{0}:{1}] failed for user [{2}].", host, port, user));
                }
            }
        }

        private static void HandleParseFailures(object sender, ParseFailureEventArgs args)
        {
            foreach (var detail in args.Details)
            {
                System.Console.WriteLine("Parse failure on line: [{0}]", detail.Line);
            }
        }

        private static void DumpBody(List<string> paramNames, ImapClient client, ImapFolder f, string upperCmd)
        {
            long uid = -1;
            string uidString = upperCmd.Substring("BODY".Length).Trim();
            if (!long.TryParse(uidString, out uid))
            {
                throw new ArgumentException("Must include valid message UID for BODY dump.", "uid");
            }

            var message = client.FetchMessages(uid, uid, ImapFetchOption.BodyStructure).FirstOrDefault();
            if (null == message)
            {
                System.Console.WriteLine("Message with UID [{0}] not found.  Can not dump its body.", uid);
            }
            else
            {
                foreach (var part in message.BodyParts)
                {
                    DumpBodyPart(client, uid, part);
                }
            }
        }

        private static void DumpBodyPart(ImapClient client, long uid, ImapBodyPart part)
        {
            const string ItemFormat = "\t{0}:\t{1}";
            System.Console.WriteLine("==========");
            System.Console.WriteLine("MSG PART SECTION [{0}]  ({1} bytes):", part.Section, part.Size);
            System.Console.WriteLine(ItemFormat, "Encoding", part.Encoding);
            System.Console.WriteLine(ItemFormat, "ContentType", part.ContentType.ToString());

            if (!string.IsNullOrEmpty(part.ContentType.CharSet))
            {
                System.Console.WriteLine(ItemFormat, "DATA", "\n");
                var sectionData = client.FetchSection(uid, part, true);
                if (sectionData is string)
                {
                    System.Console.WriteLine(sectionData as string);
                }
                else
                {
                    var arr = sectionData as byte[];
                    System.Console.WriteLine("<< BINARY DATA NOT DUMPED - {0} bytes >>", arr.Length);
                }
                System.Console.WriteLine();
            }
            
            System.Console.WriteLine("----------");
        }

        private static void DumpMessages(List<string> paramNames, ImapClient client, ImapFolder f, string upperCmd)
        {
            long lowUid = 1;
            long highUid = f.UidNext;

            string dumpRange = upperCmd.Substring("DUMP".Length).Trim();
            var rangeArgs = dumpRange.Split(new char[] { ':', ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (rangeArgs.Length > 0)
            {
                if (!long.TryParse(rangeArgs[0], out lowUid))
                {
                    lowUid = 1;
                }
            }

            if (rangeArgs.Length > 1)
            {
                if (!long.TryParse(rangeArgs[1], out highUid))
                {
                    highUid = f.UidNext;
                }
            }

            System.Console.WriteLine("Fetching UIDs in range [{0}:{1}] from folder [\"{2}\"]...",
                lowUid, highUid, client.SelectedFolder.Name);

            long[] msUids = client.FetchUids(lowUid, highUid, true).ToArray();

            System.Console.WriteLine("Fetching [{0}] headers...", msUids.Length);

            const int HeaderBlockSize = 1000;

            for (int i = 0; i < msUids.Length; i += HeaderBlockSize)
            {
                int j = i + HeaderBlockSize;

                if (j >= msUids.Length)
                {
                    j = msUids.Length - 1;
                }

                ImapFetchOption opts =
                    ImapFetchOption.Envelope
                    | ImapFetchOption.BodyStructure
                    | ImapFetchOption.Flags;

                ImapMessage[] ms = client.FetchMessages(msUids[i], msUids[j], opts, paramNames).ToArray();

                foreach (ImapMessage m in ms)
                {
                    DumpMessageInfo(m);
                    System.Console.WriteLine();
                }
            }
        }

        private static void DumpMessageInfo(ImapMessage m)
        {
            const string ItemFormat = "\t{0}:\t{1}";
            System.Console.WriteLine("==========");
            System.Console.WriteLine("MSG UID [{0}]:", m.Uid);
            System.Console.WriteLine(ItemFormat, "Subj.", m.Subject);
            System.Console.WriteLine(ItemFormat, "Sender", m.Sender.ToString());
            System.Console.WriteLine(ItemFormat, "Date", m.Timestamp.ToString());
            if (null != m.ExtensionParameters && m.ExtensionParameters.Count > 0)
            {
                System.Console.WriteLine(ItemFormat, "Extension Parameters:", string.Empty);
                const string ExtensionParamFormat = "\t" + ItemFormat;
                foreach (var paramName in m.ExtensionParameters.Keys)
                {
                    string paramValue = StringifyExtensionParamValue(m.ExtensionParameters[paramName]);

                    System.Console.WriteLine(ExtensionParamFormat, paramName, paramValue);
                }
            }
            System.Console.WriteLine("----------");
        }

        private static string StringifyExtensionParamValue(object value)
        {
            string valueString = value as string;

            if (null == valueString)
            {
                StringBuilder builder = new StringBuilder("[");

                List<object> valueList = value as List<object>;

                foreach (var subValue in valueList)
                {
                    builder.Append(StringifyExtensionParamValue(subValue));
                    builder.Append(" ");
                }

                valueString = builder.ToString().TrimEnd() + "]";
            }

            return valueString;
        }

        private static bool CheckArgs(string[] args)
        {
            if (args.Length < 4)
            {
                Usage("ERROR: Missing arguments.");
                return false;
            }
            return true;
        }

        private static void Usage(string message)
        {
            System.Console.WriteLine();
            System.Console.WriteLine(">>> {0} <<<", AssemblyFileName);
            System.Console.WriteLine();
            if (!string.IsNullOrEmpty(message))
            {
                System.Console.WriteLine(message);
                System.Console.WriteLine();
            }
            System.Console.WriteLine("USAGE: {0} host:port userName password folderName [paramName1 paramName2 ...]", Path.GetFileNameWithoutExtension(AssemblyFileName));
            System.Console.WriteLine();
            System.Console.WriteLine("\thost:port\t- imap server (eg. \"imap.gmail.com:993\")");
            System.Console.WriteLine("\tuserName\t- the userName to log in as (or 'XOAUTH' to perform SASL XOAUTH login)");
            System.Console.WriteLine("\tpassword\t- the password (or XOAUTH data if userName == 'XOAUTH')");
            System.Console.WriteLine("\tfolderName\t- the folder to dump (eg. \"[Gmail]/All Mail\")");
            System.Console.WriteLine("\t[optional] one or more extension parameter names  (eg. X-GM-MSGID X-GM-THRID etc...)");
            System.Console.WriteLine();
            System.Console.WriteLine("NOTE:  This sample app always uses SSL for connecting to the server.");
        }

        private static readonly string AssemblyFileName 
            = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
    }
}
