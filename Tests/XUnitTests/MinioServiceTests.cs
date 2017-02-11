using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using MinioBackupManager;
using Xunit;

namespace XUnitTests
{
    public class MinioServiceTests : IDisposable
    {
        private AmazonS3Client Minio { get; }
        private AmazonS3Client BackupMinio { get; }


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

            var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
            var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
            var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
            var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

            #endregion

            Minio = MinioService.GetClient(accessKey: srcAccessKey, secretAccesKey: srcSecretKey, endpoint: $"http://{srcName}:{srcPort}");
            BackupMinio = MinioService.GetClient(accessKey: dstAccessKey, secretAccesKey: dstSecretKey, endpoint: $"http://{dstName}:{dstPort}");

            //remove all buckets before tests to avoid the default bucket "Docker" on the minio docker image interferes with tests
            NukeBuckets(Minio);
            NukeBuckets(BackupMinio);
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
                tasks.Add(Task.Run(() => Minio.PutBucketAsync(bucketName)));
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(10, Minio.ListBucketsAsync().Result.Buckets.Count);

            //act
            NukeBuckets(Minio);

            //assert
            Assert.Equal(0, Minio.ListBucketsAsync().Result.Buckets.Count);

        }
        [Fact]
        public void BackupMinio_NukeBuckets_ShouldRemoveAllBuckets()
        {
            //arrange
            var tasks = new List<Task>();

            for (var i = 0; i < 10; i++)
            {
                var bucketName = Guid.NewGuid().ToString();
                tasks.Add(Task.Run(() => BackupMinio.PutBucketAsync(bucketName)));
            }
            Task.WaitAll(tasks.ToArray());

            Assert.Equal(10, BackupMinio.ListBucketsAsync().Result.Buckets.Count);

            //act
            NukeBuckets(BackupMinio);

            //assert
            Assert.Equal(0, BackupMinio.ListBucketsAsync().Result.Buckets.Count);

        }

        #endregion

        #region no buckets

        [Fact]
        public void Minio_WithoutDoingAnything_ShouldntHaveAnyBuckets()
        {
            var countOfBuckets = Minio.ListBucketsAsync().Result.Buckets.Count;
            Assert.Equal(0, countOfBuckets);
        }

        [Fact]
        public void BackupMinio_WithoutDoingAnything_ShouldntHaveAnyBuckets()
        {
            var countOfBuckets = BackupMinio.ListBucketsAsync().Result.Buckets.Count;
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
            Task.WaitAll(Task.Run(() => Minio.UploadFileAsync(bucketName, fileGuid, memStream)));
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
            Task.WaitAll(Task.Run(() => BackupMinio.UploadFileAsync(bucketName, fileGuid, memStream)));
            //assert

        }

        #endregion

        #region uploading 2 files

        [Fact]
        public void Minio_UploadingTwoFilesToTheSameBucket_ActuallyUploadsTwoFilesToTheBucket()
        {
            //arrange
            var obj1 = Encoding.UTF8.GetBytes("SomeString");
            var obj2 = Encoding.UTF8.GetBytes("SomeOtherString");
            var memStream1 = new MemoryStream(obj1);
            var memStream2 = new MemoryStream(obj2);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid1 = Guid.NewGuid().ToString();
            var fileGuid2 = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => Minio.UploadFileAsync(bucketName, fileGuid1, memStream1)));
            Task.WaitAll(Task.Run(() => Minio.UploadFileAsync(bucketName, fileGuid2, memStream2)));


            //assert
            Assert.Equal(2, Minio.ListObjectsAsync(bucketName).Result.S3Objects.Count);
        }

        [Fact]
        public void BackupMinio_UploadingTwoFilesToTheSameBucket_ActuallyUploadsTwoFilesToTheBucket()
        {
            //arrange
            var obj1 = Encoding.UTF8.GetBytes("SomeString");
            var obj2 = Encoding.UTF8.GetBytes("SomeOtherString");
            var memStream1 = new MemoryStream(obj1);
            var memStream2 = new MemoryStream(obj2);
            var bucketName = Guid.NewGuid().ToString();
            var fileGuid1 = Guid.NewGuid().ToString();
            var fileGuid2 = Guid.NewGuid().ToString();

            //act
            Task.WaitAll(Task.Run(() => BackupMinio.UploadFileAsync(bucketName, fileGuid1, memStream1)));
            Task.WaitAll(Task.Run(() => BackupMinio.UploadFileAsync(bucketName, fileGuid2, memStream2)));


            //assert
            Assert.Equal(2, BackupMinio.ListObjectsAsync(bucketName).Result.S3Objects.Count);
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
            Task.WaitAll(Task.Run(() => Minio.UploadFileAsync(bucketName, fileGuid, memStream)));
            var objStream = Minio.DownloadFileAsync(bucketName, fileGuid);

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
            Task.WaitAll(Task.Run(() => BackupMinio.UploadFileAsync(bucketName, fileGuid, memStream)));
            var objStream = BackupMinio.DownloadFileAsync(bucketName, fileGuid);

            //assert
            var ms = new MemoryStream();
            objStream.CopyTo(ms);

            Assert.Equal(obj, ms.ToArray());
        }
        #endregion



        /// <summary>
        /// removes all buckets and its items from a minio
        /// </summary>
        /// <param name="minio">minio to be nuked</param>
        private static void NukeBuckets(IAmazonS3 minio)
        {
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

        /// <summary>
        /// removes all buckets & their items on both minio and backup minio after each test,
        /// to avoid any shared objects between tests
        /// </summary>
        public void Dispose()
        {
            NukeBuckets(Minio);
            NukeBuckets(BackupMinio);

        }
    }
}
