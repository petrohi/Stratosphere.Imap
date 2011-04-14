using System;
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
                if (client.TryLogin())
                {
                    System.Console.WriteLine("INFO: Login to [{0}:{1}] succeeded for user [{2}].", host, port, user);
                    ImapFolder f = client.SelectFolder(folder);

                    if (f != null)
                    {
                        System.Console.WriteLine();
                        System.Console.WriteLine(">> To exit, type 'EXIT';  To just dump messages in selected folder 'DUMP',");
                        System.Console.WriteLine(">> otherwise issue IMAP command with the interactive console.");
                        System.Console.WriteLine();

                        bool isDump = false;
                        while (!isDump)
                        {
                            System.Console.Write("C: {0} ", client.NextCommandNumber);
                            string cmd = System.Console.ReadLine().Trim().ToUpperInvariant();

                            switch (cmd)
                            {
                                case "EXIT":
                                    return;
                                    
                                case "DUMP":
                                    isDump = true;
                                    break;

                                default:
                                    {
                                        var resp = client.SendReceive(cmd);
                                        foreach (var line in resp.Lines)
                                        {
                                            System.Console.WriteLine("S: {0}", line);
                                        }
                                    }
                                    break;
                            }
                        }



                        System.Console.WriteLine("Fetching UIDs from folder [\"{0}\"]...", folder);

                        long[] msUids = client.FetchUids(1, -1).ToArray();

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
                    System.Console.WriteLine(ExtensionParamFormat, paramName, m.ExtensionParameters[paramName].ToString());
                }
            }
            System.Console.WriteLine("----------");
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
            System.Console.WriteLine("\tuserName\t- the userName to log in as");
            System.Console.WriteLine("\tpassword\t- the password");
            System.Console.WriteLine("\tfolderName\t- the folder to dump (eg. \"[Gmail]/All Mail\")");
            System.Console.WriteLine("\t[optional] one or more extension parameter names  (eg. X-GM-MSGID X-GM-THRID etc...)");
            System.Console.WriteLine();
            System.Console.WriteLine("NOTE:  This sample app always uses SSL for connecting to the server.");
        }

        private static readonly string AssemblyFileName 
            = Path.GetFileName(Assembly.GetExecutingAssembly().Location);
    }
}
