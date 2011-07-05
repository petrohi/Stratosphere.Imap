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

            Int32 i = 0;
            while (i < input.Length)
            {
                char currentChar = input[i];
                char peekAhead;
                switch (currentChar)
                {
                    case '=':
                        peekAhead = (i == input.Length - 1) ? ' ' : input[i + 1];

                        if (peekAhead == '?')
                        {
                            if (!hasSeenAtLeastOneWord
                                || (hasSeenAtLeastOneWord && currentSurroundingText.ToString().Trim().Length > 0) )
                            {
                                sb.Append(currentSurroundingText.ToString());
                            }

                            currentSurroundingText = new StringBuilder();
                            hasSeenAtLeastOneWord = true;
                            readingWord = true;
                        }
                        break;

                    case '?':
                        if (readingWord)
                        {
                            peekAhead = (i == input.Length - 1) ? ' ' : input[i + 1];

                            if (peekAhead == '=')
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
                    currentWord.Append(currentChar);
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
            Encoding enc = Encoding.GetEncoding(encodingName);

            // Get the type of the encoding
            char type = input[encodingName.Length + 3];

            // Start after the name of the encoding and the other required parts
            int startPosition = encodingName.Length + 5;

            switch (char.ToLowerInvariant(type))
            {
                case 'q':
                    sb.Append(ParseQuotedPrintable(enc, input, startPosition, true));
                    break;
                case 'b':
                    string baseString = input.Substring(startPosition, input.Length - startPosition - 2);
                    byte[] baseDecoded = Convert.FromBase64String(baseString);
                    sb.Append(enc.GetString(baseDecoded));
                    break;
            }
            return sb.ToString();
        }

        public static string ParseQuotedPrintable(Encoding enc, string input)
        {
            return ParseQuotedPrintable(enc, input, 0, false);
        }

        public static string ParseQuotedPrintable(Encoding enc, string input, int startPos, bool skipQuestionEquals)
        {
            StringBuilder sb = new StringBuilder(input.Length);

            int i = startPos;

            while (i < input.Length)
            {
                char currentChar = input[i];
                char[] peekAhead = new char[2];
                switch (currentChar)
                {
                    case '=':
                        peekAhead = (i >= input.Length - 2) ? null : new char[] { input[i + 1], input[i + 2] };

                        if (peekAhead == null)
                        {
                            sb.Append(currentChar);
                            i++;
                            break;
                        }

                        try
                        {
                            string decodedChar = enc.GetString(new byte[] { Convert.ToByte(new string(peekAhead, 0, 2), 16) });
                            sb.Append(decodedChar);
                            i += 3;
                        }
                        catch (Exception)
                        {
                            // could not parse the peek-ahead chars as a hex number... so gobble the un-encoded '='
                            i += 1;
                        }
                        break;
                    case '?':
                        if (input[i + 1] == '=')
                        {
                            if (skipQuestionEquals)
                            {
                                i += 2;
                            }
                            else
                            {
                                sb.Append('?');
                                i++;
                            }
                        }
                        break;
                    case '_':
                        sb.Append(' ');
                        i++;
                        break;
                    default:
                        sb.Append(currentChar);
                        i++;
                        break;
                }
            }

            return sb.ToString();
        }
    }
}
