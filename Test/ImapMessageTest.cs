using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratosphere.Imap.Test
{
    [TestClass]
    public class ImapMessageTest
    {
        [TestMethod]
        public void ParticipantsRFC2047Decoded()
        {
            const string EncodedDisplayName = "=?utf-8?B?5YiY5YWL5bOw?=";
            const string EncodedUser = "someuser";
            const string EncodedHost = "someplace.com";

            const string ExpectedDecodedDisplayName = "刘克峰";

            const string Input = "* 54 FETCH (X-GM-MSGID 1379514999738475089 UID 79 FLAGS () ENVELOPE (\"Fri, 9 Sep 2011 15:38:52 -0700\" \"Simple message\" ((\"" + EncodedDisplayName + "\" NIL \"" + EncodedUser + "\" \"" + EncodedHost + "\")) ((\"" + EncodedDisplayName + "\" NIL \"" + EncodedUser + "\" \"" + EncodedHost + "\")) ((\"" + EncodedDisplayName + "\" NIL \"" + EncodedUser + "\" \"" + EncodedHost + "\")) ((NIL NIL \"tyler.austen.test1\" \"gmail.com\")) NIL NIL NIL \"<00a801cc6f41$408cd2f0$c1a678d0$@com>\") BODYSTRUCTURE ((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL \"7BIT\" 18 2 NIL NIL NIL)(\"TEXT\" \"HTML\" (\"CHARSET\" \"us-ascii\") NIL NIL \"QUOTED-PRINTABLE\" 1657 47 NIL NIL NIL) \"ALTERNATIVE\" (\"BOUNDARY\" \"----=_NextPart_000_00A9_01CC6F06.942DFAF0\") NIL NIL))";

            var list = ImapList.Parse(Input);
            var message = new ImapMessage(long.Parse(list.GetStringAt(1)), list.GetListAt(3));

            var sender = message.From;

            Assert.AreEqual(sender.DisplayName, ExpectedDecodedDisplayName);
        }

    }
}
