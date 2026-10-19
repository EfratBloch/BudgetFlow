using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YouthGroups.Core.Common;
using YouthGroups.Core.Interfaces.Repositories;
using YouthGroups.Core.Interfaces.Services;
using YouthGroups.Core.Settings;
using YouthGroups.Data;
using YouthGroups.Data.Repositories;
using YouthGroups.Data.Seed;
using YouthGroups.Service.Budget;
using YouthGroups.Service.Mapping;
using YouthGroups.Service.Security;
using YouthGroups.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// The connection string comes from configuration (User Secrets in development), never from code
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Settings from appsettings.json (+ User Secrets for Jwt:Key), read through IOptions<T>
builder.Services.Configure<BudgetSettings>(builder.Configuration.GetSection(BudgetSettings.SectionName));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));

// Data layer. Scoped = one instance per HTTP request, so every repository in a request
// shares the same AppDbContext and one SaveChangesAsync writes all their changes.
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();
builder.Services.AddScoped<IMeetingRepository, MeetingRepository>();
builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
builder.Services.AddScoped<IBudgetAdditionRepository, BudgetAdditionRepository>();
builder.Services.AddScoped<DbSeeder>();

// Service layer
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IParticipantService, ParticipantService>();
builder.Services.AddScoped<IBudgetService, BudgetService>();
builder.Services.AddScoped<IBudgetAdditionService, BudgetAdditionService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddScoped<IEquipmentService, EquipmentService>();
builder.Services.AddScoped<BudgetPots>();

// Stateless helpers - one instance for the whole application
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<BudgetCalculator>();

builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// JWT authentication: every request may carry "Authorization: Bearer <token>".
// The token is checked against the same Key, Issuer and Audience that AuthService signs with.
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ?? new JwtSettings();

// Fail at startup, not on the first login, if the secret key is missing or too short
if (Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 bytes. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random value>\"");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep our short claim names ("userId", "role") as they are in the token
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1), // the default is 5 minutes of grace after expiry

            // [Authorize(Roles = "Coordinator")] reads this claim
            RoleClaimType = AppClaimTypes.Role,
            NameClaimType = AppClaimTypes.UserId
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint needs a valid token unless it says [AllowAnonymous]
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // The API description is public, otherwise the fallback policy would hide it behind a login
    app.MapOpenApi().AllowAnonymous();
}

app.UseHttpsRedirection();

// Order matters: first find out who the caller is, then check what she may do
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
