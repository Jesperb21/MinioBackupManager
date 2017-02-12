using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace MinioBackupManager
{
    public class MinioService : IMinioService
    {

        /// <summary>
        ///     Uploads a stream to minio
        /// </summary>
        /// <param name="clientSettings"></param>
        /// <param name="bucketName">the bucket under which to save the datastream</param>
        /// <param name="fileGuid"></param>
        /// <param name="dataStream">the object to save</param>
        /// <returns>the minio id on which the item can be retrieved</returns>
        public async Task UploadFileAsync(MinioSettings clientSettings, string bucketName, string fileGuid,
            Stream dataStream)
        {
            var client = GetClient(clientSettings);

            var bucketExist = await AmazonS3Util.DoesS3BucketExistAsync(client, bucketName);

            if (!bucketExist)
                await client.PutBucketAsync(bucketName);


            var putResult = await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucketName,
                Key = fileGuid,
                InputStream = dataStream
            });

            var code = putResult.HttpStatusCode;

            if (code == HttpStatusCode.OK)
                return;

            throw new AmazonS3Exception("Upload Error");
        }

        public Stream DownloadFile(MinioSettings clientSettings, string bucketName, string fileGuid)
        {
            var client = GetClient(clientSettings);
            var obj = client.GetObjectAsync(bucketName, fileGuid).Result;
            return obj.ResponseStream;
        }


        private static AmazonS3Client GetClient(MinioSettings clientSettings)
        {
            AWSCredentials creds = new BasicAWSCredentials(clientSettings.AccessKey, clientSettings.SecretKey);

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.EUWest1,
                SignatureVersion = "v4",
                ForcePathStyle = true, //required for minio
                ServiceURL = clientSettings.Endpoint
            };

            var client = new AmazonS3Client(creds, config);

            return client;
        }
    }
}