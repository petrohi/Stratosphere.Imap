using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Stratosphere.Imap;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Stratosphere.Imap.Test
{
    [TestClass]
    public class ImapClientTest
    {
        private const string DefaultCommandId = "5";
        private static readonly TimeSpan DefaultReadTimeout = TimeSpan.FromMilliseconds(10);

        [TestMethod]
        public void ReadResponse_Malformed()
        {
            var badData = new string[] {"This is junk"};
            using (var reader = CreateStreamReaderFor(badData))
            {
                AssertEx.Throws<IOException>(() => ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout));
            }
        }

        [TestMethod]
        public void ReadResponse_Basic_OK()
        {
            var data = new string[] {
                "* CAPABILITY IMAP4rev1 STARTTLS AUTH=GSSAPI LOGINDISABLED",
                FinishingLine(DefaultCommandId)};

            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                AssertEx.SameContents(data, result.Lines);
            }
        }

        [TestMethod]
        public void ReadResponse_SplitLines_Basic()
        {
            var splitParts = new string[] {
                "* CAPABILITY IMAP4rev1 STARTTLS ",
                "AUTH=GSSAPI LOGINDISABLED",
            };

            var data = Combine<string>(
                    splitParts,
                    FinishingLine(DefaultCommandId)).ToArray();

            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                Assert.AreEqual(string.Join(string.Empty, splitParts), result.Lines.First());
            }
        }

        [TestMethod]
        public void ReadResponse_SplitLines_QuotedString_ProtocolPrefix_CommandId()
        {
            var splitParts = new string[] {
                "* CAPABILITY IMAP4rev1 STARTTLS \"Some quoted ",
                DefaultCommandId + " string with internal match of protocol prefix\" ",
                "AUTH=GSSAPI LOGINDISABLED",
            };

            var data = Combine<string>(
                    splitParts,
                    FinishingLine(DefaultCommandId)).ToArray();

            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                Assert.AreEqual(string.Join(string.Empty, splitParts), result.Lines.First());
            }
        }

        [TestMethod]
        public void ReadResponse_SplitLines_QuotedString_UnbalancedBracesInsideQuotes()
        {
            var splitParts = new string[] {
                "* CAPABILITY (IMAP4rev1 STARTTLS \"Some quoted ",
                " string with) internal (match) of )protocol prefix\" ",
                "AUTH=GSSAPI) LOGINDISABLED",
            };

            var data = Combine<string>(
                    splitParts,
                    FinishingLine(DefaultCommandId)).ToArray();

            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                Assert.AreEqual(string.Join(string.Empty, splitParts), result.Lines.First());
            }
        }

        [TestMethod]
        public void ReadResponse_SplitLines_UnbalancedBraces_ProtocolPrefix_Asterisk()
        {
            var splitParts = new string[] {
                "* CAPABILITY (IMAP4rev1 STARTTLS (JIGGY ",
                "+" + " WIGGY) WAGGA WAGGA (ANOTHER OPEN ",
                "AUTH=GSSAPI) LOGINDISABLED) DONE",
            };

            var data = Combine<string>(
                    splitParts, 
                    "*" + " ANOTHER LINE",
                    FinishingLine(DefaultCommandId)).ToArray();
                
            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                Assert.AreEqual(string.Join(string.Empty, splitParts), result.Lines.First());
            }
        }

        private static string FinishingLine(string cmdId)
        {
            return cmdId + " OK Finishing Line here...";
        }

        private static IEnumerable<T> Combine<T>(params object[] args)
        {
            List<T> combined = new List<T>();

            for (int i=0; i < args.Length; ++i)
            {
                object arg = args[i];
                if (arg is T)
                {
                    T value = (T)arg;
                    combined.Add(value);
                }
                else if (arg is IEnumerable<T>)
                {
                    IEnumerable<T> values = (IEnumerable<T>)arg;
                    combined.AddRange(values);
                }
                else
                {
                    string badArg = string.Format("args[{0}]", i);

                    throw new ArgumentException(string.Format("{0} ({1}) is not of type {2} or IEnumerable<{2}>",
                        badArg, arg.ToString(), typeof(T)), badArg);
                }
            }

            return combined;
        }

        private StreamReader CreateStreamReaderFor(string[] data)
        {
            MemoryStream stream = null;
            if (null != data)
            {
                string joined = string.Join(Environment.NewLine, data);
                stream = new MemoryStream(Encoding.UTF8.GetBytes(joined));
            }
            else
            {
                stream = new MemoryStream();
            }
            stream.Position = 0;
            return new StreamReader(stream, Encoding.UTF8);
        }
    }
}
