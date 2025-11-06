using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AuthDemo.Services
{
    public class CloudStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public CloudStorageService(IConfiguration config)
        {
            // Tenta ler do appsettings primeiro, senão pega do ambiente
            var accessKey = config["CloudflareR2:AccessKey"]
                            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_R2_ACCESS_KEY");
            var secretKey = config["CloudflareR2:SecretKey"]
                            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_R2_SECRET_KEY");
            var accountId = config["CloudflareR2:AccountId"]
                            ?? Environment.GetEnvironmentVariable("CLOUDFLARE_R2_ACCOUNT_ID");
            _bucketName = config["CloudflareR2:BucketName"]
                          ?? Environment.GetEnvironmentVariable("CLOUDFLARE_R2_BUCKET_NAME");

            // Validação
            if (string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(secretKey) ||
                string.IsNullOrWhiteSpace(accountId) ||
                string.IsNullOrWhiteSpace(_bucketName))
            {
                throw new InvalidOperationException(
                    "Credenciais do Cloudflare R2 não configuradas corretamente.");
            }

            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var s3Config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                SignatureVersion = "4"
            };
            _s3Client = new AmazonS3Client(credentials, s3Config);
        }

        public async Task<string> UploadFileAsync(string key, byte[] fileBytes, string contentType = "application/pdf")
        {
            using var stream = new MemoryStream(fileBytes);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                UseChunkEncoding = false // Desabilita chunked encoding para R2
            };

            await _s3Client.PutObjectAsync(request);

            // Retorna URL pública
            return $"https://pub-{_bucketName}.r2.dev/{key}";
        }

        public async Task<byte[]> DownloadFileAsync(string key)
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var stream = response.ResponseStream;
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        public async Task DeleteFileAsync(string key)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request);
        }

        public async Task<bool> FileExistsAsync(string key)
        {
            try
            {
                var request = new GetObjectMetadataRequest
                {
                    BucketName = _bucketName,
                    Key = key
                };
                await _s3Client.GetObjectMetadataAsync(request);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<List<string>> ListFilesAsync(string prefix = "")
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            return response.S3Objects.Select(x => x.Key).ToList();
        }
    }
}