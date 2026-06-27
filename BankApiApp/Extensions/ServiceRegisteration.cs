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
    // Program.cs-dən çağırılan əsas metod — hər şeyi qruplaşdırır
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

    // Verilənlər bazası
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
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
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

            // ← yeni: hər sorğuda SecurityStamp yoxlanışı
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var userManager = context.HttpContext.RequestServices
                        .GetRequiredService<UserManager<ApplicationUser>>();

                    var userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    var tokenStamp = context.Principal?.FindFirst("SecurityStamp")?.Value;

                    var user = userId == null ? null : await userManager.FindByIdAsync(userId);

                    // user yoxdursa, ya da stamp uyğun gəlmirsə (parol dəyişib) → token rədd
                    if (user == null || user.SecurityStamp != tokenStamp)
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

    // AutoMapper + öz servislərimiz
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
        return services;
    }

    // Swagger + JWT düyməsi
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
                Description = "Token-i bura yapışdır (Bearer yazma, sadəcə token)"
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