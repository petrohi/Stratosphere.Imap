using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratosphere.Imap.Test
{
    [TestClass]
    public class ImapListTest
    {
        [TestMethod]
        public void EmptyList()
        {
            var list = ImapList.Parse(string.Empty);
            Assert.IsNotNull(list);
            Assert.IsFalse(list.IsStringAt(0));
            Assert.IsFalse(list.IsListAt(0));
        }

        [TestMethod]
        public void Basic()
        {
            //const string Input = "(UID 25 ENVELOPE (\"Mon, 23 May 2011 14:43:49 -0700\" \"Subj with embedded \\\"quotes\\\" ..\" ((\"Jiggy Wiggy\" NIL \"jiggy\" \"test.com\")) ((\"Jiggy Wiggy\" NIL \"jiggy\" \"test.com\")) ((\"Jiggy Wiggy\" NIL \"jiggy\" \"test.com\")) ((NIL NIL \"wizzle.wazzle\" \"test.com\")) NIL NIL NIL \"<008701cc1992$812e8150$838b83f0$@com>\"))";
            //const string Input = "(\"Value with embedded \\\"quotes\\\" ..\")";
            const string Input = "(0.0 0.1 (0.2.0 0.2.1) 0.3) 1 (2.0 2.1)";

            var parsedList = ImapList.Parse(Input);

            // Verify top-level structure
            Assert.IsTrue(parsedList.IsListAt(0));

            Assert.IsTrue(parsedList.IsStringAt(1));
            Assert.AreEqual("1", parsedList.GetStringAt(1));

            Assert.IsTrue(parsedList.IsListAt(2));

            // Verify 0.* structure
            var l0 = parsedList.GetListAt(0);
            Assert.IsTrue(l0.IsStringAt(0));
            Assert.AreEqual("0.0", l0.GetStringAt(0));

            Assert.IsTrue(l0.IsStringAt(1));
            Assert.AreEqual("0.1", l0.GetStringAt(1));

            Assert.IsTrue(l0.IsListAt(2));

            Assert.IsTrue(l0.IsStringAt(3));
            Assert.AreEqual("0.3", l0.GetStringAt(3));

            // Verify 0.2.* structure
            var l02 = l0.GetListAt(2);
            Assert.IsTrue(l02.IsStringAt(0));
            Assert.AreEqual("0.2.0", l02.GetStringAt(0));

            Assert.IsTrue(l02.IsStringAt(1));
            Assert.AreEqual("0.2.1", l02.GetStringAt(1));

            // Verify 2.* structure
            var l2 = parsedList.GetListAt(2);
            Assert.IsTrue(l2.IsStringAt(0));
            Assert.AreEqual("2.0", l2.GetStringAt(0));

            Assert.IsTrue(l2.IsStringAt(1));
            Assert.AreEqual("2.1", l2.GetStringAt(1));
        }

        [TestMethod]
        public void StringPartsWithQuotes()
        {
            const string Input = "\"Value with embedded \\\"quotes\\\" ..\"";
            const string ExpectedParsedString = "Value with embedded \"quotes\" ..";

            var parsedList = ImapList.Parse(Input);
            Assert.IsTrue(parsedList.IsStringAt(0));

            var parsedString = parsedList.GetStringAt(0);
            Assert.AreEqual(ExpectedParsedString, parsedString);
        }

        [TestMethod]
        public void Escaped_NonQuoteChar()
        {
            const string Input = "\"Value with escaped \\non quote char ..\"";
            const string ExpectedParsedString = "Value with escaped \\non quote char ..";

            var parsedList = ImapList.Parse(Input);
            Assert.IsTrue(parsedList.IsStringAt(0));

            var parsedString = parsedList.GetStringAt(0);
            Assert.AreEqual(ExpectedParsedString, parsedString);
        }

        [TestMethod]
        public void Escaped_NonQuoteChar_EscapeChars()
        {
            const string Input = "\"Some \\\\ backslashes \\\\\\\\\\\\ and explicit \\\\\\\" escape-like quote\"";
            const string ExpectedParsedString = "Some \\ backslashes \\\\\\ and explicit \\\" escape-like quote";

            var parsedList = ImapList.Parse(Input);
            Assert.IsTrue(parsedList.IsStringAt(0));

            var parsedString = parsedList.GetStringAt(0);
            Assert.AreEqual(ExpectedParsedString, parsedString);
        }
    }
}
