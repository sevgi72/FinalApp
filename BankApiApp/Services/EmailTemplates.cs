namespace BankApiApp.Services;

public static class EmailTemplates
{
    // ümumi qabıq — bütün email-lər eyni dizaynda olsun deyə
    private static string Wrap(string title, string bodyHtml)
    {
        return $@"
<div style='max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #e5e5e5;border-radius:12px;overflow:hidden;font-family:Arial,Helvetica,sans-serif;'>
  <div style='background:#0F6E56;padding:28px 32px;text-align:center;'>
    <div style='color:#E1F5EE;font-size:20px;font-weight:600;'>🏦 BankApiApp</div>
  </div>
  <div style='padding:32px;'>
    <h2 style='font-size:18px;margin:0 0 12px;color:#1a1a1a;'>{title}</h2>
    {bodyHtml}
  </div>
  <div style='border-top:1px solid #e5e5e5;padding:20px 32px;text-align:center;'>
    <p style='font-size:12px;color:#999;margin:0;'>© 2026 BankApiApp · Bütün hüquqlar qorunur</p>
  </div>
</div>";
    }

    // kod göstərən qutu (təsdiq, sıfırlama, OTP — hamısı eyni stildə)
    private static string CodeBox(string code) => $@"
    <div style='background:#E1F5EE;border:1px solid #5DCAA5;border-radius:12px;padding:20px;text-align:center;margin:0 0 24px;'>
      <div style='font-size:13px;color:#0F6E56;margin-bottom:8px;'>Kod</div>
      <div style='font-size:32px;font-weight:700;letter-spacing:8px;color:#04342C;'>{code}</div>
    </div>";

    public static string EmailConfirmation(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, hesabınızı yaratdığınız üçün təşəkkür edirik. Qeydiyyatı tamamlamaq üçün aşağıdakı təsdiq kodunu daxil edin.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 15 dəqiqə ərzində etibarlıdır. Əgər bu qeydiyyatı siz etməmisinizsə, bu emaili nəzərə almayın.
    </p>";
        return Wrap("Email ünvanınızı təsdiqləyin", body);
    }
    public static string PasswordReset(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, parolunuzu sıfırlamaq üçün sorğu aldıq. Aşağıdakı kodu daxil edərək yeni parol təyin edə bilərsiniz.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 15 dəqiqə ərzində etibarlıdır. Əgər parol sıfırlama tələb etməmisinizsə, bu emaili nəzərə almayın — hesabınız təhlükəsizdir.
    </p>";
        return Wrap("Parol sıfırlama", body);
    }

    public static string TwoFactorCode(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, hesabınıza giriş üçün təsdiq kodu. Bu kodu daxil edərək girişi tamamlayın.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 5 dəqiqə ərzində etibarlıdır. Əgər giriş etməyə çalışmırsınızsa, dərhal parolunuzu dəyişin.
    </p>";
        return Wrap("Giriş təsdiqi", body);
    }
}