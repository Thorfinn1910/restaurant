using System;

namespace QuanLyNhaHang.Models
{
    public sealed class ChatMessage
    {
        public ChatMessage(string text, bool isFromUser, DateTime timestamp, string intentTag)
        {
            Text = text;
            IsFromUser = isFromUser;
            Timestamp = timestamp;
            IntentTag = intentTag;
        }

        public string Text { get; }
        public bool IsFromUser { get; }
        public DateTime Timestamp { get; }
        public string IntentTag { get; }
    }
}
