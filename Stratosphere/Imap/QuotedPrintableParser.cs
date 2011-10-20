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
using System.Linq;

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
                            ParseConsecutiveEncodedBytes(outputStream);
                            break;

                        case (byte)'?':
                            ParseQuestionMarkCharacter(outputStream);
                            break;

                        default:
                            CopyNextByteToOutput(outputStream);
                            break;
                    }
                }

                return outputStream.ToArray();
            }
        }

        private void ParseConsecutiveEncodedBytes(MemoryStream outputStream)
        {
            List<byte> consecutiveEncodedBytes = new List<byte>();

            bool isEncodedByte = true;

            do
            {
                byte nextByte;
                isEncodedByte = ParseNextEncodedByte(out nextByte);

                if (isEncodedByte)
                {
                    consecutiveEncodedBytes.Add(nextByte);
                }
                else
                {
                    // We've gathered the consecutive decoded bytes... apply any post-processing
                    // before copying to output.
                    var postProcessedEncodedBytes = PostProcessEncodedBytes(consecutiveEncodedBytes);
                    outputStream.Write(postProcessedEncodedBytes, 0, postProcessedEncodedBytes.Length);
                }
            }
            while (isEncodedByte);
        }

        private byte[] PostProcessEncodedBytes(IEnumerable<byte> consecutiveEncodedBytes)
        {
            // TODO:  Apply any post-processing necessary...
            return consecutiveEncodedBytes.ToArray();
        }

        private bool ParseNextEncodedByte(out byte nextByte)
        {
            bool isEncodedByte = true;

            bool canPeekAhead = (_inputPos < _inputBytes.Length - 2);

            if (!canPeekAhead || !IsCurrentCharAnEqualsSign())
            {
                nextByte = default(byte);
                isEncodedByte = false;
            }
            else
            {
                isEncodedByte = TryParseCandidateEncodedByte(out nextByte);
            }

            return isEncodedByte;
        }

        private bool IsCurrentCharAnEqualsSign()
        {
            return ('=' == (char)_inputBytes[_inputPos]);
        }

        private bool TryParseCandidateEncodedByte(out byte nextByte)
        {
            bool isEncodedByte = false;

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
                nextByte = default(byte);
                // If we have a lone equals followed by newline chars, then this is an artificial
                // line break that should be skipped past.
                _inputPos += 1 + skipNewLineCount;
            }
            else
            {
                isEncodedByte = TryParseEncodedByte(out nextByte);
            }

            return isEncodedByte;
        }

        private bool TryParseEncodedByte(out byte nextByte)
        {
            bool isEncodedByte = false;

            try
            {
                char[] peekAhead = new char[2];

                peekAhead[0] = (char)_inputBytes[_inputPos + 1];
                peekAhead[1] = (char)_inputBytes[_inputPos + 2];

                byte decodedByte = Convert.ToByte(new string(peekAhead, 0, 2), 16);
                nextByte = decodedByte;

                _inputPos += 3;

                isEncodedByte = true;
            }
            catch (Exception)
            {
                nextByte = default(byte);
                // could not parse the peek-ahead chars as a hex number... so gobble the un-encoded '='
                _inputPos += 1;
            }

            return isEncodedByte;
        }

        private void CopyNextByteToOutput(MemoryStream outputStream)
        {
            outputStream.WriteByte(_inputBytes[_inputPos]);
            ++_inputPos;
        }

        private void ParseQuestionMarkCharacter(MemoryStream outputStream)
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
