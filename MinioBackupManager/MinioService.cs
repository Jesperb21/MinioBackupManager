using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace MinioBackupManager
{
    public static class MinioService
    {
        /// <summary>
        ///     Uploads a stream to minio
        /// </summary>
        /// <param name="client"></param>
        /// <param name="bucketName">the bucket under which to save the datastream</param>
        /// <param name="fileGuid"></param>
        /// <param name="dataStream">the object to save</param>
        /// <returns>the minio id on which the item can be retrieved</returns>
        public static async Task UploadFileAsync(this AmazonS3Client client, string bucketName, string fileGuid,
            Stream dataStream)
        {
            //var client = GetClient();

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

        public static Stream DownloadFileAsync(this AmazonS3Client client, string bucketName, string fileGuid)
        {
            //var client = GetClient();
            var obj = client.GetObjectAsync(bucketName, fileGuid).Result;
            return obj.ResponseStream;
        }


        public static AmazonS3Client GetClient(string accessKey, string secretAccesKey, string endpoint)
        {
            AWSCredentials creds = new BasicAWSCredentials(accessKey, secretAccesKey);

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.EUWest1,
                SignatureVersion = "v4",
                ForcePathStyle = true, //required for minio
                ServiceURL = endpoint
            };

            var client = new AmazonS3Client(creds, config);

            return client;
        }
    }
}