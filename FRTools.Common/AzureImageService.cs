using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace FRTools.Common
{
    public class AzureImageService
    {
        public async Task DeleteImage(string path)
        {
            var container = GetStorageContainer(path);

            var fileName = Path.GetFileName(path);
            var reference = container.GetBlobClient(fileName);
            if (reference.Exists())
            {
                await reference.DeleteAsync();
            }
        }

        public async Task<Stream> GetImage(string path, bool cache = true)
        {
            var resultStream = new MemoryStream();

            var directory = GetStorageContainer(path);

            var fileName = Path.GetFileName(path);

            var reference = directory.GetBlobClient(fileName);

            if (reference.Exists())
            {
                await reference.DownloadToAsync(resultStream);
                resultStream.Position = 0;
                return resultStream;
            }
            throw new FileNotFoundException($"'{path}' was not found");
        }

        public async Task<string> WriteImage(string path, Stream stream)
        {
            var directory = GetStorageContainer(path);

            var fileName = Path.GetFileName(path);
            var reference = directory.GetBlobClient(fileName);

            await reference.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = MimeMapping.GetMimeMapping(fileName) }, Conditions = null });

            return reference.Uri.AbsolutePath;
        }

        public bool Exists(string path, out string url)
        {
            var directory = GetStorageContainer(path);

            var fileName = Path.GetFileName(path);
            var reference = directory.GetBlobClient(fileName);
            url = reference.Exists() ? reference.Uri.AbsolutePath : null;
            return url != null;
        }

        private BlobContainerClient GetStorageContainer(string path)
        {
            var credentials = ConfigurationManager.AppSettings["AzureCredentials"];

            var client = new BlobServiceClient(credentials);

            var directoryPath = Path.GetDirectoryName(string.Join(Path.DirectorySeparatorChar.ToString(), path.Split(Path.DirectorySeparatorChar).Skip(1)));

            return client.GetBlobContainerClient(path);
        }
    }
}
