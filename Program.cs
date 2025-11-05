using AuthDemo.Data;
using AuthDemo.Repositories;
using AuthDemo.Services;
using DotNetEnv;
using DotNetEnv.Configuration;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

// Carregar variáveis do .env
Env.Load();
var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();
builder.Configuration.AddDotNetEnv();

// --- Configuração do SQLite com caminho absoluto ---
var connectionString = Environment.GetEnvironmentVariable("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("DefaultConnection não configurado no .env");
}

// Se for relativo, converte para caminho absoluto dentro de um diretório gravável
if (!Path.IsPathRooted(connectionString))
{
    var dbFolder = Path.Combine(Directory.GetCurrentDirectory(), "data");
    Directory.CreateDirectory(dbFolder);
    connectionString = Path.Combine(dbFolder, connectionString);
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={connectionString}"));

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

// --- Criar banco e aplicar migrations automaticamente ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate(); // cria o banco e as tabelas, incluindo Users
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
