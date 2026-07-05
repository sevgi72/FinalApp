$servicesDir = "BankApiApp\Services"
$files = Get-ChildItem -Path $servicesDir -Filter "*.cs"

$translations = @{
    "Hesab tapilmadi." = "AccountNotFound"
    "SuperAdmin rolu idare oluna bilmez." = "SuperAdminRoleCannotBeManaged"
    "Istifadeci tapilmadi." = "UserNotFound"
    "İstifadəçi tapılmadı." = "UserNotFound"
    "Istifadeci artiq bu roldadir." = "UserAlreadyInRole"
    "Öz hesabınızı bloklaya bilməzsiniz." = "CannotBlockOwnAccount"
    "Admin hesablarını yalnız SuperAdmin idarə edə bilər." = "AdminAccountsSuperAdminOnly"
    "SuperAdmin bloklana bilməz." = "SuperAdminCannotBeBlocked"
    "Bu email artiq qeydiyyatdan kecib." = "EmailAlreadyRegistered"
    "Tesdiq kodu yanlisdir." = "InvalidVerificationCode"
    "Tesdiq kodunun vaxti bitib. Yeniden kod teleb edin." = "VerificationCodeExpired"
    "Email ve ya parol yanlisdir." = "InvalidEmailOrPassword"
    "Hesabınız bloklanıb. Dəstəklə əlaqə saxlayın." = "AccountBlocked"
    "Email tesdiqlenmeyib. Zehmet olmasa evvelce emailinizi tesdiqleyin." = "EmailNotVerified"
    "Kod yanlisdir ve ya vaxti bitib." = "InvalidOrExpiredCode"
    "Google token etibarsizdir." = "InvalidGoogleToken"
    "Refresh token etibarsizdir ve ya vaxti bitib." = "InvalidOrExpiredRefreshToken"
    "Bu hesab size aid deyil." = "AccountNotYours"
    "Kart tapilmadi." = "CardNotFound"
    "Bu kart size aid deyil." = "CardNotYours"
    "Limit menfi ola bilmez." = "LimitCannotBeNegative"
    "Kredit meblegi 0-dan boyuk olmalidir." = "AmountMustBePositive"
    "Muddet 0-dan boyuk olmalidir." = "DurationMustBePositive"
    "Kredit tapilmadi." = "LoanNotFound"
    "Yalniz gozleyen kredit tesdiqlene biler." = "OnlyPendingLoanCanBeApproved"
    "Yalniz gozleyen kredit redd edile biler." = "OnlyPendingLoanCanBeRejected"
    "Mebleg sifirdan boyuk olmalidir." = "AmountMustBePositive"
    "Yuvarlaqlasdirma yalniz 1 ve ya 5 AZN ola biler." = "RoundingMustBe1Or5"
    "Menbe ve yigim hesabi eyni ola bilmez." = "SourceAndSavingsAccountCannotBeSame"
    "Menbe hesab tapilmadi." = "SourceAccountNotFound"
    "Yigim hesabi tapilmadi." = "SavingsAccountNotFound"
    "Round-up ayari tapilmadi." = "RoundUpSettingNotFound"
    "Provayder tapilmadi." = "ProviderNotFound"
    "Provayder tapılmadı." = "ProviderNotFound"
    "Hesab aktiv deyil." = "AccountNotActive"
    "Balans kifayet etmir." = "InsufficientBalance"
    "Odenis tapilmadi." = "PaymentNotFound"
    "Bu odenis size aid deyil." = "PaymentNotYours"
}

foreach ($file in $files) {
    if ($file.Name -match "TransactionService|LocalizationService") { continue }
    
    $content = [System.IO.File]::ReadAllText($file.FullName)
    $changed = $false
    
    $regex = 'throw new (BadRequest|NotFound)Exception\("([^"]+)"\)'
    $matches = [regex]::Matches($content, $regex)
    
    if ($matches.Count -gt 0) {
        $replacedContent = [regex]::Replace($content, $regex, {
            param($m)
            $exType = $m.Groups[1].Value
            $msg = $m.Groups[2].Value
            
            if ($msg.StartsWith("'{role}'")) {
                return "throw new ${exType}Exception(_localizer.Get(`"RoleNotFound`"))"
            }
            if ($msg.StartsWith("string.Join")) {
                return $m.Value
            }
            
            $key = ""
            if ($translations.ContainsKey($msg)) {
                $key = $translations[$msg]
            } else {
                $key = $msg -replace "[^a-zA-Z0-9]", ""
                if ([string]::IsNullOrWhiteSpace($key)) { $key = "UnknownError" }
            }
            
            return "throw new ${exType}Exception(_localizer.Get(`"$key`"))"
        })
        
        if ($content -cne $replacedContent) {
            $changed = $true
            $content = $replacedContent
        }
    }
    
    if ($changed) {
        if (-not $content.Contains("ILocalizationService _localizer;")) {
            # Find class name
            $classRegex = 'public class (\w+)(?:.*?)[\r\n\s]*\{'
            $classMatch = [regex]::Match($content, $classRegex)
            
            if ($classMatch.Success) {
                $className = $classMatch.Groups[1].Value
                $insertPos1 = $classMatch.Index + $classMatch.Length
                $content = $content.Insert($insertPos1, "`n    private readonly ILocalizationService _localizer;")
                
                # Find constructor
                $ctorRegex = "public $className\((.*?)\)[\r\n\s]*\{"
                $ctorMatch = [regex]::Match($content, $ctorRegex)
                
                if ($ctorMatch.Success) {
                    $args = $ctorMatch.Groups[1].Value
                    $newArgs = "ILocalizationService localizer"
                    if (-not [string]::IsNullOrWhiteSpace($args)) {
                        $newArgs = $args + ", ILocalizationService localizer"
                    }
                    
                    $content = $content.Remove($ctorMatch.Groups[1].Index, $ctorMatch.Groups[1].Length).Insert($ctorMatch.Groups[1].Index, $newArgs)
                    
                    # Update $ctorMatch index because content length changed
                    $ctorMatch2 = [regex]::Match($content, $ctorRegex)
                    $insertPos2 = $ctorMatch2.Index + $ctorMatch2.Length
                    $content = $content.Insert($insertPos2, "`n        _localizer = localizer;")
                }
            }
        }
        
        $utf8 = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($file.FullName, $content, $utf8)
        Write-Host "Updated $($file.Name)"
    }
}