using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Repositories;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Services
{
    public class TrilhaService
    {
        private readonly ITrilhaRepository _trilhaRepository;
        private readonly ICertificateRepository _certificateRepository;
        private readonly CertificateService _certificateService;

        public TrilhaService(
            ITrilhaRepository trilhaRepository,
            ICertificateRepository certificateRepository,
            CertificateService certificateService)
        {
            _trilhaRepository = trilhaRepository;
            _certificateRepository = certificateRepository;
            _certificateService = certificateService;
        }

        /// <summary>
        /// Cria uma nova trilha
        /// </summary>
        public async Task<(bool Success, string[] Errors)> CreateAsync(TrilhaDto dto, string criadoPorId)
        {
            // Validações
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Nome))
                errors.Add("Nome da trilha é obrigatório");

            if (dto.CertificadosIds == null || !dto.CertificadosIds.Any())
                errors.Add("Selecione pelo menos um certificado");

            if (errors.Any())
                return (false, errors.ToArray());

            // Verifica se os certificados existem
            var todosCertificados = await _certificateRepository.GetAllAsync();
            var certificadosInvalidos = dto.CertificadosIds
                .Where(id => !todosCertificados.Any(c => c.Id == id))
                .ToList();

            if (certificadosInvalidos.Any())
            {
                errors.Add($"Certificados não encontrados: {string.Join(", ", certificadosInvalidos)}");
                return (false, errors.ToArray());
            }

            var trilha = new Trilha
            {
                Nome = dto.Nome.Trim(),
                Descricao = dto.Descricao?.Trim(),
                CertificadosIdsList = dto.CertificadosIds,
                Ativa = dto.Ativa,
                CriadoPorId = criadoPorId,
                DataCriacao = DateTime.Now
            };

            await _trilhaRepository.AddAsync(trilha);

            Console.WriteLine($"✅ Trilha criada: {trilha.Nome} ({trilha.CertificadosIdsList.Count} certificados)");
            return (true, Array.Empty<string>());
        }

        /// <summary>
        /// Atualiza uma trilha existente
        /// </summary>
        public async Task<(bool Success, string[] Errors)> UpdateAsync(int id, TrilhaDto dto)
        {
            var trilha = await _trilhaRepository.GetByIdAsync(id);
            if (trilha == null)
                return (false, new[] { "Trilha não encontrada" });

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(dto.Nome))
                errors.Add("Nome da trilha é obrigatório");

            if (dto.CertificadosIds == null || !dto.CertificadosIds.Any())
                errors.Add("Selecione pelo menos um certificado");

            if (errors.Any())
                return (false, errors.ToArray());

            // Verifica se os certificados existem
            var todosCertificados = await _certificateRepository.GetAllAsync();
            var certificadosInvalidos = dto.CertificadosIds
                .Where(id => !todosCertificados.Any(c => c.Id == id))
                .ToList();

            if (certificadosInvalidos.Any())
            {
                errors.Add($"Certificados não encontrados: {string.Join(", ", certificadosInvalidos)}");
                return (false, errors.ToArray());
            }

            trilha.Nome = dto.Nome.Trim();
            trilha.Descricao = dto.Descricao?.Trim();
            trilha.CertificadosIdsList = dto.CertificadosIds;
            trilha.Ativa = dto.Ativa;
            trilha.DataAtualizacao = DateTime.Now;

            await _trilhaRepository.UpdateAsync(trilha);

            Console.WriteLine($"✅ Trilha atualizada: {trilha.Nome}");
            return (true, Array.Empty<string>());
        }

        /// <summary>
        /// Lista todas as trilhas (para admin)
        /// </summary>
        public async Task<List<Trilha>> GetAllAsync()
        {
            return await _trilhaRepository.GetAllAsync();
        }

        /// <summary>
        /// Lista apenas trilhas ativas (para alunos)
        /// </summary>
        public async Task<List<Trilha>> GetAtivasAsync()
        {
            return await _trilhaRepository.GetAtivasAsync();
        }

        /// <summary>
        /// Busca trilha por ID
        /// </summary>
        public async Task<Trilha?> GetByIdAsync(int id)
        {
            return await _trilhaRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Deleta uma trilha
        /// </summary>
        public async Task DeleteAsync(int id)
        {
            await _trilhaRepository.DeleteAsync(id);
            Console.WriteLine($"🗑️ Trilha deletada: ID {id}");
        }

        /// <summary>
        /// Gera certificados de uma trilha para um aluno
        /// </summary>
        public async Task<MemoryStream> GerarCertificadosTrilhaAsync(int trilhaId, string nomeAluno)
        {
            var trilha = await _trilhaRepository.GetByIdAsync(trilhaId);

            if (trilha == null)
                throw new Exception("Trilha não encontrada");

            if (!trilha.Ativa)
                throw new Exception("Esta trilha não está mais ativa");

            if (string.IsNullOrWhiteSpace(nomeAluno))
                throw new ArgumentException("Nome do aluno é obrigatório");

            Console.WriteLine($"🎓 Gerando certificados da trilha '{trilha.Nome}' para: {nomeAluno}");
            Console.WriteLine($"📚 Total de certificados: {trilha.CertificadosIdsList.Count}");

            // Usa o método existente do CertificateService
            return await _certificateService.GerarTrilhaCertificadosAsync(
                trilha.CertificadosIdsList,
                nomeAluno
            );
        }

        /// <summary>
        /// Retorna detalhes da trilha com nomes dos certificados
        /// </summary>
        public async Task<TrilhaDetalhesDto> GetDetalhesAsync(int id)
        {
            var trilha = await _trilhaRepository.GetByIdAsync(id);
            if (trilha == null)
                throw new Exception("Trilha não encontrada");

            var todosCertificados = await _certificateRepository.GetAllAsync();
            var certificadosDaTrilha = todosCertificados
                .Where(c => trilha.CertificadosIdsList.Contains(c.Id))
                .ToList();

            return new TrilhaDetalhesDto
            {
                Id = trilha.Id,
                Nome = trilha.Nome,
                Descricao = trilha.Descricao,
                Ativa = trilha.Ativa,
                DataCriacao = trilha.DataCriacao,
                Certificados = certificadosDaTrilha.Select(c => new CertificadoResumoDto
                {
                    Id = c.Id,
                    NomeCurso = c.NomeCurso,
                    CargaHoraria = c.CargaHoraria ?? 0,
                    NomeInstituicao = c.NomeInstituicao
                }).ToList()
            };
        }
    }

    public class TrilhaDetalhesDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public bool Ativa { get; set; }
        public DateTime DataCriacao { get; set; }
        public List<CertificadoResumoDto> Certificados { get; set; } = new();
    }

}