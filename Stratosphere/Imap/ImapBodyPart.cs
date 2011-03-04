// Copyright (c) 2009 7Clouds

using System.Net.Mime;
using System.Text;

namespace Stratosphere.Imap
{
    public sealed class ImapBodyPart
    {
        internal ImapBodyPart(string section, ImapList list)
        {
            Section = section;

            StringBuilder builder = new StringBuilder(string.Format("{0}/{1}", list.GetStringAt(0).ToLowerInvariant(), list.GetStringAt(1).ToLowerInvariant()));
            ImapList paramsList = list.GetListAt(2);

            for (int i = 0; i < paramsList.Count; i += 2)
            {
                builder.AppendFormat(";{0}=\"{1}\"", paramsList.GetStringAt(i), paramsList.GetStringAt(i + 1));
            }
			
			try
			{
            	ContentType = new ContentType(builder.ToString());
			}
			catch
			{
				ContentType = new ContentType();
			}
			
            ID = list.GetStringAt(3);
            Description = list.GetStringAt(4);
            Encoding = list.GetStringAt(5);

            int size;

            if (int.TryParse(list.GetStringAt(6), out size))
            {
                Size = size;
            }
        }

        public string Section { private set; get; }
        public ContentType ContentType { private set; get; }
        public string ID { private set; get; }
        public string Description { private set; get; }
        public string Encoding { private set; get; }
        public int? Size { private set; get; }
    }
}