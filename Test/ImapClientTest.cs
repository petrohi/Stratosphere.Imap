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
                DefaultCommandId + " OK CAPABILITY completed"};

            using (var reader = CreateStreamReaderFor(data))
            {
                var result = ImapClient.ReadResponse(reader, DefaultCommandId, DefaultReadTimeout);
                Assert.AreEqual(ImapClient.SendReceiveStatus.OK, result.Status);
                AssertEx.SameContents(data, result.Lines);
            }
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
