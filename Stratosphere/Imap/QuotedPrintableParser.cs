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
    internal class QuotedPrintableParser
    {
        private readonly byte[] _inputBytes;
        private readonly int _startPos;
        private readonly bool _skipQuestionEquals;
        private readonly byte[] _parsedBytes;

        private int _inputPos = 0;

        public QuotedPrintableParser(string input)
            : this(input, 0, false)
        { }

        public QuotedPrintableParser(string input, int startPos, bool skipQuestionEquals)
        {
            _inputBytes = ASCIIEncoding.ASCII.GetBytes(input);
            _startPos = startPos;
            _skipQuestionEquals = skipQuestionEquals;
            
            _parsedBytes = Parse();
        }

        public byte[] GetParsedBytes()
        {
            return _parsedBytes;
        }

        public string GetParsedString(Encoding contentEncoding)
        {
            string parsed = string.Empty;

            var bytes = GetParsedBytes();
            if (bytes.Length > 0)
            {
                parsed = contentEncoding.GetString(bytes);
            }

            return parsed;
        }

        private byte[] Parse()
        {
            using (var outputStream = new MemoryStream(_inputBytes.Length))
            {
                _inputPos = _startPos;

                while (_inputPos < _inputBytes.Length)
                {
                    byte currentByte = _inputBytes[_inputPos];
                    switch (currentByte)
                    {
                        case (byte)'=':
                            ParseQuotedPrintableEqualsCharacter(outputStream);
                            break;

                        case (byte)'?':
                            ParseQuotedPrintableQuestionMarkCharacter(outputStream);
                            break;

                        default:
                            ParseQuotedPrintableCopyNextByteToOutput(outputStream);
                            break;
                    }
                }

                return outputStream.ToArray();
            }
        }

        private void ParseQuotedPrintableEqualsCharacter(MemoryStream outputStream)
        {
            bool canPeekAhead = (_inputPos < _inputBytes.Length - 2);

            if (!canPeekAhead)
            {
                ParseQuotedPrintableCopyNextByteToOutput(outputStream);
            }
            else
            {
                ParseQuotedPrintableCandidateEncodedByte(outputStream);
            }
        }

        private void ParseQuotedPrintableCandidateEncodedByte(MemoryStream outputStream)
        {
            int skipNewLineCount = 0;
            for (int j = 0; j < 2; ++j)
            {
                char c = (char)_inputBytes[_inputPos + j + 1];
                if ('\r' == c || '\n' == c)
                {
                    ++skipNewLineCount;
                }
            }

            if (skipNewLineCount > 0)
            {
                // If we have a lone equals followed by newline chars, then this is an artificial
                // line break that should be skipped past.
                _inputPos += 1 + skipNewLineCount;
            }
            else
            {
                ParseQuotedPrintableEncodedByte(outputStream);
            }
        }

        private void ParseQuotedPrintableEncodedByte(MemoryStream outputStream)
        {
            try
            {
                char[] peekAhead = new char[2];

                peekAhead[0] = (char)_inputBytes[_inputPos + 1];
                peekAhead[1] = (char)_inputBytes[_inputPos + 2];

                byte decodedByte = Convert.ToByte(new string(peekAhead, 0, 2), 16);
                outputStream.WriteByte(decodedByte);

                _inputPos += 3;
            }
            catch (Exception)
            {
                // could not parse the peek-ahead chars as a hex number... so gobble the un-encoded '='
                _inputPos += 1;
            }
        }

        private void ParseQuotedPrintableCopyNextByteToOutput(MemoryStream outputStream)
        {
            outputStream.WriteByte(_inputBytes[_inputPos]);
            ++_inputPos;
        }

        private void ParseQuotedPrintableQuestionMarkCharacter(MemoryStream outputStream)
        {
            if (_skipQuestionEquals && _inputBytes[_inputPos + 1] == (byte)'=')
            {
                _inputPos += 2;
            }
            else
            {
                outputStream.WriteByte(_inputBytes[_inputPos]);
                ++_inputPos;
            }
        }
    }
}
