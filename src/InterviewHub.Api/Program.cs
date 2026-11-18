using System.Text;
using System.Threading.RateLimiting;
using InterviewHub.Api.Auth;
using InterviewHub.Business.InterviewerExperiences;
using InterviewHub.Business.Moderation;
using InterviewHub.Business.CandidateExperiences;
using InterviewHub.Business.Questions;
using InterviewHub.Business.Experiences;
using InterviewHub.Business.Reports;
using InterviewHub.Business.InterviewerProfiles;
using InterviewHub.Data;
using InterviewHub.Data.Entities;
using InterviewHub.Data.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Identity stores users and roles in SQL Server through the shared DbContext.
builder.Services.AddDbContext<InterviewHubDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<InterviewHubDbContext>()
    .AddDefaultTokenProviders();

// JWT validation uses the same issuer, audience, key, and algorithm as issuance.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(x => !string.IsNullOrWhiteSpace(x.Issuer) && !string.IsNullOrWhiteSpace(x.Audience)
        && !string.IsNullOrWhiteSpace(x.SigningKey) && Encoding.UTF8.GetByteCount(x.SigningKey) >= 32
        && x.LifetimeMinutes is >= 1 and <= 1440, "Valid JWT settings are required.")
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
if (jwt is null || string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
    throw new InvalidOperationException("Set Jwt:Issuer, Jwt:Audience, and a 32-byte Jwt:SigningKey.");

builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();
// A configured React origin can call the API without opening CORS to arbitrary sites.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0) policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));
// Basic per-client limits protect account endpoints and submissions from bursts.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("submit", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddScoped<InterviewerExperienceService>();
builder.Services.AddScoped<AdminModerationService>();
builder.Services.AddScoped<CandidateExperienceService>();
builder.Services.AddScoped<QuestionCatalogService>();
builder.Services.AddScoped<ExperienceSearchService>();
builder.Services.AddScoped<ReportSummaryService>();
builder.Services.AddScoped<ReportQueryService>();
builder.Services.AddScoped<InterviewerProfileService>();
// String enums keep request and response values readable for React clients.
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    // Model validation returns a readable payload that the UI can display.
    options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new
        {
            error = "اطلاعات واردشده معتبر نیست. فیلدهای مشخص‌شده را بررسی کنید.",
            errors = context.ModelState.Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray())
        });
});

var app = builder.Build();

// Unexpected failures get a safe JSON message; internal exception details stay in server logs.
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { error = "خطایی در سرور رخ داد. کمی بعد دوباره تلاش کنید." });
}));
// Controllers and authorization sometimes return an empty status response.
app.UseStatusCodePages(async statusContext =>
{
    var response = statusContext.HttpContext.Response;
    if (!statusContext.HttpContext.Request.Path.StartsWithSegments("/api") ||
        response.ContentLength is > 0 || !string.IsNullOrEmpty(response.ContentType)) return;
    var message = response.StatusCode switch
    {
        400 => "درخواست معتبر نیست. اطلاعات را بررسی کنید.",
        401 => "برای ادامه وارد حساب شوید.",
        403 => "دسترسی لازم برای این بخش را ندارید.",
        404 => "مورد درخواستی پیدا نشد.",
        429 => "تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.",
        500 => "خطایی در سرور رخ داد. کمی بعد دوباره تلاش کنید.",
        _ => "درخواست انجام نشد. دوباره تلاش کنید."
    };
    await response.WriteAsJsonAsync(new { error = message });
});

await IdentityBootstrapper.InitializeAsync(app.Services, app.Configuration);
// Seed only the General reference collection after the schema has been migrated.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<InterviewHubDbContext>();
    await DbSeeder.SeedGeneralAsync(db);
    await DbSeeder.BackfillInterviewerQuestionLinksAsync(db);
    await scope.ServiceProvider.GetRequiredService<ReportSummaryService>().RebuildAsync(CancellationToken.None);
}

// The free host enables HTTPS only after the first upload and support activation.
if (app.Configuration.GetValue("Hosting:EnableHttpsRedirection", true))
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors("Frontend");

// Authentication must run before role authorization to distinguish 401 from 403.
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
// The probe confirms both the API process and its SQL connection are usable.
app.MapGet("/health", async (InterviewHubDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

// Serve React routes from the same origin while keeping unknown API paths as 404.
var spaEntry = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
if (File.Exists(spaEntry))
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await context.Response.SendFileAsync(spaEntry);
    });

app.Run();
