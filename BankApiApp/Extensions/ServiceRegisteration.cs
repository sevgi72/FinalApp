using BankApiApp.Data;
using BankApiApp.Data.Interceptors;
using BankApiApp.Models;
using BankApiApp.Models.Common;
using BankApiApp.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;


namespace BankApiApp.Extensions;

public static class ServiceRegistration
{
    // Program.cs-den cagirilan esas metod — her seyi qruplasdirir
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddIdentityConfiguration();
        services.AddJwtAuthentication(configuration);
        services.AddBackgroundJobs(configuration);
        services.AddAppDependencies();
        services.AddSwaggerDocumentation();
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));

        services.AddControllers();
        return services;
    }

    // Verilenler bazasi
    private static IServiceCollection AddPersistence(
    this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AuditInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));

        return services;
    }

    // Identity
    private static IServiceCollection AddIdentityConfiguration(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequiredLength = 6;
            options.Password.RequireNonAlphanumeric = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();
        return services;
    }

    // JWT autentifikasiya
    private static IServiceCollection AddJwtAuthentication(
    this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                // SignalR üçün: WebSocket header dəstəkləmir, ona görə token-i URL-dən oxu
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    // yalnız çat bağlantısında token-i query string-dən götür
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                        context.Token = accessToken;
                    if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/chatHub") ||
                    path.StartsWithSegments("/notificationHub")))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                },

                // parol dəyişəndə köhnə tokenlər dərhal etibarsız olsun
                OnTokenValidated = async context =>
                {
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    // 1. İstifadəçinin ID-sini tapırıq (NameIdentifier həm qısa, həm uzun XML şemasını dəstəkləyir)
                    var userId = context.Principal!.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? context.Principal!.FindFirst("sub")?.Value;

                    if (string.IsNullOrEmpty(userId))
                    {
                        context.Fail("Token daxilində istifadəçi ID-si tapılmadı.");
                        return;
                    }

                    // 2. İstifadəçini verilənlər bazasından birbaşa ID ilə çəkirik
                    var user = await userManager.FindByIdAsync(userId);
                    if (user == null)
                    {
                        context.Fail("İstifadəçi tapılmadı.");
                        return;
                    }

                    // 3. Token-dəki SecurityStamp-i həm qısa adla, həm də standard .NET şeması ilə axtarırıq
                    var tokenStamp = context.Principal!.FindFirst("SecurityStamp")?.Value
                                  ?? context.Principal!.FindFirst(ClaimTypes.SerialNumber)?.Value;

                    // Əgər token daxilində ştemp ümumiyyətlə yoxdursa və ya bazadakı ilə uyuşmursa
                    if (string.IsNullOrEmpty(tokenStamp) || tokenStamp != user.SecurityStamp)
                    {
                        context.Fail("Token etibarsızdır (Təhlükəsizlik ştampı uyuşmur).");
                        return;
                    }
                }
            };
        });

        return services;
    }

    // Hangfire (background job-lar)
    private static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHangfire(config =>
            config.UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(configuration.GetConnectionString("DefaultConnection"))));
        services.AddHangfireServer();
        return services;
    }

    // AutoMapper + oz servislerimiz
    private static IServiceCollection AddAppDependencies(this IServiceCollection services)
    {
        services.AddAutoMapper(cfg => { }, typeof(Program));
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<ICardService, CardService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<ILimitService, LimitService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUtilityService, UtilityService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ILocalizationService, LocalizationService>();
        services.AddScoped<ICleanupService, CleanupService>();
        services.AddScoped<IRoundUpService, RoundUpService>();
        services.AddScoped<IScheduledPaymentService, ScheduledPaymentService>();
        services.AddScoped<ICashByCodeService, CashByCodeService>();
        services.AddScoped<IPaymentTemplateService, PaymentTemplateService>();
        services.AddScoped<ICashbackService, CashbackService>();
        services.AddScoped<IDepositService, DepositService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ISavingsGoalService, SavingsGoalService>();
        services.AddScoped<IStatementService, StatementService>();
        services.AddScoped<IBalanceAlertService, BalanceAlertService>();
        return services;
    }

    // Swagger + JWT duymesi
    private static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "BankApiApp", Version = "v1" });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Token-i bura yapisdir (Bearer yazma, sadece token)"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }
}