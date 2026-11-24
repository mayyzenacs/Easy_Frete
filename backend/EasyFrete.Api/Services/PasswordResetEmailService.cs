using System.Net;
using EasyFrete.Api.Configuration;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace EasyFrete.Api.Services;

public interface IPasswordResetEmailSender
{
    bool IsConfigured { get; }
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetUrl,
        int expiresInMinutes,
        CancellationToken ct);
}

public sealed class MailKitPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly ApiSettings _settings;

    public MailKitPasswordResetEmailSender(ApiSettings settings)
    {
        _settings = settings;
    }

    public bool IsConfigured => _settings.IsSmtpConfigured;

    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string toName,
        string resetUrl,
        int expiresInMinutes,
        CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SMTP não configurado nas variáveis de ambiente.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.SmtpFromName, _settings.SmtpFromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = "Redefinição de senha — Easy Frete";

        var safeName = WebUtility.HtmlEncode(toName);
        var safeUrl = WebUtility.HtmlEncode(resetUrl);

        var bodyBuilder = new BodyBuilder
        {
            TextBody =
                $"Olá, {toName}!\n\n" +
                $"Recebemos uma solicitação para redefinir a senha da sua conta no Easy Frete.\n" +
                $"Acesse o link abaixo para criar uma nova senha (válido por {expiresInMinutes} minutos e de uso único):\n\n" +
                $"{resetUrl}\n\n" +
                $"Se você não fez esta solicitação, ignore este e-mail. Sua senha permanecerá inalterada.",
            HtmlBody =
                $"""
                <!DOCTYPE html>
                <html lang="pt-BR">
                <body style="margin:0;padding:24px;background:#09090b;color:#f4f4f5;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;">
                  <div style="max-width:480px;margin:0 auto;background:#18181b;border:1px solid #27272a;border-radius:16px;padding:28px;">
                    <h1 style="margin:0 0 8px;font-size:20px;font-weight:900;color:#facc15;letter-spacing:-0.02em;">EASY FRETE</h1>
                    <h2 style="margin:0 0 12px;font-size:16px;font-weight:700;color:#f4f4f5;">Redefinição de senha</h2>
                    <p style="margin:0 0 16px;font-size:14px;line-height:1.5;color:#d4d4d8;">
                      Olá, <strong>{safeName}</strong>! Recebemos uma solicitação para redefinir a senha da sua conta.
                    </p>
                    <p style="margin:0 0 22px;">
                      <a href="{safeUrl}" style="display:inline-block;padding:12px 20px;background:#facc15;color:#09090b;text-decoration:none;border-radius:10px;font-weight:800;font-size:14px;">
                        Criar nova senha
                      </a>
                    </p>
                    <p style="margin:0 0 10px;font-size:12px;color:#a1a1aa;">
                      Este link é de uso único e expira em <strong>{expiresInMinutes} minutos</strong>.
                    </p>
                    <p style="margin:0;font-size:12px;color:#71717a;">
                      Se você não solicitou a redefinição de senha, ignore esta mensagem.
                    </p>
                  </div>
                </body>
                </html>
                """
        };

        message.Body = bodyBuilder.ToMessageBody();

        var socketOption = _settings.SmtpUseSsl switch
        {
            true => SecureSocketOptions.SslOnConnect,
            false => SecureSocketOptions.StartTls,
            null => _settings.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls
        };

        using var client = new SmtpClient();
        client.Timeout = 15_000;
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOption, ct);

        if (!string.IsNullOrWhiteSpace(_settings.SmtpUser))
        {
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
