using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using HealthChecks.UI.Client;
using Marketplace.Api.Exceptions;
using Marketplace.Api.Options;
using Marketplace.Api.Repositories;
using Marketplace.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Npgsql;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// === Options Pattern ===
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

builder.Services.Configure<PostgresOptions>(
    builder.Configuration.GetSection(PostgresOptions.SectionName));

builder.Services.Configure<RedisOptions>(
    builder.Configuration.GetSection(RedisOptions.SectionName));

// === JWT ===
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section is not configured.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException("Jwt:Key is not configured.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(jwtOptions.Issuer),
            ValidIssuer = jwtOptions.Issuer,

            ValidateAudience = !string.IsNullOrEmpty(jwtOptions.Audience),
            ValidAudience = jwtOptions.Audience,

            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

// === PostgreSQL ===
var postgresOptions = builder.Configuration
    .GetSection(PostgresOptions.SectionName)
    .Get<PostgresOptions>()
    ?? throw new InvalidOperationException("ConnectionStrings section is not configured.");

if (string.IsNullOrWhiteSpace(postgresOptions.DefaultConnection))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
}

builder.Services.AddSingleton(NpgsqlDataSource.Create(postgresOptions.DefaultConnection));

// === Swagger ===
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Введите JWT токен"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
    });
});

// === Repositories & Services ===
builder.Services.AddScoped<IdempotencyKeyRepository>();
builder.Services.AddScoped<OrderItemRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<OrderRepository>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<OrderService>();

builder.Services.AddSingleton<RedisCacheService>();

// === Redis ===
var redisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName)
    .Get<RedisOptions>()
    ?? throw new InvalidOperationException("Redis section is not configured.");

if (string.IsNullOrWhiteSpace(redisOptions.ConnectionString))
{
    throw new InvalidOperationException("Redis:ConnectionString is not configured.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisOptions.ConnectionString));

builder.Services.AddHostedService<PendingOrderCancellationService>();

// === Health Checks ===
builder.Services.AddHealthChecks()
    .AddNpgSql(
        postgresOptions.DefaultConnection,
        name: "postgres",
        tags: new[] { "db", "postgres" })
    .AddRedis(
        redisOptions.ConnectionString,
        name: "redis",
        tags: new[] { "cache", "redis" });

var app = builder.Build();
_ = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.Run();