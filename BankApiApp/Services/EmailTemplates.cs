namespace BankApiApp.Services;

public static class EmailTemplates
{
    // umumi qabiq — butun email-ler eyni dizaynda olsun deye
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
    <p style='font-size:12px;color:#999;margin:0;'>© 2026 BankApiApp · Butun huquqlar qorunur</p>
  </div>
</div>";
    }

    // kod gosteren qutu (tesdiq, sifirlama, OTP — hamisi eyni stilde)
    private static string CodeBox(string code) => $@"
    <div style='background:#E1F5EE;border:1px solid #5DCAA5;border-radius:12px;padding:20px;text-align:center;margin:0 0 24px;'>
      <div style='font-size:13px;color:#0F6E56;margin-bottom:8px;'>Kod</div>
      <div style='font-size:32px;font-weight:700;letter-spacing:8px;color:#04342C;'>{code}</div>
    </div>";

    public static string EmailConfirmation(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, hesabinizi yaratdiginiz ucun tesekkur edirik. Qeydiyyati tamamlamaq ucun asagidaki tesdiq kodunu daxil edin.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 15 deqiqe erzinde etibarlidir. Eger bu qeydiyyati siz etmemisinizse, bu emaili nezere almayin.
    </p>";
        return Wrap("Email unvaninizi tesdiqleyin", body);
    }
    public static string PasswordReset(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, parolunuzu sifirlamaq ucun sorgu aldiq. Asagidaki kodu daxil ederek yeni parol teyin ede bilersiniz.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 15 deqiqe erzinde etibarlidir. Eger parol sifirlama teleb etmemisinizse, bu emaili nezere almayin — hesabiniz tehlukesizdir.
    </p>";
        return Wrap("Parol sifirlama", body);
    }

    public static string TwoFactorCode(string fullName, string code)
    {
        var body = $@"
    <p style='font-size:15px;color:#555;line-height:1.7;margin:0 0 24px;'>
      Salam {fullName}, hesabiniza giris ucun tesdiq kodu. Bu kodu daxil ederek girisi tamamlayin.
    </p>
    {CodeBox(code)}
    <p style='font-size:13px;color:#999;line-height:1.7;margin:0;'>
      ⏱ Bu kod 5 deqiqe erzinde etibarlidir. Eger giris etmeye calismirsinizsa, derhal parolunuzu deyisin.
    </p>";
        return Wrap("Giris tesdiqi", body);
    }
}