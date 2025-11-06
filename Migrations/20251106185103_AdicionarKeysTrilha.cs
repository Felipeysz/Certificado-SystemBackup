using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Certificado.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarKeysTrilha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    NomeCurso = table.Column<string>(type: "TEXT", nullable: false),
                    CargaHoraria = table.Column<int>(type: "INTEGER", nullable: true),
                    DataInicio = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DataTermino = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    NomeInstituicao = table.Column<string>(type: "TEXT", nullable: false),
                    EnderecoInstituicao = table.Column<string>(type: "TEXT", nullable: true),
                    Cidade = table.Column<string>(type: "TEXT", nullable: true),
                    DataEmissao = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    LogoInstituicao = table.Column<string>(type: "TEXT", nullable: true),
                    NomeResponsavel = table.Column<string>(type: "TEXT", nullable: true),
                    CargoResponsavel = table.Column<string>(type: "TEXT", nullable: true),
                    Assinatura = table.Column<string>(type: "TEXT", nullable: true),
                    SeloQrCode = table.Column<string>(type: "TEXT", nullable: true),
                    CodigoCertificado = table.Column<string>(type: "TEXT", nullable: true),
                    CertificadoVazio = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trilhas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CertificadosIds = table.Column<string>(type: "TEXT", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Ativa = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoPorId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trilhas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: false),
                    Permission = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "PasswordHash", "Permission", "Username" },
                values: new object[] { 1, "admin@neoauth.local", "7UJyiYcYaU29pOxBRAZ7eDaHGvRx7hrMoESDIpT/N8U=", "Admin", "admin" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Certificates");

            migrationBuilder.DropTable(
                name: "Trilhas");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
