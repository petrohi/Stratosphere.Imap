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

        private static void VerifyDecoding(string encoded, string expectedDecoded)
        {
            var decoded = RFC2047Decoder.Parse(encoded);
            Assert.AreEqual(expectedDecoded, decoded);
        }
    }
}
