using System.IO;
using System.Threading.Tasks;

namespace MinioBackupManager
{
    public interface IMinioService
    {
        /// <summary>
        /// Uploads a stream to minio
        /// </summary>
        /// <param name="clientSettings">settings for the minio client</param>
        /// <param name="bucketName">the bucket under which to save the datastream</param>
        /// <param name="fileGuid">the guid under which to save the object</param>
        /// <param name="dataStream">the object to save</param>
        /// <returns>the minio id on which the item can be retrieved</returns>        
        Task UploadFileAsync(MinioSettings clientSettings, string bucketName, string fileGuid, Stream dataStream);

        Stream DownloadFile(MinioSettings clientSettings, string bucketName, string fileGuid);
    }
}