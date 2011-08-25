using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratosphere.Imap.Test
{
    [TestClass]
    public class RFC2047DecoderTest
    {
        [TestMethod]
        public void RFC2047_SPEC_CASE_1_BasicEncodedWord()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?=";
            const string Expected = "a";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_2_SurroundingText()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?= b";
            const string Expected = "a b";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_3_AdjacentEncodedWords_Whitespace_1()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?= =?ISO-8859-1?Q?b?=";
            const string Expected = "ab";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_4_AdjacentEncodedWords_Whitespace_2()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?=  =?ISO-8859-1?Q?b?=";
            const string Expected = "ab";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_5_AdjacentEncodedWords_Whitespace_3()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?= \t\r\n  =?ISO-8859-1?Q?b?=";
            const string Expected = "ab";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_6_SpacesEncodedAsUnderscoresInsideEncodedWord()
        {
            const string Encoded = "=?ISO-8859-1?Q?a_b?=";
            const string Expected = "a b";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RFC2047_SPEC_CASE_7_SpaceEncodedAsUnderscoreInsideAdjacentEncodedWords()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?= =?ISO-8859-2?Q?_b?=";
            const string Expected = "a b";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RealWorldData_1()
        {
            const string Encoded = "=?WINDOWS-1252?Q?Gmail=92s_=91People_Widget=92_Takes_On_Ra?= =?WINDOWS-1252?Q?pportive,_No_Browser_Plugin_Required?=";
            const string Expected = "Gmail’s ‘People Widget’ Takes On Rapportive, No Browser Plugin Required";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RealWorldData_2()
        {
            // "Another test...  ?=  ,   ;   =   ==    =2C"
            const string Encoded = "Another test...  ?=  ,   ;   =   ==    =2C";
            const string Expected = "Another test...  ?=  ,   ;   =   ==    =2C";

            VerifyDecoding(Encoded, Expected);
        }

        [TestMethod]
        public void RealWorldData_3_EqualsAsWordsFirstCharacter()
        {
            const string Encoded = "=?ISO-8859-1?Q?a?= =?ISO-8859-1?Q?=2C?= =?ISO-8859-2?Q?_b?= =?ISO-8859-1?Q?=3B=3D?= =?ISO-8859-2?Q?_c?=";
            const string Expected = "a, b;= c";

            VerifyDecoding(Encoded, Expected);
        }

        /*
        [TestMethod]
        public void RealWorldData_4_UnderscoresInSurroundingTextPreserved()
        {
            const string Encoded = "=?WINDOWS-1252?Q?Gmail=92s_=91People_Widget=92?= _Here are _ some underscores =?WINDOWS-1252?Q?_No_Browser_Plugin_Required?=";
            const string Expected = "Gmail’s ‘People Widget’ _Here are _ some underscores No Browser Plugin Required";

            VerifyDecoding(Encoded, Expected);
        }
        */

        [TestMethod]
        public void ParseQuotedPrintable_Basic()
        {
            const string Encoded = "Another test...  ?=3D  =2C   =3B   =3D   =3D=3D    =3D2C";
            const string Expected = "Another test...  ?=  ,   ;   =   ==    =2C";

            var decoded = RFC2047Decoder.ParseQuotedPrintable(Encoding.UTF8, Encoded);
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_LoneEquals_NewlineGetsStripped()
        {
            const string Encoded = "Another test...  =\r\nThis should not be on a new line.";
            const string Expected = "Another test...  This should not be on a new line.";

            var decoded = RFC2047Decoder.ParseQuotedPrintable(Encoding.UTF8, Encoded);
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_LoneEquals_NonNewlinePreserved()
        {
            const string Encoded = "Another test...  =This should not be on a new line.";
            const string Expected = "Another test...  This should not be on a new line.";

            var decoded = RFC2047Decoder.ParseQuotedPrintable(Encoding.UTF8, Encoded);
            Assert.AreEqual(Expected, decoded);
        }

        [TestMethod]
        public void ParseQuotedPrintable_UTF8_MultibyteCharacters()
        {
            const string Encoded = "We=E2=80=99re";
            const string Expected = "We\u2019re";

            var decoded = RFC2047Decoder.ParseQuotedPrintable(Encoding.UTF8, Encoded);
            Assert.AreEqual(Expected, decoded);
        }

        private static void VerifyDecoding(string encoded, string expectedDecoded)
        {
            var decoded = RFC2047Decoder.Parse(encoded);
            Assert.AreEqual(expectedDecoded, decoded);
        }
    }
}
