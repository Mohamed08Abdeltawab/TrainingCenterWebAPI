using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;
using TrainingCenter.Data;
using TrainingCenter.Entities;
using TrainingCenter.Interfaces;
using TrainingCenter.Repositories;

namespace TrainingCenter.Extensions
{
    public static class ServiceCollectionExtensions
    {
        // 1. Application Infrastructure Services (DbContext, Repositories, AutoMapper)
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers();

            // DbContext Registration
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            // Core Repositories & Unit of Work
            //addScpoed -> create one cope for http request
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // AutoMapper
            services.AddAutoMapper(cfg => cfg.AddProfile<Profiles.MappingProfile>());

            // Password Hasher Service
            //addScpoed -> create one cope for http request
            services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

            return services;
        }

        // 2. Authentication & JWT Setup
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtSettings = configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Key"] ?? "SUPER_SECRET_KEY_FOR_JWT_TRAINING_CENTER_API_2026";

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException("JWT Secret Key is not configured.");
            }

            //use bearer to detect indentity by using [Authorize]
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })//search for token kinda bearer
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtSettings["Issuer"] ?? "TrainingCenterAPI",
                    ValidAudience = jwtSettings["Audience"] ?? "TrainingCenterClients",

                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
                options.AddPolicy("InstructorOrAdmin", policy => policy.RequireRole("Admin", "Instructor"));
            });

            return services;
        }

        // 3. Security Services (CORS & Rate Limiter)
        public static IServiceCollection AddSecurityPolicies(this IServiceCollection services)
        {
            // CORS
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policy =>
                {
                    policy.WithOrigins("https://localhost:7123", "http://localhost:3000")
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            // Rate Limiting
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    var path = context.HttpContext.Request.Path;

                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Rate limit exceeded. IP={IP}, Path={Path}", ip, path);

                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsync("{\"message\": \"Too many requests. Please try again later.\"}", token);
                };

                options.AddPolicy("AuthLimiter", httpContext =>
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ip,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                });
            });

            return services;
        }

        // 4. Swagger / OpenAPI Configuration
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "TrainingCenter API", Version = "v1" });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token directly below."
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
}