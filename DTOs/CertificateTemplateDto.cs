using System;
using System.ComponentModel.DataAnnotations;

namespace AuthDemo.DTOs
{
    public class CertificateDto
    {
        public int? Id { get; set; }

        // 1. Identificação do curso
        [Required]
        public string NomeCurso { get; set; } = null!;
        public int? CargaHoraria { get; set; }
        public DateOnly? DataInicio { get; set; }
        public DateOnly? DataTermino { get; set; }

        // 2. Instituição e validade
        [Required]
        public string NomeInstituicao { get; set; } = null!;
        public string? EnderecoInstituicao { get; set; }
        public string? Cidade { get; set; }
        [Required]
        public DateOnly DataEmissao { get; set; } = DateOnly.FromDateTime(DateTime.Now);

        // Logo da instituição (arquivo salvo em wwwroot/img/logoInstituicao)
        public string? LogoInstituicao { get; set; }

        // 4. Assinaturas e validação
        public string? NomeResponsavel { get; set; }
        public string? CargoResponsavel { get; set; }
        public string? Assinatura { get; set; } // Base64 ou caminho da imagem
        public string? SeloQrCode { get; set; }

        // 6. Número ou código do certificado
        public string? CodigoCertificado { get; set; }

        // Certificado modelo visual (CertificadoVazio)
        public string? CertificadoVazio { get; set; } // Caminho do arquivo de upload

        // Nova propriedade para receber o certificado gerado pelo html2canvas
        public string? CertificadoGeradoBase64 { get; set; }

        public string? NomeAlunoConfig { get; set; } // Recebe posição/font/cor/tamanho do nome do aluno
    }

}
