// Copyright (c) 2009 7Clouds

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;

namespace Stratosphere.Imap
{
    public sealed class ImapMessage
    {
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

                DateTime timestamp;

                if (DateTime.TryParse(envelopeList.GetStringAt(0), out timestamp))
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
                string address = string.Format("{0}@{1}",
                        addressList.GetStringAt(2), addressList.GetStringAt(3));

                if (string.IsNullOrEmpty(addressList.GetStringAt(0)))
                {
                    yield return new MailAddress(address);
                }
                else
                {
                    yield return new MailAddress(address, addressList.GetStringAt(0));
                }
            }
        }

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
    }
}