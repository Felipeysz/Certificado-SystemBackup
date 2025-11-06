using AuthDemo.DTOs;
using AuthDemo.Models;
using AuthDemo.Repositories;
using AuthDemo.Validators;
using FluentValidation.Results;
using System.Text.Json;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Layout.Properties;
using IOPath = System.IO.Path;
using Microsoft.AspNetCore.Hosting;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.IO;
using System;

namespace AuthDemo.Services
{
    public class CertificateService
    {
        private readonly ICertificateRepository _repository;
        private readonly CertificateDtoValidator _validator;
        private readonly IWebHostEnvironment _env;
        private readonly CloudStorageService _cloudStorage;
        private readonly ITrilhaRepository _trilhaRepository;

        public CertificateService(ICertificateRepository repository, IWebHostEnvironment env, CloudStorageService cloudStorage, ITrilhaRepository trilhaRepository)
        {
            _repository = repository;
            _env = env;
            _cloudStorage = cloudStorage;
            _trilhaRepository = trilhaRepository;
            _validator = new CertificateDtoValidator(_repository);
        }

        public async Task<(bool Success, string[] Errors)> CreateAsync(CertificateDto dto, IFormFile? certificadoVazioFile = null, IFormFile? logoFile = null, IFormFile? assinaturaFile = null)
        {
            ValidationResult result = await _validator.ValidateAsync(dto);
            if (!result.IsValid)
                return (false, result.Errors.Select(e => e.ErrorMessage).ToArray());

            var safeFileNameBase = string.Concat(dto.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));

            // ⭐ Salva PDF no Cloud Storage
            if (!string.IsNullOrEmpty(dto.CertificadoGeradoBase64))
            {
                try
                {
                    var base64Data = dto.CertificadoGeradoBase64.Contains(',')
                        ? dto.CertificadoGeradoBase64.Split(',')[1]
                        : dto.CertificadoGeradoBase64;

                    var bytes = Convert.FromBase64String(base64Data);

                    // Upload para Cloud Storage
                    var key = $"certificados/{safeFileNameBase}/{safeFileNameBase}.pdf";
                    var url = await _cloudStorage.UploadFileAsync(key, bytes, "application/pdf");

                    dto.CertificadoVazio = url; // Salva URL pública

                    Console.WriteLine($"✅ PDF salvo no Cloud: {url}");
                }
                catch (Exception ex)
                {
                    return (false, new[] { $"Erro ao salvar PDF no Cloud: {ex.Message}" });
                }
            }

            // ⭐ Salva Config no Cloud Storage (como JSON)
            if (!string.IsNullOrEmpty(dto.NomeAlunoConfig))
            {
                try
                {
                    var configBytes = System.Text.Encoding.UTF8.GetBytes(dto.NomeAlunoConfig);
                    var configKey = $"certificados/{safeFileNameBase}/{safeFileNameBase}.config";

                    await _cloudStorage.UploadFileAsync(configKey, configBytes, "application/json");

                    Console.WriteLine($"✅ Config salva no Cloud");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro ao salvar config: {ex.Message}");
                }
            }

            // ⭐ Logos e assinaturas também no Cloud
            if (logoFile != null && logoFile.Length > 0)
                dto.LogoInstituicao = await SaveFileToCloud(logoFile, "logos");

            if (assinaturaFile != null && assinaturaFile.Length > 0)
                dto.Assinatura = await SaveFileToCloud(assinaturaFile, "assinaturas");

            var certificate = new Certificate
            {
                NomeCurso = dto.NomeCurso,
                CargaHoraria = dto.CargaHoraria,
                DataInicio = dto.DataInicio,
                DataTermino = dto.DataTermino,
                NomeInstituicao = dto.NomeInstituicao,
                EnderecoInstituicao = dto.EnderecoInstituicao,
                Cidade = dto.Cidade,
                DataEmissao = dto.DataEmissao,
                LogoInstituicao = dto.LogoInstituicao,
                NomeResponsavel = dto.NomeResponsavel,
                CargoResponsavel = dto.CargoResponsavel,
                Assinatura = dto.Assinatura,
                SeloQrCode = dto.SeloQrCode,
                CodigoCertificado = dto.CodigoCertificado,
                CertificadoVazio = dto.CertificadoVazio
            };

            await _repository.AddAsync(certificate);
            return (true, Array.Empty<string>());
        }

        private async Task<string> SaveFileToCloud(IFormFile file, string folder)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var bytes = stream.ToArray();

            var extension = IOPath.GetExtension(file.FileName);
            var fileName = Guid.NewGuid().ToString() + extension;
            var key = $"{folder}/{fileName}";

            return await _cloudStorage.UploadFileAsync(key, bytes, file.ContentType);
        }

        public async Task<List<Certificate>> GetAllAsync() => await _repository.GetAllAsync();

        public async Task DeleteAsync(int id)
        {
            var certificate = await _repository.GetByIdAsync(id);
            if (certificate == null) return;

            // ⭐ NOVO: Verifica trilhas que contêm este certificado
            var todasTrilhas = await _trilhaRepository.GetAllAsync();
            var trilhasAfetadas = todasTrilhas
                .Where(t => t.CertificadosIdsList.Contains(id))
                .ToList();

            if (trilhasAfetadas.Any())
            {
                Console.WriteLine($"⚠️ Certificado '{certificate.NomeCurso}' faz parte de {trilhasAfetadas.Count} trilha(s)");

                foreach (var trilha in trilhasAfetadas)
                {
                    // Remove o certificado da lista
                    trilha.CertificadosIdsList.Remove(id);

                    // Se ficou sem certificados, desativa a trilha
                    if (!trilha.CertificadosIdsList.Any())
                    {
                        trilha.Ativa = false;
                        Console.WriteLine($"  ❌ Trilha '{trilha.Nome}' desativada (sem certificados)");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ Certificado removido da trilha '{trilha.Nome}' ({trilha.CertificadosIdsList.Count} restantes)");
                    }

                    trilha.DataAtualizacao = DateTime.Now;
                    await _trilhaRepository.UpdateAsync(trilha);
                }
            }

            // ⭐ Deletar arquivos do Cloud Storage (se for URL do R2)
            if (!string.IsNullOrEmpty(certificate.CertificadoVazio))
            {
                // Verifica se é URL do R2 (cloud) ou caminho local
                if (certificate.CertificadoVazio.StartsWith("https://pub-") ||
                    certificate.CertificadoVazio.StartsWith("http://") ||
                    certificate.CertificadoVazio.Contains(".r2.dev"))
                {
                    try
                    {
                        // Extrai o key da URL (tudo após o domínio)
                        var uri = new Uri(certificate.CertificadoVazio);
                        var key = uri.AbsolutePath.TrimStart('/');

                        Console.WriteLine($"🗑️ Deletando do Cloud: {key}");
                        await _cloudStorage.DeleteFileAsync(key);

                        // Também deleta o arquivo .config associado
                        var safeFileNameBase = string.Concat(certificate.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));
                        var configKey = $"certificados/{safeFileNameBase}/{safeFileNameBase}.config";

                        if (await _cloudStorage.FileExistsAsync(configKey))
                        {
                            await _cloudStorage.DeleteFileAsync(configKey);
                            Console.WriteLine($"🗑️ Config deletada do Cloud: {configKey}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Erro ao deletar do Cloud: {ex.Message}");
                    }
                }
                else
                {
                    // Sistema legado: deleta do filesystem local
                    var certificadoVazioPath = IOPath.Combine(_env.WebRootPath,
                        certificate.CertificadoVazio.TrimStart('/').Replace("/", IOPath.DirectorySeparatorChar.ToString()));
                    var certificateFolder = IOPath.GetDirectoryName(certificadoVazioPath);

                    if (Directory.Exists(certificateFolder))
                    {
                        Directory.Delete(certificateFolder, true);
                        Console.WriteLine($"🗑️ Pasta local deletada: {certificateFolder}");
                    }
                }
            }

            // ⭐ Deletar logo do Cloud (se existir)
            if (!string.IsNullOrEmpty(certificate.LogoInstituicao) &&
                (certificate.LogoInstituicao.Contains(".r2.dev") || certificate.LogoInstituicao.StartsWith("https://pub-")))
            {
                try
                {
                    var uri = new Uri(certificate.LogoInstituicao);
                    var key = uri.AbsolutePath.TrimStart('/');
                    await _cloudStorage.DeleteFileAsync(key);
                    Console.WriteLine($"🗑️ Logo deletada do Cloud: {key}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro ao deletar logo: {ex.Message}");
                }
            }

            // ⭐ Deletar assinatura do Cloud (se existir)
            if (!string.IsNullOrEmpty(certificate.Assinatura) &&
                (certificate.Assinatura.Contains(".r2.dev") || certificate.Assinatura.StartsWith("https://pub-")))
            {
                try
                {
                    var uri = new Uri(certificate.Assinatura);
                    var key = uri.AbsolutePath.TrimStart('/');
                    await _cloudStorage.DeleteFileAsync(key);
                    Console.WriteLine($"🗑️ Assinatura deletada do Cloud: {key}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Erro ao deletar assinatura: {ex.Message}");
                }
            }

            await _repository.DeleteAsync(id);
            Console.WriteLine($"✅ Certificado '{certificate.NomeCurso}' deletado com sucesso");
        }

        /// <summary>
        /// Gera certificado individual para um aluno em um curso específico
        /// </summary>
        public async Task<byte[]> CertificarAlunoAsync(string nomeCurso, string nomeAluno)
        {
            Console.WriteLine($"🔵 Gerando certificado para: {nomeAluno} | Curso: {nomeCurso}");

            var safeFileNameBase = string.Concat(nomeCurso.Split(IOPath.GetInvalidFileNameChars()));

            // ⭐ Baixa PDF template do Cloudflare R2
            var pdfKey = $"certificados/{safeFileNameBase}/{safeFileNameBase}.pdf";
            byte[] templateBytes;

            try
            {
                Console.WriteLine($"📥 Baixando template do Cloud: {pdfKey}");
                templateBytes = await _cloudStorage.DownloadFileAsync(pdfKey);
                Console.WriteLine($"✅ Template baixado: {templateBytes.Length} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao baixar template: {ex.Message}");
                throw new FileNotFoundException($"Template de certificado não encontrado no Cloud: {pdfKey}", ex);
            }

            // ⭐ Baixa config do Cloudflare R2
            var configKey = $"certificados/{safeFileNameBase}/{safeFileNameBase}.config";
            NomeAlunoConfig config = new NomeAlunoConfig();

            try
            {
                Console.WriteLine($"📥 Baixando config do Cloud: {configKey}");
                var configBytes = await _cloudStorage.DownloadFileAsync(configKey);
                var configJson = System.Text.Encoding.UTF8.GetString(configBytes);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new NumberOrStringToStringConverter());
                config = JsonSerializer.Deserialize<NomeAlunoConfig>(configJson, options) ?? new NomeAlunoConfig();

                Console.WriteLine($"✅ Config carregada: {configJson}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Config não encontrada, usando valores padrão: {ex.Message}");
            }

            // ⭐ MODIFICAÇÃO: Calcular largura máxima e ajustar fonte
            float maxWidth = config.Width > 0 ? config.Width : 400f;

            // Parse do tamanho base da fonte
            float baseFontSize = float.TryParse(
                (config.BaseFontSize ?? config.FontSize ?? "24px").Replace("px", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var bfs) ? bfs : 24f;

            // Parse das outras configurações
            float x = float.TryParse(
                (config.Left ?? "0").Replace("px", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var lx) ? lx : 50f;

            float y = float.TryParse(
                (config.Top ?? "0").Replace("px", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ty) ? ty : 50f;

            bool isBold = (config.FontWeight ?? "regular").ToLower() == "bold";

            // ⭐ Gera PDF com iText7 - Versão Robusta
            var outputStream = new MemoryStream();

            try
            {
                Console.WriteLine($"🔧 Iniciando geração do PDF...");

                // Cria uma cópia do template bytes para não afetar o original
                byte[] workingBytes = new byte[templateBytes.Length];
                Array.Copy(templateBytes, workingBytes, templateBytes.Length);

                using (var templateStream = new MemoryStream(workingBytes))
                {
                    // Configurações do Reader para ser mais tolerante
                    var readerProperties = new ReaderProperties();

                    Console.WriteLine($"📖 Criando PDF Reader...");
                    using (var reader = new PdfReader(templateStream, readerProperties))
                    {
                        Console.WriteLine($"✏️ Criando PDF Writer...");
                        using (var writer = new PdfWriter(outputStream))
                        {
                            // Importante: não fechar o stream de saída automaticamente
                            writer.SetCloseStream(false);

                            Console.WriteLine($"📄 Criando PDF Document...");
                            using (var pdfDoc = new PdfDocument(reader, writer))
                            {
                                Console.WriteLine($"📝 Número de páginas: {pdfDoc.GetNumberOfPages()}");

                                using (var document = new Document(pdfDoc))
                                {
                                    // Obtém o tamanho da primeira página
                                    var page = pdfDoc.GetFirstPage();
                                    if (page == null)
                                    {
                                        throw new Exception("PDF não contém páginas válidas");
                                    }

                                    var pageSize = page.GetPageSize();
                                    Console.WriteLine($"📄 Tamanho da página: {pageSize.GetWidth()}x{pageSize.GetHeight()}");

                                    // Seleciona fonte ANTES de calcular coordenadas
                                    Console.WriteLine($"🔤 Carregando fonte {(isBold ? "Bold" : "Regular")}...");
                                    PdfFont font;
                                    if (isBold)
                                    {
                                        font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                                    }
                                    else
                                    {
                                        font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
                                    }

                                    // ⭐ AUTO-AJUSTE DO TAMANHO DA FONTE
                                    float fontSize = baseFontSize;
                                    float textWidth = font.GetWidth(nomeAluno, fontSize);

                                    Console.WriteLine($"📏 Tamanho inicial da fonte: {fontSize}px");
                                    Console.WriteLine($"📏 Largura do texto: {textWidth}px / Máxima: {maxWidth}px");

                                    // Se o texto não couber, diminui a fonte progressivamente
                                    while (textWidth > maxWidth && fontSize > 8)
                                    {
                                        fontSize -= 0.5f;
                                        textWidth = font.GetWidth(nomeAluno, fontSize);
                                    }

                                    if (fontSize != baseFontSize)
                                    {
                                        Console.WriteLine($"📏 Auto-ajuste aplicado: {baseFontSize}px → {fontSize}px");
                                        Console.WriteLine($"   Largura do texto: {textWidth}px / Máxima: {maxWidth}px");
                                    }

                                    // ⭐ CALCULA Y SOMENTE APÓS AJUSTE FINAL DA FONTE (CRÍTICO)
                                    // ⭐ AJUSTE: Subtrai 17px adicionais para posicionar mais abaixo
                                    float pdfY = pageSize.GetHeight() - y - fontSize - 17f;

                                    Console.WriteLine($"📐 Coordenadas finais aplicadas:");
                                    Console.WriteLine($"   - HTML Y (top): {y}px");
                                    Console.WriteLine($"   - Altura da página: {pageSize.GetHeight()}px");
                                    Console.WriteLine($"   - FontSize final: {fontSize}px");
                                    Console.WriteLine($"   - Ajuste adicional: -17px");
                                    Console.WriteLine($"   - PDF Y calculado: {pdfY}px");
                                    Console.WriteLine($"   - Fórmula: {pageSize.GetHeight()} - {y} - {fontSize} - 17 = {pdfY}");
                                    Console.WriteLine($"   - Posição X: {x}px");
                                    Console.WriteLine($"   - Negrito: {isBold}");
                                    Console.WriteLine($"   - Cor: {config.Color ?? "black"}");
                                    Console.WriteLine($"   - Alinhamento: {config.TextAlign ?? "center"}");

                                    // Parse da cor
                                    DeviceRgb color = ParseColor(config.Color ?? "black");
                                    Console.WriteLine($"🎨 Cor aplicada: {config.Color ?? "black"}");

                                    // Cria parágrafo com o nome do aluno
                                    Console.WriteLine($"✍️ Adicionando texto: '{nomeAluno}'");

                                    // ⭐ CORREÇÃO CRÍTICA: Usa largura da página inteira
                                    float paragraphWidth = pageSize.GetWidth() - (x * 2);

                                    var paragraph = new Paragraph(nomeAluno)
                                        .SetFont(font)
                                        .SetFontSize(fontSize)
                                        .SetFontColor(color)
                                        .SetFixedPosition(x, pdfY, paragraphWidth)
                                        .SetWidth(paragraphWidth)
                                        .SetMaxWidth(paragraphWidth);

                                    // ⭐ FORÇA TEXTO EM UMA ÚNICA LINHA (CRÍTICO)
                                    paragraph.SetProperty(iText.Layout.Properties.Property.NO_SOFT_WRAP_INLINE, true);

                                    // Alinhamento
                                    var alignment = (config.TextAlign ?? "center").ToLower();
                                    Console.WriteLine($"↔️ Alinhamento: {alignment}");

                                    switch (alignment)
                                    {
                                        case "center":
                                            paragraph.SetTextAlignment(TextAlignment.CENTER);
                                            break;
                                        case "right":
                                            paragraph.SetTextAlignment(TextAlignment.RIGHT);
                                            break;
                                        default:
                                            paragraph.SetTextAlignment(TextAlignment.LEFT);
                                            break;
                                    }

                                    document.Add(paragraph);
                                    Console.WriteLine($"✅ Texto adicionado ao documento com sucesso");
                                    Console.WriteLine($"   Posição final: X={x}, Y={pdfY}");
                                    Console.WriteLine($"   Tamanho: {fontSize}px");
                                    Console.WriteLine($"   Largura disponível: {paragraphWidth}px");
                                }

                                Console.WriteLine($"📦 Document fechado");
                            }

                            Console.WriteLine($"📦 PdfDocument fechado");
                        }

                        Console.WriteLine($"📦 Writer fechado");
                    }

                    Console.WriteLine($"📦 Reader fechado");
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                var resultBytes = outputStream.ToArray();

                Console.WriteLine($"✅ Certificado gerado com sucesso: {resultBytes.Length} bytes");

                return resultBytes;
            }
            catch (Exception ex)
            {
                // Log detalhado do erro
                Console.WriteLine($"❌ Erro ao processar PDF");
                Console.WriteLine($"   Tipo da Exception: {ex.GetType().FullName}");
                Console.WriteLine($"   Mensagem: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner Exception Tipo: {ex.InnerException.GetType().FullName}");
                    Console.WriteLine($"   Inner Exception Mensagem: {ex.InnerException.Message}");
                    Console.WriteLine($"   Inner StackTrace: {ex.InnerException.StackTrace}");
                }

                // Identifica tipo de erro baseado no tipo da exceção
                var exceptionTypeName = ex.GetType().FullName ?? "";

                if (exceptionTypeName.Contains("iText"))
                {
                    throw new Exception($"Erro do iText7 ao processar PDF: {ex.Message}. Possíveis causas: PDF protegido por senha, corrompido ou com estrutura inválida.", ex);
                }
                else if (ex is InvalidOperationException)
                {
                    throw new Exception($"Operação inválida ao processar PDF: {ex.Message}", ex);
                }
                else if (ex is ArgumentException)
                {
                    throw new Exception($"Argumento inválido ao processar PDF: {ex.Message}", ex);
                }
                else
                {
                    throw new Exception($"Erro inesperado ao gerar certificado: {ex.Message}", ex);
                }
            }
            finally
            {
                // Garante limpeza do stream se necessário
                if (outputStream?.Length == 0)
                {
                    outputStream?.Dispose();
                }
            }
        }

        /// <summary>
        /// 🆕 Gera múltiplos certificados para um único aluno (Trilha/Curso Completo)
        /// </summary>
        /// <param name="certificateIds">IDs dos certificados (módulos) selecionados</param>
        /// <param name="nomeAluno">Nome do aluno para preencher em todos os certificados</param>
        /// <returns>ZIP contendo todos os certificados gerados</returns>
        public async Task<MemoryStream> GerarTrilhaCertificadosAsync(List<int> certificateIds, string nomeAluno)
        {
            if (certificateIds == null || !certificateIds.Any())
                throw new ArgumentException("Nenhum certificado selecionado.");

            if (string.IsNullOrWhiteSpace(nomeAluno))
                throw new ArgumentException("Nome do aluno é obrigatório.");

            Console.WriteLine($"🎓 Iniciando geração de trilha de certificados para: {nomeAluno}");
            Console.WriteLine($"📚 Total de certificados selecionados: {certificateIds.Count}");

            var outputStream = new MemoryStream();

            using (var archive = new System.IO.Compression.ZipArchive(outputStream,
                System.IO.Compression.ZipArchiveMode.Create, true))
            {
                int totalProcessados = 0;
                int totalSucesso = 0;
                int totalErros = 0;
                var certificadosGerados = new List<string>();
                var errosDetalhados = new List<string>();

                // Busca os certificados do banco
                var todosCertificados = await _repository.GetAllAsync();

                foreach (var certId in certificateIds)
                {
                    totalProcessados++;
                    var certificado = todosCertificados.FirstOrDefault(c => c.Id == certId);

                    if (certificado == null)
                    {
                        totalErros++;
                        var erro = $"Certificado ID {certId} não encontrado no banco de dados";
                        Console.WriteLine($"  ❌ {erro}");
                        errosDetalhados.Add(erro);
                        continue;
                    }

                    try
                    {
                        Console.WriteLine($"  ➡️ [{totalProcessados}/{certificateIds.Count}] Gerando: {certificado.NomeCurso}");

                        // Gera o certificado usando o método existente
                        var pdfBytes = await CertificarAlunoAsync(certificado.NomeCurso, nomeAluno.Trim());

                        // Sanitiza o nome do arquivo
                        var safeFileName = string.Concat(certificado.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));
                        var entryName = $"{safeFileName}.pdf";

                        // Adiciona ao ZIP
                        var entry = archive.CreateEntry(entryName);
                        await using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pdfBytes);

                        totalSucesso++;
                        certificadosGerados.Add(certificado.NomeCurso);
                        Console.WriteLine($"  ✅ Certificado gerado: {entryName}");
                    }
                    catch (Exception ex)
                    {
                        totalErros++;
                        var erro = $"Erro ao gerar '{certificado.NomeCurso}': {ex.Message}";
                        Console.WriteLine($"  ❌ {erro}");
                        errosDetalhados.Add(erro);

                        // Adiciona arquivo de erro no ZIP
                        try
                        {
                            var safeFileName = string.Concat(certificado.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));
                            var errorEntry = archive.CreateEntry($"_ERROS/{safeFileName}_erro.txt");
                            await using var errorStream = errorEntry.Open();
                            await using var writer = new StreamWriter(errorStream);
                            await writer.WriteAsync($"Erro ao gerar certificado '{certificado.NomeCurso}':\n\n{ex.Message}\n\n{ex.StackTrace}");
                        }
                        catch { /* Ignora erro ao criar log */ }
                    }
                }

                // ⭐ Adiciona arquivo de resumo
                var summaryEntry = archive.CreateEntry("_RESUMO.txt");
                await using (var summaryStream = summaryEntry.Open())
                await using (var writer = new StreamWriter(summaryStream))
                {
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                    await writer.WriteLineAsync($"  TRILHA DE CERTIFICADOS - RESUMO DA GERAÇÃO");
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"🎓 Aluno: {nomeAluno}");
                    await writer.WriteLineAsync($"📅 Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"📊 ESTATÍSTICAS:");
                    await writer.WriteLineAsync($"   ✅ Total de certificados processados: {totalProcessados}");
                    await writer.WriteLineAsync($"   ✅ Certificados gerados com sucesso: {totalSucesso}");
                    await writer.WriteLineAsync($"   ❌ Erros: {totalErros}");
                    await writer.WriteLineAsync($"");

                    if (certificadosGerados.Any())
                    {
                        await writer.WriteLineAsync($"✅ CERTIFICADOS GERADOS:");
                        foreach (var cert in certificadosGerados)
                        {
                            await writer.WriteLineAsync($"   • {cert}");
                        }
                        await writer.WriteLineAsync($"");
                    }

                    if (errosDetalhados.Any())
                    {
                        await writer.WriteLineAsync($"❌ ERROS ENCONTRADOS:");
                        foreach (var erro in errosDetalhados)
                        {
                            await writer.WriteLineAsync($"   • {erro}");
                        }
                    }

                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                }

                Console.WriteLine($"");
                Console.WriteLine($"✅ Trilha de certificados concluída:");
                Console.WriteLine($"   📚 Total: {totalProcessados}");
                Console.WriteLine($"   ✅ Sucesso: {totalSucesso}");
                Console.WriteLine($"   ❌ Erros: {totalErros}");
            }

            outputStream.Seek(0, SeekOrigin.Begin);
            return outputStream;
        }

        /// <summary>
        /// Converte string de cor para DeviceRgb
        /// </summary>
        private DeviceRgb ParseColor(string colorString)
        {
            if (string.IsNullOrWhiteSpace(colorString))
                return new DeviceRgb(0, 0, 0);

            colorString = colorString.Trim().ToLower();

            // Cores nomeadas básicas
            if (colorString == "black") return new DeviceRgb(0, 0, 0);
            if (colorString == "white") return new DeviceRgb(255, 255, 255);
            if (colorString == "red") return new DeviceRgb(255, 0, 0);
            if (colorString == "green") return new DeviceRgb(0, 255, 0);
            if (colorString == "blue") return new DeviceRgb(0, 0, 255);

            // Formato hexadecimal (#RRGGBB)
            if (colorString.StartsWith("#"))
            {
                colorString = colorString.TrimStart('#');

                if (colorString.Length == 6)
                {
                    int r = Convert.ToInt32(colorString.Substring(0, 2), 16);
                    int g = Convert.ToInt32(colorString.Substring(2, 2), 16);
                    int b = Convert.ToInt32(colorString.Substring(4, 2), 16);

                    return new DeviceRgb(r, g, b);
                }
            }

            return new DeviceRgb(0, 0, 0);
        }

        // Config auxiliar
        public class NomeAlunoConfig
        {
            public string Top { get; set; } = "0";
            public string Left { get; set; } = "0";
            public int Width { get; set; } = 400;
            public float Height { get; set; } = 16;
            public string FontFamily { get; set; } = "Arial";
            public string FontSize { get; set; } = "16px";
            public string BaseFontSize { get; set; } = "16px";
            public string Color { get; set; } = "black";
            public string FontWeight { get; set; } = "regular";
            public string TextAlign { get; set; } = "center";
        }

        public class NumberOrStringToStringConverter : System.Text.Json.Serialization.JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble().ToString();
                if (reader.TokenType == JsonTokenType.String) return reader.GetString() ?? string.Empty;
                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
    }
}