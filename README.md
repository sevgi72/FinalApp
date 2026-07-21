# 🏦 Amorabank — Rəqəmsal Bank API

Amorabank tam funksional rəqəmsal bankçılıq platformasıdır. Backend .NET 8 üzərində Clean Architecture prinsipləri ilə qurulub; frontend isə Tailwind CSS və vanilla JavaScript ilə hazırlanmış çoxdilli (AZ / EN / RU) responsiv interfeysdir.

> Code Academy backend proqramının diplom layihəsi.

---

## 📋 Mündəricat

- [Əsas imkanlar](#-əsas-imkanlar)
- [Texnologiyalar](#-texnologiyalar)
- [Memarlıq](#-memarlıq)
- [Quraşdırma](#-quraşdırma)
- [Konfiqurasiya](#-konfiqurasiya)
- [API bölmələri](#-api-bölmələri)
- [Fon işləri (Hangfire)](#-fon-işləri-hangfire)
- [Təhlükəsizlik](#-təhlükəsizlik)
- [Frontend](#-frontend)
- [Testlər](#-testlər)

---

## ✨ Əsas imkanlar

### Bankçılıq
- **Hesablar** — çoxvalyutalı hesablar (AZN, USD, EUR, TRY), balans idarəsi
- **Köçürmələr** — IBAN ilə, kart ilə, öz hesablar arası köçürmə
- **Kartlar** — kart yaratma və idarəetmə
- **Əmanətlər** — faizli əmanət (illik dərəcə hər əmanətdə "dondurulur"), aylıq avtomatik faiz
- **Kreditlər** — kredit müraciəti, admin təsdiqi
- **Kommunal ödənişlər** — provayder seçimi, borc sorğusu, ödəniş

### Fərqləndirici funksiyalar
- **Kodla nağdlaşdırma (CashByCode)** — kartsız pul göndərmə; 8 rəqəmli kod, 72 saat keçərlilik, avtomatik geri qaytarma
- **Round-up yığım** — hər alışın qəpiyini avtomatik yığım hesabına atır
- **Keşbek** — kateqoriyaya görə bonus (kommunal 5%, nağd çıxarış 1%)
- **Yığım məqsədləri** — hədəf məbləğə doğru yığım
- **Planlaşdırılmış ödənişlər** — təkrarlanan avtomatik ödənişlər
- **Balans xəbərdarlığı** — aşağı balans bildirişi

### İstifadəçi
- **2FA (İki mərhələli doğrulama)** — email üzərindən OTP
- **Google ilə giriş** — Google Identity Services
- **Çoxdillilik** — AZ / EN / RU
- **PDF qəbz** — əməliyyat və ödəniş qəbzləri (QuestPDF)
- **AI köməkçi** — söhbət interfeysi

### Admin (Back-office)
- İstifadəçi idarəetməsi (bloklama, rol təyini)
- Kredit sorğularının təsdiqi/rəddi
- Dəstək söhbətləri (real-time)
- Toplu bildiriş (broadcast)
- Audit jurnalı (SuperAdmin)
- Sistem tənzimləri (SuperAdmin)

---

## 🛠 Texnologiyalar

### Backend
| Texnologiya | Təyinat |
|---|---|
| **.NET 8** | Əsas framework |
| **ASP.NET Core Web API** | REST API |
| **Entity Framework Core** | ORM |
| **PostgreSQL** | Verilənlər bazası |
| **ASP.NET Identity** | Autentifikasiya və istifadəçi idarəsi |
| **JWT** | Token əsaslı autentifikasiya |
| **Hangfire** | Fon işləri (planlaşdırılmış tapşırıqlar) |
| **SignalR** | Real-time əlaqə (dəstək söhbəti, bildiriş) |
| **AutoMapper** | Obyekt çevirmə |
| **QuestPDF** | PDF sənəd yaratma |
| **Rate Limiting** | Sorğu məhdudlaşdırma |

### Frontend
| Texnologiya | Təyinat |
|---|---|
| **HTML5 + Vanilla JS** | Struktur və məntiq |
| **Tailwind CSS** | Üslub (utility-first) |
| **Lucide Icons** | İkonlar |
| **Google Identity Services** | Google login |

---

## 🏗 Memarlıq

Layihə **Clean Architecture** prinsipləri ilə qurulub — laylar bir-birindən ayrıdır:

```
BankApiApp/
├── Controllers/       → HTTP sorğularını qəbul edir (nazik lay)
├── Services/          → Biznes məntiqi (əsas iş burada)
│   └── Interfaces/    → Servis müqavilələri (I-prefiksli)
├── Models/            → Verilənlər bazası entity-ləri
│   └── Common/        → AuditableEntity (Id, CreatedAt, UpdatedAt)
├── DTOs/              → Data Transfer Objects (API giriş/çıxış)
├── Data/              → AppDbContext (EF Core konfiqurasiyası)
├── Extensions/        → Köməkçi genişləndirmələr (pagination və s.)
├── Exceptions/        → Xüsusi exception tipləri
└── Migrations/        → EF Core migration-ları
```

### Əsas prinsiplər
- **Dependency Injection** — bütün servislər Program.cs-də qeydiyyatdan keçir
- **Interface əsaslı** — hər servisin interfeysi var (test və dəyişdirmə asanlığı)
- **DTO ilə izolyasiya** — modellər birbaşa API-yə çıxmır, DTO-lar vasitəsilə
- **Transaction (atomicity)** — pul əməliyyatları "ya hamısı, ya heç biri" prinsipi ilə
- **Optimistik konkurentlik** — PostgreSQL `xmin` ilə balans korlanmasının qarşısı alınır

---

## 🚀 Quraşdırma

### Tələblər
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (14+)
- IDE: Visual Studio 2022 və ya VS Code

### Addımlar

**1. Reponu klonla**
```bash
git clone https://github.com/istifadeci/amorabank.git
cd amorabank/BankApiApp
```

**2. Paketləri bərpa et**
```bash
dotnet restore
```

**3. Verilənlər bazasını qur**

`appsettings.json`-da bağlantı sətrini öz PostgreSQL məlumatlarınla yenilə (aşağıda), sonra migration-ları tətbiq et:
```bash
dotnet ef database update
```

**4. Backend-i işə sal**
```bash
dotnet run
```
API `http://localhost:5150` ünvanında işə düşür.
Swagger: `http://localhost:5150/swagger`

**5. Frontend-i işə sal**

Frontend qovluğunu istənilən statik server ilə aç (məs. VS Code Live Server):
```
http://127.0.0.1:5500/amorabank-auth.html
```

---

## ⚙️ Konfiqurasiya

`appsettings.json` faylında aşağıdakılar tənzimlənir:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=BankApiDb;Username=postgres;Password=SİZİN_PAROL"
  },
  "Jwt": {
    "Key": "SİZİN_GİZLİ_AÇARINIZ",
    "Issuer": "Amorabank",
    "Audience": "AmorabankUsers"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "Username": "SİZİN_EMAIL",
    "Password": "SİZİN_APP_PAROL"
  },
  "Google": {
    "ClientId": "SİZİN_GOOGLE_CLIENT_ID"
  }
}
```

> ⚠️ **Təhlükəsizlik qeydi:** Real açarları (JWT key, email parolu, Google secret) heç vaxt repoya commit etməyin. `.gitignore`-a `appsettings.json` əlavə edin və `appsettings.Example.json` şablon kimi saxlayın. Həssas dəyərlər üçün **User Secrets** və ya **environment variable** istifadə edin.

---

## 📡 API bölmələri

Bütün endpointlər Swagger-də (`/swagger`) sənədləşdirilib. Əsas kontrollerlər:

| Bölmə | Təyinat |
|---|---|
| **Auth** | Qeydiyyat, giriş, email təsdiqi, 2FA, Google login, parol bərpası |
| **Account** | Hesab yaratma, siyahı, detal |
| **Transaction** | Köçürmə, çıxarış, mədaxil, qəbz (PDF) |
| **Card** | Kart idarəsi |
| **Deposit** | Əmanət açma/bağlama |
| **Loan** | Kredit müraciəti |
| **CashByCode** | Kodla nağdlaşdırma |
| **RoundUp** | Qəpik yığımı ayarları |
| **Cashback** | Keşbek balansı və köçürmə |
| **SavingsGoal** | Yığım məqsədləri |
| **ScheduledPayment** | Planlaşdırılmış ödənişlər |
| **Payment / Utility** | Kommunal ödənişlər |
| **Limit** | Hesab limitləri |
| **BalanceAlert** | Balans xəbərdarlığı |
| **Statement** | Hesab çıxarışı |
| **Analytics** | Xərc analitikası |
| **Currency** | Valyuta məzənnələri və çevirmə |
| **Notification** | Bildirişlər |
| **Chat** | Dəstək söhbəti (SignalR) |
| **Ai** | AI köməkçi |
| **Admin** | İstifadəçi/kredit idarəsi, statistika |
| **Audit** | Audit jurnalı (SuperAdmin) |

---

## ⏰ Fon işləri (Hangfire)

Bəzi əməliyyatlar istifadəçi hərəkətindən asılı olmadan avtomatik işləyir. Hangfire paneli: `http://localhost:5150/hangfire`

| İş | Cədvəl | Təyinat |
|---|---|---|
| `accrue-deposit-interest` | Aylıq | Aktiv əmanətlərə faiz hesablayır |
| `expire-cash-by-code` | Saatlıq | Vaxtı keçmiş kodları geri qaytarır |
| `process-loan-payments` | Günlük | Kredit ödənişlərini emal edir |
| `process-scheduled-payments` | Günlük | Planlaşdırılmış ödənişləri icra edir |
| `cleanup-unconfirmed-accounts` | Günlük | Təsdiqlənməmiş hesabları təmizləyir |

> Demo üçün istənilən işi Hangfire panelindən **"Trigger now"** ilə dərhal işə salmaq olar.

---

## 🔐 Təhlükəsizlik

- **JWT autentifikasiya** — hər sorğu token ilə qorunur
- **İki mərhələli doğrulama (2FA)** — email OTP
- **Sahiblik yoxlaması** — hər əməliyyatda resurs istifadəçiyə aid olduğu yoxlanır (`OwnerId == userId`)
- **Rol əsaslı icazələr** — Customer / Admin / SuperAdmin
- **Rate limiting** — sorğu sıxlığının məhdudlaşdırılması
- **Optimistik konkurentlik** — eyni anda balans dəyişməsinin qarşısı alınır (`xmin`)
- **Backend validasiya** — frontend yoxlamalarına güvənilmir, əsl yoxlama serverdə
- **Məlumat sızmasının qarşısı** — kod doğrulamada eyni xəta mesajı (mövcudluq gizlədilir)

---

## 🎨 Frontend

Tam responsiv (mobil / planşet / masaüstü), çoxdilli interfeys:

- **14 səhifə** — giriş, panel, köçürmə, kartlar, əmanət, kredit, kommunal, kodla nağd, yığım, çıxarış, analitika, ayarlar, admin
- **3 dil** — AZ / EN / RU (`i18n.js` ilə)
- **4 valyuta** — real-time çevirmə
- **Mobil naviqasiya** — alt-nav + "Daha çox" menyusu
- **Design tokenlər** — vahid rəng və şrift sistemi (Sora / Inter / JetBrains Mono)

---

## ✅ Testlər

Layihədə xUnit ilə yazılmış vahid testlər (unit tests) var — əsas servislərin biznes məntiqini əhatə edir (əmanət faizi, kodla köçürmə, limit, round-up və s.). Testlər Moq ilə servisləri izolyasiya edir.

```bash
dotnet test
```

---

## 👤 Müəllif

Code Academy — Backend proqramı, diplom layihəsi.

---

## 📄 Lisenziya

Bu layihə təhsil məqsədilə hazırlanıb.
