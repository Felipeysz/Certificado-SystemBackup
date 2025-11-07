using AuthDemo.DTOs;
using AuthDemo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Controllers
{
    [Authorize]
    public class CertificadosController : Controller
    {
        private readonly CertificateService _service;

        public CertificadosController(CertificateService service)
        {
            _service = service;
        }

        /// <summary>
        /// GET: /Certificados/Create
        /// Exibe formulário para criar novo certificado
        /// </summary>
        [HttpGet]
        public IActionResult Create()
        {
            return View(new CertificateDto());
        }

        /// <summary>
        /// POST: /Certificados/Create
        /// Processa a criação de um novo certificado
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CertificateDto dto,
            IFormFile? certificadoVazioFile,
            IFormFile? logoFile,
            IFormFile? assinaturaFile)
        {
            Console.WriteLine($"📄 PDF Base64: {(string.IsNullOrEmpty(dto.CertificadoGeradoBase64) ? "VAZIO" : "OK")}");
            Console.WriteLine($"📄 Config: {(string.IsNullOrEmpty(dto.NomeAlunoConfig) ? "VAZIO" : "OK")}");
            Console.WriteLine($"📄 Arquivo: {(certificadoVazioFile == null ? "VAZIO" : "OK")}");

            var (isSuccess, serviceErrors) = await _service.CreateAsync(dto, certificadoVazioFile, logoFile, assinaturaFile);

            ModelState.Clear();

            if (!isSuccess)
            {
                foreach (var err in serviceErrors)
                    ModelState.AddModelError(string.Empty, err);

                return View(dto);
            }

            TempData["SuccessMessage"] = "Certificado criado com sucesso!";
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// GET: /Certificados/View/{id}
        /// Visualiza detalhes de um certificado específico
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> View(int id)
        {
            try
            {
                var certificados = await _service.GetAllAsync();
                var certificado = certificados.Find(c => c.Id == id);

                if (certificado == null)
                {
                    TempData["ErrorMessage"] = "Certificado não encontrado.";
                    return RedirectToAction("Index", "Home");
                }

                return View(certificado);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao visualizar certificado: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao carregar certificado.";
                return RedirectToAction("Index", "Home");
            }
        }

        /// <summary>
        /// POST: /Certificados/Delete
        /// Deleta um certificado e seus arquivos associados
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _service.DeleteAsync(id);
                TempData["SuccessMessage"] = "Certificado excluído com sucesso!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao excluir certificado: {ex.Message}");
                TempData["ErrorMessage"] = "Erro ao excluir certificado.";
            }

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// GET: /Certificados/EnviarAluno?nomeCurso=NomeDoCurso
        /// Exibe formulário para certificar um único aluno (PÚBLICO)
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // 🆕 Permite acesso sem autenticação
        public IActionResult EnviarAluno(string nomeCurso)
        {
            if (string.IsNullOrWhiteSpace(nomeCurso))
            {
                TempData["ErrorMessage"] = "Nome do curso não informado.";
                return RedirectToAction("Login", "Auth"); // 🆕 Redireciona para login se não autenticado
            }

            ViewBag.NomeCurso = nomeCurso;
            return View();
        }

        /// <summary>
        /// POST: /Certificados/EnviarAluno
        /// Gera certificado para um único aluno e retorna PDF para download (PÚBLICO)
        /// </summary>
        [HttpPost]
        [AllowAnonymous] // 🆕 Permite acesso sem autenticação
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarAluno(string nomeCurso, string nomeAluno)
        {
            if (string.IsNullOrWhiteSpace(nomeCurso) || string.IsNullOrWhiteSpace(nomeAluno))
            {
                TempData["ErrorMessage"] = "Nome do curso e do aluno são obrigatórios.";
                ViewBag.NomeCurso = nomeCurso;
                return View();
            }

            try
            {
                Console.WriteLine($"🔵 Gerando certificado individual para: {nomeAluno.Trim()}");

                var pdfBytes = await _service.CertificarAlunoAsync(nomeCurso, nomeAluno.Trim());

                // Sanitiza o nome do arquivo
                var safeFileName = string.Concat(nomeAluno.Trim().Split(Path.GetInvalidFileNameChars()));
                var fileName = $"{safeFileName}_certificado.pdf";

                Console.WriteLine($"✅ Certificado gerado: {fileName} ({pdfBytes.Length} bytes)");

                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"❌ Template não encontrado: {ex.Message}");
                TempData["ErrorMessage"] = "Template de certificado não encontrado. Verifique se o certificado foi criado corretamente.";
                ViewBag.NomeCurso = nomeCurso;
                return View();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao gerar certificado: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Erro ao gerar certificado: {ex.Message}";
                ViewBag.NomeCurso = nomeCurso;
                return View();
            }
        }
    }
}