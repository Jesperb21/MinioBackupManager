using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MinioBackupManager;
using Xunit;

namespace XUnitTests
{
    [Collection("minio tests")]
    public class MinioServiceTests : IDisposable
    {
        private MinioSettings MinioSettings { get; }
        private MinioSettings BackupMinioSettings { get; }
        private IMinioService MinioService { get; }


        /// <summary>
        /// Makes a new Minio client & a new BackupMinio client before every test
        /// </summary>
        public MinioServiceTests()
        {
            #region get minio settings from env

            var srcName = Environment.GetEnvironmentVariable("MINIO_SRC_NAME");
            var srcPort = Environment.GetEnvironmentVariable("MINIO_SRC_PORT");
            var srcAccessKey = Environment.GetEnvironmentVariable("MINIO_SRC_ACCESS_KEY");
            var srcSecretKey = Environment.GetEnvironmentVariable("MINIO_SRC_SECRET_KEY");

            MinioSettings = new MinioSettings() { Endpoint = $"http://{srcName}:{srcPort}", AccessKey = srcAccessKey, SecretKey = srcSecretKey };

            var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
            var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
            var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
            var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

            BackupMinioSettings = new MinioSettings() { Endpoint = $"http://{dstName}:{dstPort}", AccessKey = dstAccessKey, SecretKey = dstSecretKey };

            #endregion

            MinioService = new MinioService();

            //remove all buckets before tests to avoid the default bucket "Docker" on the minio docker image interferes with tests
            NukeBuckets(MinioSettings);
            NukeBuckets(BackupMinioSettings);
        }


        #region nuke

        [Fact]
        public void Minio_NukeBuckets_ShouldRemoveAllBuckets()
        {
            //arrange
            var tasks = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                var bucketName = Guid.NewGuid().ToString();
                tasks.Add(Task.Run(() => GetClient(MinioSettings).PutBucketAsync(bucketName)));
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(10, GetClient(MinioSettings).ListBucketsAsync().Result.Buckets.Count);

            //act
            NukeBuckets(MinioSettings);

            //assert
            Assert.Equal(0, GetClient(MinioSettings).ListBucketsAsync().Result.Buckets.Count);

        }
        [Fact]
        public void BackupMinio_NukeBuckets_ShouldRemoveAllBuckets()
        {
            //arrange
            var tasks = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                var bucketName = Guid.NewGuid().ToString();
                tasks.Add(Task.Run(() => GetClient(BackupMinioSettings).PutBucketAsync(bucketName)));
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(10, GetClient(BackupMinioSettings).ListBucketsAsync().Result.Buckets.Count);

            //act
            NukeBuckets(BackupMinioSettings);

            //assert
            Assert.Equal(0, GetClient(BackupMinioSettings).ListBucketsAsync().Result.Buckets.Count);

        }

        #endregion

        #region no buckets

        [Fact]
        public void Minio_WithoutDoingAnything_ShouldntHaveAnyBuckets()
        {
            var countOfBuckets = GetClient(MinioSettings).ListBucketsAsync().Result.Buckets.Count;
            Assert.Equal(0, countOfBuckets);
        }

        [Fact]
        public void BackupMinio_WithoutDoingAnything_ShouldntHaveAnyBuckets()
        {
            var countOfBuckets = GetClient(BackupMinioSettings).ListBucketsAsync().Result.Buckets.Count;
            Assert.Equal(0, countOfBuckets);
        }

        #endregion

        #region uploading 1 files

        [Fact]
        public void Minio_UploadingFirstFile_ShouldntGiveAnyErrors()
        {
            //arrange
            var obj = Encoding.UTF8.GetBytes("SomeString");
            var memStream = new MemoryStream(obj);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(MinioSettings, bucketName, fileGuid, memStream)));
            //assert

        }

        [Fact]
        public void BackupMinio_UploadingFirstFile_ShouldntGiveAnyErrors()
        {
            //arrange
            var obj = Encoding.UTF8.GetBytes("SomeString");
            var memStream = new MemoryStream(obj);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(BackupMinioSettings, bucketName, fileGuid, memStream)));
            //assert

        }

        #endregion

        #region uploading 2 files

        [Fact]
        public void Minio_UploadingTwoFilesToTheSameBucket_ActuallyUploadsTwoFilesToTheBucket()
        {
            //arrange
            var obj1 = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var obj2 = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var memStream1 = new MemoryStream(obj1);
            var memStream2 = new MemoryStream(obj2);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid1 = Guid.NewGuid().ToString();
            var fileGuid2 = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(MinioSettings, bucketName, fileGuid1, memStream1)));
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(MinioSettings, bucketName, fileGuid2, memStream2)));


            //assert
            Assert.Equal(2, GetClient(MinioSettings).ListObjectsAsync(bucketName).Result.S3Objects.Count);
        }

        [Fact]
        public void BackupMinio_UploadingTwoFilesToTheSameBucket_ActuallyUploadsTwoFilesToTheBucket()
        {
            //arrange
            var obj1 = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var obj2 = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var memStream1 = new MemoryStream(obj1);
            var memStream2 = new MemoryStream(obj2);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid1 = Guid.NewGuid().ToString();
            var fileGuid2 = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(BackupMinioSettings, bucketName, fileGuid1, memStream1)));
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(BackupMinioSettings, bucketName, fileGuid2, memStream2)));


            //assert
            Assert.Equal(2, GetClient(BackupMinioSettings).ListObjectsAsync(bucketName).Result.S3Objects.Count);
        }

        #endregion

        #region downloading files

        [Fact]
        public void Minio_AfterUploading_CanDownloadFile()
        {
            //arrange
            var obj = Encoding.UTF8.GetBytes("SomeString");
            var memStream = new MemoryStream(obj);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(MinioSettings, bucketName, fileGuid, memStream)));
            var objStream = MinioService.DownloadFile(MinioSettings, bucketName, fileGuid);

            //assert
            var ms = new MemoryStream();
            objStream.CopyTo(ms);

            Assert.Equal(obj, ms.ToArray());
        }

        [Fact]
        public void BackupMinio_AfterUploading_CanDownloadFile()
        {
            //arrange
            var obj = Encoding.UTF8.GetBytes("SomeString");
            var memStream = new MemoryStream(obj);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(BackupMinioSettings, bucketName, fileGuid, memStream)));
            var objStream = MinioService.DownloadFile(BackupMinioSettings, bucketName, fileGuid);

            //assert
            var ms = new MemoryStream();
            objStream.CopyTo(ms);

            Assert.Equal(obj, ms.ToArray());
        }
        #endregion


        /// <summary>
        /// removes all buckets and its items from a minio
        /// </summary>
        /// <param name="minioSettings">settings for the minio to be nuked</param>
        private static void NukeBuckets(MinioSettings minioSettings)
        {
            var minio = GetClient(minioSettings);

            minio.ListBucketsAsync().Result.Buckets.ForEach(bucket =>
            {
                minio.ListObjectsAsync(bucket.BucketName)
                    .Result.S3Objects.ForEach(o =>
                    {
                        var deleteObjectResponse = minio.DeleteObjectAsync(bucket.BucketName, o.Key).Result;
                        Assert.Equal(HttpStatusCode.NoContent, deleteObjectResponse.HttpStatusCode);
                    });
                var deleteBucketResponse = minio.DeleteBucketAsync(bucket.BucketName).Result;
                Assert.Equal(HttpStatusCode.NoContent, deleteBucketResponse.HttpStatusCode);
            });


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

        /// <summary>
        /// removes all buckets & their items on both minio and backup minio after each test,
        /// to avoid any shared objects between tests
        /// </summary>
        public void Dispose()
        {
            NukeBuckets(MinioSettings);
            NukeBuckets(BackupMinioSettings);

        }
    }
}
