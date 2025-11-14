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

        /// <summary>
        /// ⭐ NOVO: Remove margens de um PDF e retorna os bytes processados
        /// </summary>
        private async Task<byte[]> RemoverMargensPdfAsync(byte[] pdfBytes)
        {
            var outputStream = new MemoryStream();

            try
            {
                using (var inputStream = new MemoryStream(pdfBytes))
                {
                    using (var reader = new PdfReader(inputStream))
                    {
                        using (var writer = new PdfWriter(outputStream))
                        {
                            writer.SetCloseStream(false);

                            using (var pdfDoc = new PdfDocument(reader, writer))
                            {
                                // Processa todas as páginas
                                int numPages = pdfDoc.GetNumberOfPages();
                                Console.WriteLine($"🔧 Processando {numPages} página(s) para remover margens...");

                                for (int i = 1; i <= numPages; i++)
                                {
                                    var page = pdfDoc.GetPage(i);
                                    var mediaBox = page.GetMediaBox();

                                    // ⭐ Remove crop box, trim box, bleed box e art box que podem causar margens
                                    page.SetCropBox(mediaBox);
                                    page.SetTrimBox(mediaBox);
                                    page.SetBleedBox(mediaBox);
                                    page.SetArtBox(mediaBox);

                                    Console.WriteLine($"   Página {i}: MediaBox ajustado para {mediaBox.GetWidth()}x{mediaBox.GetHeight()}");
                                }
                            }
                        }
                    }
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                var result = outputStream.ToArray();
                Console.WriteLine($"✅ Margens removidas com sucesso. Tamanho final: {result.Length} bytes");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao remover margens: {ex.Message}");
                // Retorna o PDF original se falhar
                return pdfBytes;
            }
        }

        public async Task<(bool Success, string[] Errors)> CreateAsync(CertificateDto dto, IFormFile? certificadoVazioFile = null, IFormFile? logoFile = null, IFormFile? assinaturaFile = null)
        {
            ValidationResult result = await _validator.ValidateAsync(dto);
            if (!result.IsValid)
                return (false, result.Errors.Select(e => e.ErrorMessage).ToArray());

            var safeFileNameBase = string.Concat(dto.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));

            // ⭐ Salva PDF no Cloud Storage - VERSÃO CORRIGIDA COM REMOÇÃO DE MARGENS
            if (!string.IsNullOrEmpty(dto.CertificadoGeradoBase64))
            {
                try
                {
                    var base64Data = dto.CertificadoGeradoBase64.Contains(',')
                        ? dto.CertificadoGeradoBase64.Split(',')[1]
                        : dto.CertificadoGeradoBase64;

                    var bytes = Convert.FromBase64String(base64Data);
                    Console.WriteLine($"📥 PDF recebido: {bytes.Length} bytes");

                    // ⭐ NOVO: Remove margens do PDF antes de salvar
                    Console.WriteLine($"🔧 Processando PDF para remover margens...");
                    bytes = await RemoverMargensPdfAsync(bytes);

                    // Upload para Cloud Storage
                    var key = $"certificados/{safeFileNameBase}/{safeFileNameBase}.pdf";
                    var url = await _cloudStorage.UploadFileAsync(key, bytes, "application/pdf");

                    dto.CertificadoVazio = url; // Salva URL pública

                    Console.WriteLine($"✅ PDF salvo no Cloud (sem margens): {url}");
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
                    trilha.CertificadosIdsList.Remove(id);

                    if (!trilha.CertificadosIdsList.Any())
                    {
                        trilha.Ativa = false;
                        Console.WriteLine($"  ❌ Trilha '{trilha.Nome}' desativada (sem certificados)");
                    }
                    else
                    {
                        Console.WriteLine($"  ⚠️ Certificado removido da trilha '{trilha.Nome}' ({trilha.CertificadosIdsList.Count} restantes)");
                    }

                    trilha.DataAtualizacao = DateTime.UtcNow;
                    await _trilhaRepository.UpdateAsync(trilha);
                }
            }

            // ⭐ Deletar arquivos do Cloud Storage
            if (!string.IsNullOrEmpty(certificate.CertificadoVazio))
            {
                if (certificate.CertificadoVazio.StartsWith("https://pub-") ||
                    certificate.CertificadoVazio.StartsWith("http://") ||
                    certificate.CertificadoVazio.Contains(".r2.dev"))
                {
                    try
                    {
                        var uri = new Uri(certificate.CertificadoVazio);
                        var key = uri.AbsolutePath.TrimStart('/');

                        Console.WriteLine($"🗑️ Deletando do Cloud: {key}");
                        await _cloudStorage.DeleteFileAsync(key);

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

        public async Task<byte[]> CertificarAlunoAsync(string nomeCurso, string nomeAluno)
        {
            Console.WriteLine($"🔵 Gerando certificado para: {nomeAluno} | Curso: {nomeCurso}");

            var safeFileNameBase = string.Concat(nomeCurso.Split(IOPath.GetInvalidFileNameChars()));

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
                throw new FileNotFoundException($"Template não encontrado: {pdfKey}", ex);
            }

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

                Console.WriteLine($"✅ Config carregada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Config não encontrada, usando padrão: {ex.Message}");
            }

            float maxWidth = config.Width > 0 ? config.Width : 400f;

            float baseFontSize = float.TryParse(
                (config.BaseFontSize ?? config.FontSize ?? "24px").Replace("px", "").Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var bfs) ? bfs : 24f;

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

            var outputStream = new MemoryStream();

            try
            {
                Console.WriteLine($"🔧 Iniciando geração do PDF...");

                byte[] workingBytes = new byte[templateBytes.Length];
                Array.Copy(templateBytes, workingBytes, templateBytes.Length);

                using (var templateStream = new MemoryStream(workingBytes))
                {
                    var readerProperties = new ReaderProperties();

                    using (var reader = new PdfReader(templateStream, readerProperties))
                    {
                        using (var writer = new PdfWriter(outputStream))
                        {
                            writer.SetCloseStream(false);

                            using (var pdfDoc = new PdfDocument(reader, writer))
                            {
                                var page = pdfDoc.GetFirstPage();
                                if (page == null)
                                    throw new Exception("PDF sem páginas válidas");

                                var pageSize = page.GetPageSize();
                                Console.WriteLine($"📄 Tamanho: {pageSize.GetWidth()}x{pageSize.GetHeight()}");

                                using (var document = new Document(pdfDoc))
                                {
                                    // ⭐ MARGENS ZERADAS
                                    document.SetMargins(0, 0, 0, 0);
                                    document.SetTopMargin(0);
                                    document.SetRightMargin(0);
                                    document.SetBottomMargin(0);
                                    document.SetLeftMargin(0);

                                    PdfFont font = isBold
                                        ? PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)
                                        : PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                                    float fontSize = baseFontSize;
                                    float textWidth = font.GetWidth(nomeAluno, fontSize);

                                    while (textWidth > maxWidth && fontSize > 8)
                                    {
                                        fontSize -= 0.5f;
                                        textWidth = font.GetWidth(nomeAluno, fontSize);
                                    }

                                    float pdfY = pageSize.GetHeight() - y - fontSize - 17f;

                                    DeviceRgb color = ParseColor(config.Color ?? "black");

                                    float paragraphWidth = config.Width > 0 ? config.Width : 400f;

                                    var paragraph = new Paragraph(nomeAluno)
                                        .SetFont(font)
                                        .SetFontSize(fontSize)
                                        .SetFontColor(color)
                                        .SetFixedPosition(x, pdfY, paragraphWidth)
                                        .SetWidth(paragraphWidth)
                                        .SetMaxWidth(paragraphWidth)
                                        .SetMargin(0)
                                        .SetPadding(0);

                                    paragraph.SetProperty(Property.NO_SOFT_WRAP_INLINE, true);

                                    // ⭐ Aplica alinhamento DENTRO da largura configurada
                                    var alignment = (config.TextAlign ?? "center").ToLower();
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
                                    Console.WriteLine($"✅ Texto adicionado: X={x}, Y={pdfY}, Width={paragraphWidth}, Size={fontSize}px, Align={alignment}");

                                    // ⭐ ADICIONAR DATA ATUAL (se configurada)
                                    if (config.DataEmissao != null && config.DataEmissao.Width > 0)
                                    {
                                        // Formata a data atual no formato brasileiro
                                        string dataFormatada;
                                        if (!string.IsNullOrEmpty(config.DataEmissao.DateFormat))
                                        {
                                            dataFormatada = DateTime.UtcNow.AddHours(-3).ToString(config.DataEmissao.DateFormat);
                                        }
                                        else
                                        {
                                            dataFormatada = DateTime.UtcNow.AddHours(-3).ToString("dd/MM/yyyy");
                                        }

                                        float dataX = float.TryParse(
                                            (config.DataEmissao.Left ?? "0").Replace("px", "").Replace(",", "."),
                                            System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out var dx) ? dx : 50f;

                                        float dataY = float.TryParse(
                                            (config.DataEmissao.Top ?? "0").Replace("px", "").Replace(",", "."),
                                            System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out var dy) ? dy : 100f;

                                        float dataFontSize = float.TryParse(
                                            (config.DataEmissao.FontSize ?? "12px").Replace("px", "").Replace(",", "."),
                                            System.Globalization.NumberStyles.Any,
                                            System.Globalization.CultureInfo.InvariantCulture,
                                            out var dfs) ? dfs : 12f;

                                        bool dataIsBold = (config.DataEmissao.FontWeight ?? "regular").ToLower() == "bold";

                                        PdfFont dataFont = dataIsBold
                                            ? PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)
                                            : PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                                        float dataPdfY = pageSize.GetHeight() - dataY - dataFontSize - 17f;

                                        DeviceRgb dataColor = ParseColor(config.DataEmissao.Color ?? "black");

                                        var dataParagraph = new Paragraph(dataFormatada)
                                            .SetFont(dataFont)
                                            .SetFontSize(dataFontSize)
                                            .SetFontColor(dataColor)
                                            .SetFixedPosition(dataX, dataPdfY, config.DataEmissao.Width)
                                            .SetWidth(config.DataEmissao.Width)
                                            .SetMaxWidth(config.DataEmissao.Width)
                                            .SetMargin(0)
                                            .SetPadding(0);

                                        dataParagraph.SetProperty(Property.NO_SOFT_WRAP_INLINE, true);

                                        var dataAlignment = (config.DataEmissao.TextAlign ?? "center").ToLower();
                                        switch (dataAlignment)
                                        {
                                            case "center":
                                                dataParagraph.SetTextAlignment(TextAlignment.CENTER);
                                                break;
                                            case "right":
                                                dataParagraph.SetTextAlignment(TextAlignment.RIGHT);
                                                break;
                                            default:
                                                dataParagraph.SetTextAlignment(TextAlignment.LEFT);
                                                break;
                                        }

                                        document.Add(dataParagraph);
                                        Console.WriteLine($"✅ Data adicionada: {dataFormatada} | X={dataX}, Y={dataPdfY}, Size={dataFontSize}px");
                                    }
                                }
                            }
                        }
                    }
                }

                outputStream.Seek(0, SeekOrigin.Begin);
                var resultBytes = outputStream.ToArray();

                Console.WriteLine($"✅ Certificado gerado: {resultBytes.Length} bytes");

                return resultBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro: {ex.Message}");
                throw new Exception($"Erro ao gerar certificado: {ex.Message}", ex);
            }
        }

        public async Task<MemoryStream> GerarTrilhaCertificadosAsync(List<int> certificateIds, string nomeAluno)
        {
            if (certificateIds == null || !certificateIds.Any())
                throw new ArgumentException("Nenhum certificado selecionado.");

            if (string.IsNullOrWhiteSpace(nomeAluno))
                throw new ArgumentException("Nome do aluno é obrigatório.");

            Console.WriteLine($"🎓 Trilha para: {nomeAluno}");
            Console.WriteLine($"📚 Total: {certificateIds.Count}");

            var outputStream = new MemoryStream();

            using (var archive = new System.IO.Compression.ZipArchive(outputStream,
                System.IO.Compression.ZipArchiveMode.Create, true))
            {
                int totalProcessados = 0;
                int totalSucesso = 0;
                int totalErros = 0;
                var certificadosGerados = new List<string>();
                var errosDetalhados = new List<string>();

                var todosCertificados = await _repository.GetAllAsync();

                foreach (var certId in certificateIds)
                {
                    totalProcessados++;
                    var certificado = todosCertificados.FirstOrDefault(c => c.Id == certId);

                    if (certificado == null)
                    {
                        totalErros++;
                        var erro = $"Certificado ID {certId} não encontrado";
                        Console.WriteLine($"  ❌ {erro}");
                        errosDetalhados.Add(erro);
                        continue;
                    }

                    try
                    {
                        Console.WriteLine($"  ➡️ [{totalProcessados}/{certificateIds.Count}] {certificado.NomeCurso}");

                        var pdfBytes = await CertificarAlunoAsync(certificado.NomeCurso, nomeAluno.Trim());

                        var safeFileName = string.Concat(certificado.NomeCurso.Split(IOPath.GetInvalidFileNameChars()));
                        var entryName = $"{safeFileName}.pdf";

                        var entry = archive.CreateEntry(entryName);
                        await using var entryStream = entry.Open();
                        await entryStream.WriteAsync(pdfBytes);

                        totalSucesso++;
                        certificadosGerados.Add(certificado.NomeCurso);
                        Console.WriteLine($"  ✅ {entryName}");
                    }
                    catch (Exception ex)
                    {
                        totalErros++;
                        var erro = $"Erro '{certificado.NomeCurso}': {ex.Message}";
                        Console.WriteLine($"  ❌ {erro}");
                        errosDetalhados.Add(erro);
                    }
                }

                var summaryEntry = archive.CreateEntry("_RESUMO.txt");
                await using (var summaryStream = summaryEntry.Open())
                await using (var writer = new StreamWriter(summaryStream))
                {
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                    await writer.WriteLineAsync($"  TRILHA DE CERTIFICADOS - RESUMO");
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"🎓 Aluno: {nomeAluno}");
                    await writer.WriteLineAsync($"📅 Data: {DateTime.UtcNow:dd/MM/yyyy HH:mm:ss}");
                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"📊 ESTATÍSTICAS:");
                    await writer.WriteLineAsync($"   ✅ Processados: {totalProcessados}");
                    await writer.WriteLineAsync($"   ✅ Sucesso: {totalSucesso}");
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
                        await writer.WriteLineAsync($"❌ ERROS:");
                        foreach (var erro in errosDetalhados)
                        {
                            await writer.WriteLineAsync($"   • {erro}");
                        }
                    }

                    await writer.WriteLineAsync($"");
                    await writer.WriteLineAsync($"═══════════════════════════════════════════════════");
                }

                Console.WriteLine($"");
                Console.WriteLine($"✅ Concluído: {totalSucesso}/{totalProcessados}");
            }

            outputStream.Seek(0, SeekOrigin.Begin);
            return outputStream;
        }

        private DeviceRgb ParseColor(string colorString)
        {
            if (string.IsNullOrWhiteSpace(colorString))
                return new DeviceRgb(0, 0, 0);

            colorString = colorString.Trim().ToLower();

            if (colorString == "black") return new DeviceRgb(0, 0, 0);
            if (colorString == "white") return new DeviceRgb(255, 255, 255);
            if (colorString == "red") return new DeviceRgb(255, 0, 0);
            if (colorString == "green") return new DeviceRgb(0, 255, 0);
            if (colorString == "blue") return new DeviceRgb(0, 0, 255);

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

        // Adicione esta classe dentro de NomeAlunoConfig
        public class DataEmissaoFieldConfig
        {
            public string Top { get; set; } = "0";
            public string Left { get; set; } = "0";
            public int Width { get; set; } = 200;
            public string FontFamily { get; set; } = "Arial";
            public string FontSize { get; set; } = "12px";
            public string Color { get; set; } = "black";
            public string FontWeight { get; set; } = "regular";
            public string TextAlign { get; set; } = "center";
            public string DateFormat { get; set; } = "dd/MM/yyyy"; // Formato da data
        }

        // Modifique a classe NomeAlunoConfig para incluir:
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

            // ⭐ NOVO: Configuração da data
            public DataEmissaoFieldConfig DataEmissao { get; set; } = new DataEmissaoFieldConfig();
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