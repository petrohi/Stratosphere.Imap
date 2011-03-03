// Copyright (c) 2009 7Clouds

using System;
using System.Collections.Generic;
using System.Text;

namespace Stratosphere.Imap
{
    internal sealed class ImapList
    {
        private readonly List<object> _list = new List<object>();

        private ImapList() { }

        private ImapList(IEnumerator<char> chars)
        {
            StringBuilder builder = new StringBuilder();
            bool isInQuotes = false;

            while (chars.MoveNext())
            {
                if (chars.Current == '\"')
                {
                    isInQuotes = !isInQuotes;
                }
                else if (chars.Current == ' ' && !isInQuotes)
                {
                    if (builder.Length > 0)
                    {
                        AddString(builder.ToString());
                        builder = new StringBuilder();
                    }
                }
                else if (chars.Current == '(' && !isInQuotes)
                {
                    _list.Add(new ImapList(chars));
                }
                else if (chars.Current == ')' && !isInQuotes)
                {
                    break;
                }
                else
                {
                    builder.Append(chars.Current);
                }
            }

            if (builder.Length > 0)
            {
                AddString(builder.ToString());
            }
        }

        private void AddString(string s)
        {
            if (s == "NIL")
            {
                _list.Add(null);
            }
            else
            {
                bool isEncoded = false;

                if (s.StartsWith("=?"))
                {
                    string[] segments = s.Split('?');

                    if (segments.Length == 5 &&
                        segments[0] == "=" &&
                        segments[2] == "B" && 
                        segments[4] == "=")
                    {
                        try
                        {
                            _list.Add(Encoding.GetEncoding(segments[1]).GetString(Convert.FromBase64String(segments[3])));
                            
                            isEncoded = true;
                        }
                        catch (ArgumentException) { }
                        catch (FormatException) { }
                    }
                }

                if (!isEncoded)
                {
                    _list.Add(s);
                }
            }
        }

        public int Count { get { return _list.Count; } }

        public int IndexOfString(string s)
        {
            return _list.IndexOf(s);
        }

        public bool IsStringAt(int i)
        {
            if (i < Count)
            {
                return _list[i] is string;
            }

            return false;
        }

        public bool IsListAt(int i)
        {
            if (i < Count)
            {
                return _list[i] is ImapList;
            }

            return false;
        }

        public string GetStringAt(int i)
        {
            if (IsStringAt(i))
            {
                return (string)_list[i];
            }

            return string.Empty;
        }

        public ImapList GetListAt(int i)
        {
            if (IsListAt(i))
            {
                return (ImapList)_list[i];
            }

            return Empty;
        }

        public static ImapList Parse(string content)
        {
            using (IEnumerator<char> chars = content.GetEnumerator())
            {
                return new ImapList(chars);
            }
        }

        public static readonly ImapList Empty = new ImapList();
    }
}