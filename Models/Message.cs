using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WinIrcClient.Models
{
    public enum MessageKind
    {
        Channel,
        Private,
        Server,
        Motd
    }

    public class Message
    {
        public int Id { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
        [NotMapped]
        public MessageKind Kind { get; set; } = MessageKind.Channel;
    }
}
