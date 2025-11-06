using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Repositories;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthDemo.Controllers
{
    [Authorize]
    public class UsersController : Controller
    {
        private readonly AuthService _authService;
        private readonly UserRepository _userRepository;

        public UsersController(AuthService authService, UserRepository userRepository)
        {
            _authService = authService;
            _userRepository = userRepository;
        }

        // GET: /Users/Create
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Users/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserCreateDto dto)
        {
            var permission = User.FindFirst("Permission")?.Value ?? string.Empty;
            if (!string.Equals(permission, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var (success, error) = await _authService.RegisterCollaboratorAsync(dto);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Erro ao criar usuário.");
                return View(dto);
            }

            TempData["SuccessMessage"] = "Colaborador criado com sucesso.";
            return RedirectToAction("Index", "Home");
        }

        // POST: /Users/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            // somente Admin pode deletar
            var permission = User.FindFirst("Permission")?.Value ?? string.Empty;
            if (!string.Equals(permission, "Admin", StringComparison.OrdinalIgnoreCase))
                return Forbid();

            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound();

            // só deletar Colaborador
            if (user.Permission != UserPermission.Colaborador)
                return BadRequest("Apenas contas com cargo 'Colaborador' podem ser excluídas.");

            // impede self-delete (caso admin tente excluir a própria conta)
            var currentUsername = User.Identity?.Name ?? User.FindFirst(ClaimTypes.Name)?.Value;
            if (!string.IsNullOrEmpty(currentUsername) &&
                string.Equals(currentUsername, user.Username, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Não é permitido excluir sua própria conta.");
            }

            await _userRepository.DeleteAsync(user);
            TempData["SuccessMessage"] = $"Usuário '{user.Username}' excluído.";

            return RedirectToAction("Index", "Home");
        }
    }

    [Authorize]
    public class HomeController : Controller
    {
        private readonly UserRepository _userRepository;
        private readonly CertificateService _certificateService;

        public HomeController(UserRepository userRepository, CertificateService certificateService)
        {
            _userRepository = userRepository;
            _certificateService = certificateService;
        }

        public async Task<IActionResult> Index()
        {
            var permission = User.FindFirst(ClaimTypes.Role)?.Value ?? "Colaborador";

            // Buscar certificados reais do service
            var certificadosEntities = await _certificateService.GetAllAsync();
            var certificadosDto = certificadosEntities.Select(c => new CertificateItemDto
            {
                Id = c.Id,
                NomeCurso = c.NomeCurso,
                NomeInstituicao = c.NomeInstituicao,
                DataEmissao = c.DataEmissao.ToString("yyyy-MM-dd"),
                CodigoCertificado = c.CodigoCertificado,
                CertificadoVazio = c.CertificadoVazio ?? "~/images/certificados/placeholder.png"
            }).ToList();

            // Buscar usuários reais do repositório
            var users = await _userRepository.GetAllAsync();
            var usuariosDto = users.Select(u => new UserItemDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                Permission = u.Permission.ToString()
            }).ToList();

            var vm = new DashboardViewModel
            {
                Permission = permission,
                Certificados = certificadosDto,
                Usuarios = usuariosDto
            };

            return View(vm);
        }
    }
    
    public class AuthController : Controller
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(UserLoginDto dto, bool rememberMe)
        {
            var (result, error) = await _authService.LoginAsync(dto);

            if (result == null)
            {
                ViewBag.Error = error;
                return View(dto);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, dto.Login),
                new Claim("Token", result.Token),
                new Claim(ClaimTypes.Role, result.Permission)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
