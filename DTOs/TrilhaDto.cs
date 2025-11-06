// ============================================
// 📁 DTOs/TrilhaDto.cs
// ============================================
using AuthDemo.Data;
using AuthDemo.Models;
using AuthDemo.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace AuthDemo.DTOs
{
    public class TrilhaDto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nome da trilha é obrigatório")]
        [StringLength(200, ErrorMessage = "Nome deve ter no máximo 200 caracteres")]
        public string Nome { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Descrição deve ter no máximo 500 caracteres")]
        public string? Descricao { get; set; }

        [Required(ErrorMessage = "Selecione pelo menos um certificado")]
        public List<int> CertificadosIds { get; set; } = new List<int>();

        public bool Ativa { get; set; } = true;
    }

    public class CertificadoResumoDto
    {
        public int Id { get; set; }
        public string NomeCurso { get; set; } = string.Empty;
        public int CargaHoraria { get; set; }
        public string? NomeInstituicao { get; set; }
    }

}