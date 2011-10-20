using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratosphere.Imap.Test
{
    [TestClass]
    public class QuotedPrintableParserTest
    {
        [TestMethod]
        public void ParseQuotedPrintable_Basic()
        {
            const string Encoded = "Another test...  ?=3D  =2C   =3B   =3D   =3D=3D    =3D2C";
            const string Expected = "Another test...  ?=  ,   ;   =   ==    =2C";

            var parser = new QuotedPrintableParser(Encoded);
            var decoded = parser.GetParsedString(Encoding.UTF8);
            
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_LoneEquals_NewlineGetsStripped()
        {
            const string Encoded = "Another test...  =\r\nThis should not be on a new line.";
            const string Expected = "Another test...  This should not be on a new line.";

            var parser = new QuotedPrintableParser(Encoded);
            var decoded = parser.GetParsedString(Encoding.UTF8);
            
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_LoneEquals_NonNewlinePreserved()
        {
            const string Encoded = "Another test...  =This should not be on a new line.";
            const string Expected = "Another test...  This should not be on a new line.";

            var parser = new QuotedPrintableParser(Encoded);
            var decoded = parser.GetParsedString(Encoding.UTF8);
            
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_UTF8_MultibyteCharacters()
        {
            const string Encoded = "We=E2=80=99re";
            const string Expected = "We\u2019re";

            var parser = new QuotedPrintableParser(Encoded);
            var decoded = parser.GetParsedString(Encoding.UTF8);

            Assert.AreEqual(Expected, decoded);
        }
    }
}
