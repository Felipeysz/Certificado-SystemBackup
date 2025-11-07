using AuthDemo.Data;
using AuthDemo.Repositories;
using AuthDemo.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// --- Configuração do Banco usando variável de ambiente ---
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DATABASE_URL não configurada no ambiente.");
}

// 🆕 Converte formato do Render (postgres://) para EF Core
if (connectionString.StartsWith("postgres://") || connectionString.StartsWith("postgresql://"))
{
    connectionString = connectionString.Replace("postgresql://", "").Replace("postgres://", "");

    var parts = connectionString.Split('@');
    if (parts.Length != 2)
    {
        throw new InvalidOperationException($"Formato inválido da DATABASE_URL. Esperado: postgresql://user:pass@host:port/db");
    }

    var userPass = parts[0].Split(':');
    if (userPass.Length != 2)
    {
        throw new InvalidOperationException($"Formato inválido do usuário/senha na DATABASE_URL");
    }

    var hostDbPort = parts[1].Split('/');
    if (hostDbPort.Length != 2)
    {
        throw new InvalidOperationException($"Formato inválido do host/database na DATABASE_URL");
    }

    var hostPort = hostDbPort[0].Split(':');
    var host = hostPort[0];
    var port = hostPort.Length > 1 ? hostPort[1] : "5432";
    var database = hostDbPort[1];
    var username = userPass[0];
    var password = userPass[1];

    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
}

// 🆕 Usa PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- JWT Key ---
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY");
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("JWT_KEY não configurada no ambiente.");
}

// --- Data Protection para antiforgery e cookies ---
var keysFolder = Path.Combine(Directory.GetCurrentDirectory(), "keys");
Directory.CreateDirectory(keysFolder);
builder.Services.AddDataProtection()
       .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
       .SetApplicationName("CertificadoSystem");

// --- Injeção de dependências ---
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ICertificateRepository, CertificateRepository>();
builder.Services.AddScoped<CertificateService>();
builder.Services.AddValidatorsFromAssemblyContaining<AuthService>();
builder.Services.AddSingleton<CloudStorageService>();
builder.Services.AddScoped<ITrilhaRepository, TrilhaRepository>();
builder.Services.AddScoped<TrilhaService>();

// --- Autenticação por cookie ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/Login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    });

// Autorização
builder.Services.AddAuthorization();

var app = builder.Build();

// 🆕 Aplica migrations automaticamente no startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    Console.WriteLine("✅ Migrations aplicadas com sucesso!");
}

// Middleware de erro
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/ErrorPage/Index");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Rotas padrão
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Auth}/{action=Login}/{id?}");

app.Run();