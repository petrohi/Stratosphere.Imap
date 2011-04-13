// Copyright (c) 2009 7Clouds

using System.Collections.Generic;

namespace Stratosphere.Imap
{
    public sealed class ImapFolder
    {
        internal ImapFolder(IEnumerable<string> lines)
        {
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

                    var uidNextPos = list.IndexOfString("[UIDNEXT");

                    if (uidNextPos >= 0)
                    {
                        long uidNext = long.MaxValue;
                        if (long.TryParse(list.GetStringAt(uidNextPos+1).TrimEnd(']'), out uidNext))
                        {
                            UidNext = uidNext;
                        }
                    }
                }
            }
        }

        public int ExistsCount { private set; get; }
        public int RecentCount { private set; get; }
        public long UidNext { private set; get; }
    }
}