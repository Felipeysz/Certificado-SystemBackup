using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthDemo.Services
{
    public class AuthService
    {
        private readonly UserRepository _repository;
        private readonly string _jwtKey;

        public AuthService(UserRepository repository, IConfiguration configuration)
        {
            _repository = repository;
            _jwtKey = configuration["JWT_KEY"]
                ?? throw new InvalidOperationException("JWT_KEY não configurado nas variáveis de ambiente.");
        }

        public async Task<(bool Success, string? Error)> RegisterCollaboratorAsync(UserCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return (false, "Todos os campos são obrigatórios.");

            var byUsername = await _repository.GetByLoginAsync(dto.Username);
            if (byUsername != null)
                return (false, "Nome de usuário já existe.");

            var byEmail = await _repository.GetByLoginAsync(dto.Email);
            if (byEmail != null)
                return (false, "E-mail já cadastrado.");

            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                PasswordHash = HashPassword(dto.Password),
                Permission = UserPermission.Colaborador
            };

            await _repository.AddAsync(user);
            return (true, null);
        }

        public async Task<(LoginResultDto? Result, string? Error)> LoginAsync(UserLoginDto dto)
        {
            var user = await _repository.GetByLoginAsync(dto.Login);
            if (user == null)
                return (null, "Usuário ou e-mail não encontrado");

            if (user.PasswordHash != HashPassword(dto.Password))
                return (null, "Senha incorreta");

            var token = GenerateJwtToken(user);
            var result = new LoginResultDto
            {
                Token = token,
                Permission = user.Permission.ToString()
            };

            return (result, null);
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("UserId", user.Id.ToString()),
                new Claim("Permission", user.Permission.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                )
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}
