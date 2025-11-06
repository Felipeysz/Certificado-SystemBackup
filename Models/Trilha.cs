// ============================================
// 📁 Models/Trilha.cs
// ============================================
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AuthDemo.Models
{
    /// <summary>
    /// Representa uma trilha de certificados criada pelo instrutor
    /// </summary>
    public class Trilha
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Nome da trilha é obrigatório")]
        [StringLength(200)]
        public string Nome { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descricao { get; set; }

        /// <summary>
        /// IDs dos certificados que compõem esta trilha (JSON serializado)
        /// Exemplo: "[1,3,5,7]"
        /// </summary>
        [Required]
        public string CertificadosIds { get; set; } = "[]";

        [Required]
        public DateTime DataCriacao { get; set; } = DateTime.Now;

        public DateTime? DataAtualizacao { get; set; }

        /// <summary>
        /// Se true, a trilha está ativa e pode ser usada pelos alunos
        /// </summary>
        public bool Ativa { get; set; } = true;

        /// <summary>
        /// ID do usuário que criou a trilha (instrutor)
        /// </summary>
        public string? CriadoPorId { get; set; }

        // ⭐ Propriedade auxiliar para deserializar os IDs
        [NotMapped]
        public List<int> CertificadosIdsList
        {
            get
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<List<int>>(CertificadosIds) ?? new List<int>();
                }
                catch
                {
                    return new List<int>();
                }
            }
            set
            {
                CertificadosIds = System.Text.Json.JsonSerializer.Serialize(value);
            }
        }
    }
}