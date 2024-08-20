using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace WSR.CIMS.Implementation
{
    public class QuotationDocumentBlobHelper
    {
        private readonly IConfiguration _configuration;

        public QuotationDocumentBlobHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<byte[]> GetBlobAsByteArrayAsync(string containerName, string blobPath)
        {
            var client = GetBlobClient();
            var container = client.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(blobPath);
            var exists = await blob.ExistsAsync();

            if (!exists) return null;

            using var stream = new MemoryStream();
            await blob.DownloadToStreamAsync(stream);

            return stream.GetBuffer();
        }

        public async Task<string> UploadBlobAsync(string containerName, string blobPath, byte[] blobContent)
        {
            var client = GetBlobClient();
            var container = client.GetContainerReference(containerName);
            await container.CreateIfNotExistsAsync();

            var blob = container.GetBlockBlobReference(blobPath);
            using var ms = new MemoryStream(blobContent);
            await blob.UploadFromStreamAsync(ms);

            return blobPath;
        }

        private CloudBlobClient GetBlobClient()
        {
            string accountName = GetAzureConfiguration("AccountName");
            string accountKey = GetAzureConfiguration("AccountKey");

            var cred = new StorageCredentials(accountName, accountKey);
            var account = new CloudStorageAccount(cred, true);
            return account.CreateCloudBlobClient();
        }

        private string GetAzureConfiguration(string key)
        {
            return _configuration.GetValue<string>($"AzureStorage:{key}");
        }
    }
}
