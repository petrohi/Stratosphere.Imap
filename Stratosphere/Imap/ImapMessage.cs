// Copyright (c) 2009 7Clouds

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Net.Mail;
using System.Globalization;
using System.IO;
using System.Text;

namespace Stratosphere.Imap
{
    public sealed class ImapMessage
    {
        public int Number { private set; get; }
        public DateTime Timestamp { private set; get; }
        public string Subject { private set; get; }
        public MailAddress Sender { private set; get; }
        public MailAddress From { private set; get; }
        public MailAddress ReplyTo { private set; get; }
        public IEnumerable<MailAddress> To { private set; get; }
        public IEnumerable<MailAddress> Cc { private set; get; }
        public IEnumerable<MailAddress> Bcc { private set; get; }
        public string ID { private set; get; }
        public IEnumerable<ImapBodyPart> BodyParts { private set; get; }

        internal ImapMessage(ImapList list)
        {
            int number;

            if (int.TryParse(list.GetStringAt(1), out number))
            {
                Number = number;
            }

            ImapList fetchList = list.GetListAt(3);

            int bodyIndex = fetchList.IndexOfString("BODYSTRUCTURE") + 1;
            int envelopeIndex = fetchList.IndexOfString("ENVELOPE") + 1;

            if (envelopeIndex != 0)
            {
                ImapList envelopeList = fetchList.GetListAt(envelopeIndex);

                string timestampString = envelopeList.GetStringAt(0);
                DateTime timestamp;

                if (TryParseTimestamp(timestampString, out timestamp))
                {
                    Timestamp = timestamp;
                }

                Subject = envelopeList.GetStringAt(1);
                Sender = ParseAddresses(envelopeList.GetListAt(2)).FirstOrDefault();
                From = ParseAddresses(envelopeList.GetListAt(3)).FirstOrDefault();
                ReplyTo = ParseAddresses(envelopeList.GetListAt(4)).FirstOrDefault();
                To = ParseAddresses(envelopeList.GetListAt(5)).ToArray();
                Cc = ParseAddresses(envelopeList.GetListAt(6)).ToArray();
                Bcc = ParseAddresses(envelopeList.GetListAt(7)).ToArray();
                ID = envelopeList.GetStringAt(8);
            }

            if (bodyIndex != 0)
            {
                ImapList bodyList = fetchList.GetListAt(bodyIndex);

                if (bodyList.Count != 0)
                {
                    BodyParts = ParseBodyParts(string.Empty, bodyList).ToArray();
                }
            }
        }

        private static IEnumerable<ImapBodyPart> ParseBodyParts(string section, ImapList bodyList)
        {
            if (bodyList.IsStringAt(0))
            {
                yield return new ImapBodyPart(string.IsNullOrEmpty(section) ? "1" : section, bodyList);
            }
            else
            {
                string innerSectionPrefix = string.IsNullOrEmpty(section) ? string.Empty : section + ".";

                string mutipartType = bodyList.GetStringAt(bodyList.Count - 4);

                if (!string.IsNullOrEmpty(mutipartType))
                {
                    for (int i = 0; i < bodyList.Count - 4; i++)
                    {
                        string innerSection = innerSectionPrefix + (i + 1).ToString();
                        ImapList innerBodyList = bodyList.GetListAt(i);

                        if (innerBodyList.Count != 0)
                        {
                            foreach (ImapBodyPart part in ParseBodyParts(innerSection, innerBodyList))
                            {
                                yield return part;
                            }
                        }
                    }
                }
            }
        }

        private static IEnumerable<MailAddress> ParseAddresses(ImapList list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                ImapList addressList = list.GetListAt(i);
				
				string displayName = addressList.GetStringAt(0);
				string user = addressList.GetStringAt(2);
				string host = addressList.GetStringAt(3);
				
				if (!string.IsNullOrEmpty(user) &&
				    !string.IsNullOrEmpty(host))
                {
	                string addressString = string.Format("{0}@{1}",
	                        user, host);
	
					MailAddress address = null;
					
					try
					{
		                if (string.IsNullOrEmpty(displayName))
		                {
		                    address = new MailAddress(addressString);
		                }
		                else
		                {
		                    address = new MailAddress(addressString, displayName);
		                }
					}
					catch (FormatException) { }
					
					if (address != null)
					{
						yield return address;
					}
				}
            }
        }

        private static bool TryParseTimestamp(string s, out DateTime timestamp)
        {
            if (!string.IsNullOrEmpty(s))
            {
                string[] parts = s.Split(' ');
                string lastPart = parts[parts.Length - 1];

                if (lastPart.StartsWith("("))
                {
                    StringBuilder b = new StringBuilder();

                    for (int i = 0; i < parts.Length - 1; ++i)
                    {
                        if (b.Length != 0)
                        {
                            b.Append(' ');
                        }

                        b.Append(parts[i]);
                    }

                    s = b.ToString();
                }
                else
                {
                    string tz;

                    if (__timezoneAbbreaviations.TryGetValue(lastPart, out tz))
                    {
                        StringBuilder b = new StringBuilder();

                        for (int i = 0; i < parts.Length - 1; ++i)
                        {
                            b.Append(parts[i]);
                            b.Append(' ');
                        }

                        b.Append(tz);
                        s = b.ToString();
                    }
                }

                return DateTime.TryParse(s, null, DateTimeStyles.AdjustToUniversal, out timestamp);
            }

            timestamp = DateTime.MinValue;
            return false;
        }

        static ImapMessage()
        {
            using (StreamReader reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("Stratosphere.Imap.TZ.txt")))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    string[] abbs = line.Split(' ');

                    for (int i = 1; i < abbs.Length; ++i)
                    {
                        __timezoneAbbreaviations.Add(abbs[i], abbs[0]);
                    }
                }
            }
        }

        private static Dictionary<string, string> __timezoneAbbreaviations = new Dictionary<string, string>();
    }
}