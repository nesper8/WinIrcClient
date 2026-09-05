namespace WinIrcClient.Models
{
    public sealed class ChannelInfo
    {
        public string Name { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public string UserCountLabel => $"{UserCount:N0} users";
    }
}