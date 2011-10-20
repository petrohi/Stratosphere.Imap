/// This class adapted from the original RFC2047Decoder class seen here
/// http://blog.crazybeavers.se/index.php/archive/rfc2047decoder-in-c-sharp/
/// 
/// It has been adapted (following http://www.ietf.org/rfc/rfc2047.txt) to:
/// * handle underscores inside encoded-words
/// * strip whitespace between adjacent encoded-words
/// * do not strip whitespace from "surrounding text"

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Globalization;

namespace Stratosphere.Imap
{
    public static class RFC2047Decoder
    {
        public static string Parse(string input)
        {
            StringBuilder sb = new StringBuilder();
            StringBuilder currentWord = new StringBuilder();
            StringBuilder currentSurroundingText = new StringBuilder();
            bool readingWord = false;
            bool hasSeenAtLeastOneWord = false;

            int wordQuestionMarkCount = 0;
            int i = 0;
            while (i < input.Length)
            {
                char currentChar = input[i];
                char peekAhead;
                switch (currentChar)
                {
                    case '=':
                        peekAhead = (i == input.Length - 1) ? ' ' : input[i + 1];

                        if (!readingWord && peekAhead == '?')
                        {
                            if (!hasSeenAtLeastOneWord
                                || (hasSeenAtLeastOneWord && currentSurroundingText.ToString().Trim().Length > 0) )
                            {
                                sb.Append(currentSurroundingText.ToString());
                            }

                            currentSurroundingText = new StringBuilder();
                            hasSeenAtLeastOneWord = true;
                            readingWord = true;
                            wordQuestionMarkCount = 0;
                        }
                        break;

                    case '?':
                        if (readingWord)
                        {
                            wordQuestionMarkCount++;

                            peekAhead = (i == input.Length - 1) ? ' ' : input[i + 1];

                            if (wordQuestionMarkCount > 3 && peekAhead == '=')
                            {
                                readingWord = false;

                                currentWord.Append(currentChar);
                                currentWord.Append(peekAhead);

                                sb.Append(ParseEncodedWord(currentWord.ToString()));
                                currentWord = new StringBuilder();

                                i += 2;
                                continue;
                            }
                        }
                        break;
                }

                if (readingWord)
                {
                    currentWord.Append( ('_' == currentChar) ? ' ' : currentChar );
                    i++;
                }
                else
                {
                    currentSurroundingText.Append(currentChar);
                    i++;
                }
            }

            sb.Append(currentSurroundingText.ToString());

            return sb.ToString();
        }

        private static string ParseEncodedWord(string input)
        {
            StringBuilder sb = new StringBuilder();

            if (!input.StartsWith("=?"))
                return input;

            if (!input.EndsWith("?="))
                return input;

            // Get the name of the encoding but skip the leading =?
            string encodingName = input.Substring(2, input.IndexOf("?", 2) - 2);
            Encoding enc = ASCIIEncoding.ASCII;
            if (!string.IsNullOrEmpty(encodingName))
            {
                enc = Encoding.GetEncoding(encodingName);
            }

            // Get the type of the encoding
            char type = input[encodingName.Length + 3];

            // Start after the name of the encoding and the other required parts
            int startPosition = encodingName.Length + 5;

            switch (char.ToLowerInvariant(type))
            {
                case 'q':
                    var parser = new QuotedPrintableParser(input, startPosition, true);
                    sb.Append(parser.GetParsedString(enc));
                    break;
                case 'b':
                    string baseString = input.Substring(startPosition, input.Length - startPosition - 2);
                    byte[] baseDecoded = Convert.FromBase64String(baseString);
                    var intermediate = enc.GetString(baseDecoded);
                    sb.Append(intermediate);
                    break;
            }
            return sb.ToString();
        }
    }
}
