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

// --- Configuração do SQLite usando variável de ambiente ---
var connectionString = Environment.GetEnvironmentVariable("DEFAULTCONNECTION");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DEFAULTCONNECTION não configurado no ambiente.");
}

// Conecta diretamente no arquivo de banco (mesmo nível da aplicação dentro do container)
if (!Path.IsPathRooted(connectionString))
{
    connectionString = Path.Combine(Directory.GetCurrentDirectory(), connectionString);
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={connectionString}"));

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
