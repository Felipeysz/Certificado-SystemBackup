using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace AuthDemo.Controllers
{
    public class TrilhasController : Controller
    {
        private readonly TrilhaService _trilhaService;
        private readonly CertificateService _certificateService;

        public TrilhasController(TrilhaService trilhaService, CertificateService certificateService)
        {
            _trilhaService = trilhaService;
            _certificateService = certificateService;
        }

        // ==========================================
        // ÁREA DO ADMIN/INSTRUTOR
        // ==========================================

        /// <summary>
        /// GET: /Trilhas
        /// Lista todas as trilhas (admin/instrutor)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var trilhas = await _trilhaService.GetAllAsync();
                return View(trilhas);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao listar trilhas: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao carregar trilhas.";
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// GET: /Trilhas/Create
        /// Formulário para criar nova trilha (admin/instrutor)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Create()
        {
            try
            {
                var certificados = await _certificateService.GetAllAsync();

                if (!certificados.Any())
                {
                    TempData["ErrorMessage"] = "Crie certificados antes de criar trilhas.";
                    return RedirectToAction("Index");
                }

                ViewBag.Certificados = certificados;
                return View(new TrilhaDto());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao carregar formulário: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao carregar formulário.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: /Trilhas/Create
        /// Cria uma nova trilha (admin/instrutor)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Create(TrilhaDto dto, int[] certificadosSelecionados)
        {
            dto.CertificadosIds = certificadosSelecionados?.ToList() ?? new();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var (success, errors) = await _trilhaService.CreateAsync(dto, userId);

            if (!success)
            {
                foreach (var error in errors)
                    ModelState.AddModelError(string.Empty, error);

                ViewBag.Certificados = await _certificateService.GetAllAsync();
                return View(dto);
            }

            TempData["SuccessMessage"] = $"Trilha '{dto.Nome}' criada com sucesso!";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// GET: /Trilhas/Edit/{id}
        /// Formulário para editar trilha (admin/instrutor)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var trilha = await _trilhaService.GetByIdAsync(id);
                if (trilha == null)
                {
                    TempData["ErrorMessage"] = "Trilha não encontrada.";
                    return RedirectToAction("Index");
                }

                var dto = new TrilhaDto
                {
                    Id = trilha.Id,
                    Nome = trilha.Nome,
                    Descricao = trilha.Descricao,
                    CertificadosIds = trilha.CertificadosIdsList,
                    Ativa = trilha.Ativa
                };

                ViewBag.Certificados = await _certificateService.GetAllAsync();
                return View(dto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao carregar trilha: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao carregar trilha.";
                return RedirectToAction("Index");
            }
        }

        /// <summary>
        /// POST: /Trilhas/Edit/{id}
        /// Atualiza uma trilha (admin/instrutor)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Edit(int id, TrilhaDto dto, int[] certificadosSelecionados)
        {
            dto.CertificadosIds = certificadosSelecionados?.ToList() ?? new();

            var (success, errors) = await _trilhaService.UpdateAsync(id, dto);

            if (!success)
            {
                foreach (var error in errors)
                    ModelState.AddModelError(string.Empty, error);

                ViewBag.Certificados = await _certificateService.GetAllAsync();
                return View(dto);
            }

            TempData["SuccessMessage"] = $"Trilha '{dto.Nome}' atualizada com sucesso!";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// POST: /Trilhas/Delete/{id}
        /// Deleta uma trilha (SOMENTE ADMIN)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _trilhaService.DeleteAsync(id);
                TempData["SuccessMessage"] = "Trilha excluída com sucesso!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao excluir trilha: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao excluir trilha.";
            }

            return RedirectToAction("Index");
        }

        /// <summary>
        /// GET: /Trilhas/Detalhes/{id}
        /// Visualiza detalhes da trilha (admin/instrutor)
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin,Instrutor")]
        public async Task<IActionResult> Detalhes(int id)
        {
            try
            {
                var detalhes = await _trilhaService.GetDetalhesAsync(id);
                return View(detalhes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao carregar detalhes: {ex.Message}");
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index");
            }
        }

        // ==========================================
        // ÁREA PÚBLICA (SEM AUTENTICAÇÃO)
        // ==========================================

        /// <summary>
        /// GET: /Trilhas/Gerar/{id}
        /// Formulário PÚBLICO para gerar certificados de uma trilha
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Gerar(int id)
        {
            try
            {
                var detalhes = await _trilhaService.GetDetalhesAsync(id);

                if (!detalhes.Ativa)
                {
                    ViewBag.ErrorMessage = "Esta trilha não está mais disponível.";
                    return View("TrilhaInativa");
                }

                return View(detalhes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao carregar trilha: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                ViewBag.ErrorMessage = "Trilha não encontrada.";
                return View("Erro");
            }
        }

        /// <summary>
        /// POST: /Trilhas/Gerar/{id}
        /// Gera certificados da trilha PUBLICAMENTE como PDF único
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> Gerar(int id, string nomeAluno)
        {
            if (string.IsNullOrWhiteSpace(nomeAluno))
            {
                TempData["ErrorMessage"] = "Nome do aluno é obrigatório.";
                return RedirectToAction("Gerar", new { id });
            }

            try
            {
                Console.WriteLine($"🎓 Gerando certificados da trilha ID {id}: {nomeAluno.Trim()}");

                var pdfStream = await _trilhaService.GerarCertificadosTrilhaAsync(id, nomeAluno.Trim());

                var trilha = await _trilhaService.GetByIdAsync(id);

                if (trilha == null)
                {
                    throw new Exception("Trilha não encontrada");
                }

                var nomeTrilha = trilha.Nome ?? "Trilha";
                var safeNome = string.Concat(nomeAluno.Trim().Split(Path.GetInvalidFileNameChars()));
                var safeTrilha = string.Concat(nomeTrilha.Split(Path.GetInvalidFileNameChars()));
                var outputFileName = $"{safeTrilha}_{safeNome}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf"; // 🆕 Mudou de .zip para .pdf

                Console.WriteLine($"✅ Certificados gerados: {outputFileName}");

                return File(pdfStream, "application/pdf", outputFileName); // 🆕 Mudou de application/zip para application/pdf
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao gerar certificados: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Erro ao gerar certificados: {ex.Message}";
                return RedirectToAction("Gerar", new { id });
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Publicas()
        {
            try
            {
                var trilhasAtivas = await _trilhaService.GetAtivasAsync();
                return View(trilhasAtivas);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao listar trilhas: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao carregar trilhas.";
                return View(new List<Trilha>());
            }
        }
    }
}