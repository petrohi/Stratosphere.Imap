// Copyright (c) 2009 7Clouds

using System;
using System.Collections.Generic;
using System.Text;

namespace Stratosphere.Imap
{
    internal sealed class ImapList
    {
        public List<object> ToBasicTypesList()
        {
            List<object> theList = new List<object>();

            // NOTE:  For now, we'll do this recursively, but better approach is non-recursive.
            foreach (var item in _list)
            {
                if (item is string)
                {
                    theList.Add(item);
                }
                else if (item is ImapList)
                {
                    theList.Add((item as ImapList).ToBasicTypesList());
                }
                else
                {
                    throw new InvalidOperationException(
                        string.Format("Encountered (currently) unsupported list type [{0}].  Unable to transform to basic-types list",
                        item.GetType().Name));
                }
            }

            return theList;
        }

        private readonly List<object> _list = new List<object>();

        private ImapList() { }

        private ImapList(IEnumerator<char> chars)
        {
            StringBuilder aggregateBuilder = new StringBuilder();
            const char EscapeChar = '\\';
            const char QuoteChar = '\"';
            bool isInQuotes = false;
            bool isEscaped = false;

            while (chars.MoveNext())
            {
                if (!isEscaped && isInQuotes && chars.Current == EscapeChar)
                {
                    isEscaped = true;
                }
                else
                {
                    if (chars.Current == QuoteChar && !isEscaped)
                    {
                        isInQuotes = !isInQuotes;
                    }
                    else if (chars.Current == ' ' && !isInQuotes)
                    {
                        if (aggregateBuilder.Length > 0)
                        {
                            AddString(aggregateBuilder.ToString());
                            aggregateBuilder = new StringBuilder();
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
                        if (isEscaped && chars.Current != QuoteChar && chars.Current != EscapeChar)
                        {
                            // It wasn't escaping a quote or escape char, so add the escape char back
                            aggregateBuilder.Append(EscapeChar);
                        }

                        aggregateBuilder.Append(chars.Current);
                        isEscaped = false;
                    }
                }
            }

            if (aggregateBuilder.Length > 0)
            {
                AddString(aggregateBuilder.ToString());
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
                _list.Add(RFC2047Decoder.Parse(s));
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