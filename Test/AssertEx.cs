using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Stratosphere.Imap.Test
{
    public static class AssertEx
    {
        public static void Throws<T>(Action codeToExecute)
            where T : Exception
        {
            Exception ex = null;
            bool correctExceptionCaught = false;

            try
            {
                codeToExecute();
            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(T))
                {
                    correctExceptionCaught = true;
                }
                else
                {
                    ex = e;
                }
            }

            if (!correctExceptionCaught)
            {
                if (null != ex)
                {
                    Assert.Fail(string.Format("Expected [{0}], but caught [{1}]",
                        typeof(T).Name, ex.GetType().Name));
                }
                else
                {
                    Assert.Fail(string.Format("Expected [{0}], but no exception was thrown.",
                        typeof(T).Name));
                }
            }
        }

        public static void SameContents<T>(IEnumerable<T> expected, IEnumerable<T> toCompare)
        {
            T[] expectedArray = expected.ToArray();
            T[] toCompareArray = toCompare.ToArray();

            Assert.AreEqual(expectedArray.Length, toCompareArray.Length);
            for (int i = 0; i < expectedArray.Length; ++i)
            {
                Assert.AreEqual(expectedArray[i], toCompareArray[i], 
                    string.Format("Collections differ at [{0}'th] element.  Expected: [{1}], Actual [{2}]",
                    i, expectedArray[i], toCompareArray[i]));
            }
        }
    }
}
