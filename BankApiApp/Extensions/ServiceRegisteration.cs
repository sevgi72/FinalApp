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
        var jwtKey = configuration["Jwt:Key"]!;
        var jwtIssuer = configuration["Jwt:Issuer"];
        var jwtAudience = configuration["Jwt:Audience"];

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
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero   // token vaxtı bitəndə dərhal etibarsız olsun
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

                    return Task.CompletedTask;
                },

                // parol dəyişəndə köhnə tokenlər dərhal etibarsız olsun (SecurityStamp yoxlaması)
                OnTokenValidated = async context =>
                {
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    var user = await userManager.GetUserAsync(context.Principal!);
                    if (user == null)
                    {
                        context.Fail("İstifadəçi tapılmadı.");
                        return;
                    }

                    var tokenStamp = context.Principal!.FindFirst("SecurityStamp")?.Value;
                    if (tokenStamp != user.SecurityStamp)
                        context.Fail("Token etibarsızdır.");
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