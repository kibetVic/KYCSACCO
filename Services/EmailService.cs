using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using KYCSACCO.Models;
using System.Net.Http;
using AuthenticationException = MailKit.Security.AuthenticationException;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace KYCSACCO.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmail(EmailSetting settings, string toEmail, string subject, string body);
        Task<bool> SendVerificationCodeAsync(string email, string username, string code);
        Task<bool> SendMemberVerificationCodeAsync(string email, string username, string code);
        Task<bool> SendLoanDisbursementVerificationCodeAsync(string email, string username, string code);
        Task<bool> SendEmailAsync(string to, string subject, string body);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml);
        Task<bool> SendOtpAsync(string email, string username, string otp, int expiryMinutes = 5);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // ============================================================
        // Build EmailSetting from appsettings.json
        // ============================================================
        private EmailSetting LoadSettingsFromConfig()
        {
            return new EmailSetting
            {
                EmailAddress = _configuration["EmailSettings:FromEmail"] ?? "amtechalerts@amtechreeds.com",
                EmailName = _configuration["EmailSettings:FromName"] ?? "Amtech Alerts",
                SendingHost = _configuration["EmailSettings:SmtpServer"] ?? "mail.amtechreeds.com",
                SendingPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "465"),
                UserName = _configuration["EmailSettings:Username"] ?? "",
                Password = _configuration["EmailSettings:Password"] ?? "",
                SendingEncryption = null,   // port-driven below; can override with "ssl"/"tls"
                IsOAuth = false,
                AccessToken = null
            };
        }

        // ============================================================
        // MAIN SEND METHOD — used by everything else
        // ============================================================
        public async Task<bool> SendEmail(EmailSetting settings, string toEmail, string subject, string body)
        {
            if (settings == null)
            {
                _logger.LogError("SendEmail: settings is null.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(settings.SendingHost) ||
                string.IsNullOrWhiteSpace(settings.UserName) ||
                string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("SendEmail: incomplete settings. Host={Host}, User={User}, To={To}",
                    settings.SendingHost, settings.UserName, toEmail);
                return false;
            }

            try
            {
                // 1. Build the message
                var message = new MimeMessage();

                message.From.Add(new MailboxAddress(
                    settings.EmailName ?? "System Support",
                    settings.EmailAddress));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                // Ensure Message-Id domain matches sender domain (helps Gmail spam scoring)
                var domain = settings.EmailAddress?.Split('@').LastOrDefault() ?? "localhost";
                message.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId(domain);

                // Send BOTH plain text and HTML — improves deliverability
                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = body,
                    TextBody = StripHtml(body)
                };
                message.Body = bodyBuilder.ToMessageBody();

                // 2. Create client
                using var client = new SmtpClient
                {
                    Timeout = 20000 // 20 seconds
                };

                // ⚠️ DO NOT disable certificate validation in production.
                // Only enable the block below if you have a self-signed cert on a known host
                // AND validate by thumbprint.
                //
                // client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                // client.CheckCertificateRevocation = false;

                // 3. Choose encryption based on port (more reliable than string matching)
                SecureSocketOptions socketOption = settings.SendingPort switch
                {
                    465 => SecureSocketOptions.SslOnConnect,   // implicit TLS
                    587 => SecureSocketOptions.StartTls,       // STARTTLS
                    _ => settings.SendingEncryption?.ToLower() switch
                    {
                        "ssl" => SecureSocketOptions.SslOnConnect,
                        "tls" => SecureSocketOptions.StartTls,
                        "starttls" => SecureSocketOptions.StartTls,
                        _ => SecureSocketOptions.Auto
                    }
                };

                _logger.LogInformation("Connecting to {Host}:{Port} using {Option}",
                    settings.SendingHost, settings.SendingPort, socketOption);

                // 4. Connect
                await client.ConnectAsync(settings.SendingHost, settings.SendingPort, socketOption);

                // 5. Authenticate
                if (settings.IsOAuth)
                {
                    var oauth2 = new SaslMechanismOAuth2(settings.UserName, settings.AccessToken);
                    await client.AuthenticateAsync(oauth2);
                }
                else
                {
                    // Remove XOAUTH2 so LOGIN/PLAIN is used
                    client.AuthenticationMechanisms.Remove("XOAUTH2");
                    await client.AuthenticateAsync(settings.UserName, settings.Password);
                }

                // 6. Send
                var response = await client.SendAsync(message);

                // 7. Disconnect
                await client.DisconnectAsync(true);

                _logger.LogInformation(
                    "Email accepted by server for {To}. SMTP response: {Response}",
                    toEmail, response);
                return true;
            }
            catch (AuthenticationException ex)
            {
                _logger.LogError(ex,
                    "SMTP authentication failed for {User} on {Host}:{Port}. Check username/password.",
                    settings.UserName, settings.SendingHost, settings.SendingPort);
                return false;
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError(ex,
                    "SMTP command error. StatusCode={StatusCode}, ErrorCode={ErrorCode}, Message={Message}",
                    ex.StatusCode, ex.ErrorCode, ex.Message);
                return false;
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError(ex,
                    "SMTP protocol error (TLS/SSL handshake likely failed) for {Host}:{Port}",
                    settings.SendingHost, settings.SendingPort);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error sending email to {To}", toEmail);
                return false;
            }
        }

        // ============================================================
        // Password reset verification code
        // ============================================================
        public Task<bool> SendVerificationCodeAsync(string email, string username, string code)
        {
            var subject = "Password Reset Verification Code";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Password Reset Request</h2>
                    <p>Dear {username},</p>
                    <p>You requested to reset your password. Please use the verification code below:</p>
                    <div style='background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                        {code}
                    </div>
                    <p>This code will expire in <strong>10 minutes</strong>.</p>
                    <p>If you did not request this, please ignore this email.</p>
                    <hr/>
                    <p style='font-size: 12px; color: #666;'>SACCO Blockchain System</p>
                </body>
                </html>";

            return SendEmail(LoadSettingsFromConfig(), email, subject, body);
        }

        // ============================================================
        // Member pin verification code
        // ============================================================
        public Task<bool> SendMemberVerificationCodeAsync(string email, string username, string code)
        {
            var subject = "Member PIN Verification Code";
            var body = $@"
                 <html>
                 <body style='font-family: Arial, sans-serif;'>
                     <h2>Member PIN Request</h2>
                     <p>Dear {username},</p>
                     <p>You requested a PIN verification. Please use the verification code below:</p>
                     <div style='background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                         {code}
                     </div>
                     <p>This code will expire in <strong>10 minutes</strong>.</p>
                     <p>If you did not request this, please ignore this email.</p>
                     <hr/>
                     <p style='font-size: 12px; color: #666;'>SACCO Blockchain System</p>
                 </body>
                 </html>";

            return SendEmail(LoadSettingsFromConfig(), email, subject, body);
        }



        // ============================================================
        // Loan Disbursement verification code
        // ============================================================
        public Task<bool> SendLoanDisbursementVerificationCodeAsync(string email, string username, string code)
        {
            var subject = "Loan Disbursement Verification Code";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Loan Disbursement Request</h2>
                    <p>Dear {username},</p>
                    <p>You requested a loan disbursement verification. Please use the verification code below:</p>
                    <div style='background-color: #f0f0f0; padding: 15px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                        {code}
                    </div>
                    <p>This code will expire in <strong>10 minutes</strong>.</p>
                    <p>If you did not request this, please ignore this email.</p>
                    <hr/>
                    <p style='font-size: 12px; color: #666;'>SACCO Blockchain System</p>
                </body>
                </html>";

            return SendEmail(LoadSettingsFromConfig(), email, subject, body);
        }



        // ============================================================
        // Generic email (HTML by default)
        // ============================================================
        public Task<bool> SendEmailAsync(string to, string subject, string body)
            => SendEmail(LoadSettingsFromConfig(), to, subject, body);

        // ============================================================
        // Generic email with explicit HTML/plain flag
        // ============================================================
        public Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml)
        {
            if (isHtml)
                return SendEmail(LoadSettingsFromConfig(), to, subject, body);

            var safe = System.Net.WebUtility.HtmlEncode(body).Replace("\n", "<br/>");
            var html = $"<pre style='font-family:Arial,sans-serif;'>{safe}</pre>";
            return SendEmail(LoadSettingsFromConfig(), to, subject, html);
        }

        // ============================================================
        // OTP for pending disbursement
        // ============================================================
        public Task<bool> SendOtpAsync(string email, string username, string otp, int expiryMinutes = 5)
        {
            var subject = "OTP for Pending Disbursement Access";

            var ipAddress = "Unknown";
            try
            {
                ipAddress = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                    .AddressList
                    .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.ToString() ?? "Unknown";
            }
            catch { /* non-fatal */ }

            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #1a237e; color: white; padding: 20px; text-align: center; }}
                        .content {{ padding: 20px; background-color: #f8f9fa; }}
                        .otp-box {{ background-color: #fff; padding: 20px; text-align: center; border-radius: 8px; border: 2px dashed #1a237e; margin: 20px 0; }}
                        .otp-code {{ font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #1a237e; }}
                        .footer {{ text-align: center; padding: 15px; font-size: 12px; color: #666; }}
                        .warning {{ color: #d32f2f; font-weight: bold; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h2>🔐 One-Time Password (OTP)</h2>
                        </div>
                        <div class='content'>
                            <p>Dear <strong>{username}</strong>,</p>
                            <p>You are attempting to access <strong>Pending Disbursement</strong> section.</p>
                            <p>Please use the One-Time Password below to complete your verification:</p>

                            <div class='otp-box'>
                                <div class='otp-code'>{otp}</div>
                            </div>

                            <p>This OTP is valid for <strong>{expiryMinutes} minutes</strong>.</p>
                            <p class='warning'>⚠️ This is a one-time use OTP. Do not share it with anyone.</p>

                            <hr/>
                            <p><strong>Security Information:</strong></p>
                            <ul>
                                <li>OTP expires: {DateTime.UtcNow.AddMinutes(expiryMinutes):yyyy-MM-dd HH:mm:ss} UTC</li>
                                <li>IP Address: {ipAddress}</li>
                                <li>Request Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</li>
                            </ul>

                            <p>If you did not request this OTP, please ignore this email or contact support immediately.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; {DateTime.UtcNow.Year} SACCO Blockchain System. All rights reserved.</p>
                            <p>This is an automated email, please do not reply.</p>
                        </div>
                    </div>
                </body>
                </html>";

            return SendEmail(LoadSettingsFromConfig(), email, subject, body);
        }

        // ============================================================
        // Helper: strip HTML tags for the plain-text alternative
        // ============================================================
        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            return System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
        }
    }
}