using TrainingCenter.Extensions;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// 1) Configure Logging Standards
// =========================================================
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// =========================================================
// 2) Configure Application Services (Modular Extensions)
// =========================================================
builder.Services.AddApplicationServices(builder.Configuration)
                .AddJwtAuthentication(builder.Configuration)
                .AddSecurityPolicies()
                .AddSwaggerDocumentation();

// =========================================================
// 3) Middleware Pipeline
// =========================================================
var app = builder.Build();

// Global Exception Handler
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("AllowSpecificOrigins");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Custom Security Audit Middleware
app.UseSecurityAuditLogging();

app.MapControllers();

app.Run();