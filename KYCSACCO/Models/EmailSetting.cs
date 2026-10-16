namespace KYCSACCO.Models
{
    public class EmailSetting
    {
        public string EmailAddress { get; set; } = string.Empty;
        public string EmailName { get; set; } = string.Empty;
        public string SendingHost { get; set; } = string.Empty;
        public int SendingPort { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? SendingEncryption { get; set; }   // "ssl", "tls", "starttls"
        public bool IsOAuth { get; set; }
        public string? AccessToken { get; set; }          // only when IsOAuth = true
    }
}