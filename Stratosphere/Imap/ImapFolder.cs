// Copyright (c) 2009 7Clouds

using System.Collections.Generic;

namespace Stratosphere.Imap
{
    public sealed class ImapFolder
    {
        internal ImapFolder(IEnumerable<string> lines)
        {
            foreach (string line in lines)
            {
                ImapList list = ImapList.Parse(line);

                if (list.Count == 3 && list.GetStringAt(0) == "*")
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
            }
        }

        public int ExistsCount { private set; get; }
        public int RecentCount { private set; get; }
    }
}