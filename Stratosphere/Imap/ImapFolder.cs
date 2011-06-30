// Copyright (c) 2009 7Clouds

using System.Collections.Generic;

namespace Stratosphere.Imap
{
    public sealed class ImapFolder
    {
        internal ImapFolder(string name, IEnumerable<string> lines)
        {
            Name = name;

            UidNext = long.MaxValue;

            foreach (string line in lines)
            {
                ImapList list = ImapList.Parse(line);

                if (list.GetStringAt(0) == "*")
                {
                    if (list.Count == 3)
                    {
                        int count;

                        if (int.TryParse(list.GetStringAt(1), out count))
                        {
                            if (list.GetStringAt(2) == "EXISTS")
                            {
                                ExistsCount = count;
                            }
                            else if (list.GetStringAt(2) == "RECENT")
                            {
                                RecentCount = count;
                            }
                        }
                    }

                    long temp = 0;
                    if (TryGetBracketedLongValue(list, "[UIDNEXT", out temp))
                    {
                        UidNext = temp;
                    }

                    if (TryGetBracketedLongValue(list, "[UIDVALIDITY", out temp))
                    {
                        UidValidity = temp;
                    }

                }
                else
                {
                    IsReadOnly = (list.GetStringAt(2) == "[READ-ONLY]");
                }
            }
        }

        private bool TryGetBracketedLongValue(ImapList list, string name, out long retVal)
        {
            bool isOk = false;
            retVal = default(long);

            var pos = list.IndexOfString(name);
            if (pos >= 0)
            {
                isOk = long.TryParse(list.GetStringAt(pos + 1).TrimEnd(']'), out retVal);
            }

            return isOk;
        }

        public string Name { private set; get; }
        public bool IsReadOnly { private set; get; }
        public int ExistsCount { private set; get; }
        public int RecentCount { private set; get; }
        public long UidNext { private set; get; }
        public long UidValidity { private set; get; }
    }
}