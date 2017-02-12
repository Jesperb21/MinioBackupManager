using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using MinioBackupManager;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Xunit;
using Xunit.Sdk;

namespace XUnitTests
{
    [Collection("minio tests")]
    public class BackupServiceManagerTests : IDisposable
    {
        private MinioSettings MinioSettings { get; }
        private MinioSettings BackupMinioSettings { get; }
        private BackupServiceManager BackupManager { get; }
        private IMinioService MinioService { get; }

        /// <summary>
        /// Makes a new Minio client & a new BackupMinio client before every test
        /// </summary>
        public BackupServiceManagerTests()
        {
            #region get minio settings from env

            var srcName = Environment.GetEnvironmentVariable("MINIO_SRC_NAME");
            var srcPort = Environment.GetEnvironmentVariable("MINIO_SRC_PORT");
            var srcAccessKey = Environment.GetEnvironmentVariable("MINIO_SRC_ACCESS_KEY");
            var srcSecretKey = Environment.GetEnvironmentVariable("MINIO_SRC_SECRET_KEY");

            MinioSettings = new MinioSettings() { Endpoint = $"http://{srcName}:{srcPort}", AccessKey = srcAccessKey, SecretKey = srcSecretKey};

            var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
            var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
            var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
            var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

            BackupMinioSettings = new MinioSettings() {Endpoint = $"http://{dstName}:{dstPort}", AccessKey = dstAccessKey, SecretKey = dstSecretKey};

            #endregion

            
            MinioService = new MinioService();
            
            BackupManager = new BackupServiceManager(MinioService, MinioSettings, BackupMinioSettings);
            
            //remove all buckets before tests to avoid the default bucket "Docker" on the minio docker image interferes with tests
            NukeBuckets(MinioSettings);
            NukeBuckets(BackupMinioSettings);
        }

        /// <summary>
        /// tests the entire backup process, 
        /// 1. uploads file to minio
        /// 2. sends backup event to rabbitmq
        /// 3. downloads file from backup minio
        /// </summary>
        [Fact]
        public void BackupManager_backsFileUp_correctly()
        {
            //Arrange
            const string bucketname = "testbucket"; //Guid.NewGuid().ToString();
            const string fileguid = "testfile"; //Guid.NewGuid().ToString();

            var obj = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var objSrcStream = new MemoryStream(obj);

            var rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ");
            var factory = new ConnectionFactory() { HostName = rabbitMqHostname };
            
            var subscriberThread = new Thread(() => BackupManager.SubscribeToEvents(rabbitMqHostname));

            //act
            Task.WaitAll(Task.Run(() => MinioService.UploadFileAsync(MinioSettings, bucketname, fileguid, objSrcStream)));
            Assert.Equal(1, GetClient(MinioSettings).ListObjectsAsync(bucketname).Result.S3Objects.Count);

            subscriberThread.Start();

            //copied from BackupServiceManager, lovely ain't it?
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "file_transfer_queue", //queue name
                    durable: true, 
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.BasicQos(0, 1, false);
                SendBackupRequest(channel, bucketname, fileguid);  //send the backup request
            }

            Thread.Sleep(500); //give it some time to finish the backup stuff
            
            //download file
            var objStream = MinioService.DownloadFile(BackupMinioSettings, bucketname, fileguid);

            //assert
            var ms = new MemoryStream();
            objStream.CopyTo(ms);

            Assert.Equal(obj, ms.ToArray());
        }

        #region private helper methods
        private static void PublishEvent(IModel channel, string msg)
        {
            var body = Encoding.UTF8.GetBytes(msg);

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true; //make sure events don't get lost if rabbitmq restarts


            channel.BasicPublish(exchange: "",
                routingKey: "file_transfer_queue",
                basicProperties: properties,
                body: body);
        }

        private static void SendBackupRequest(IModel channel, string bucketName, string fileGuid)
        {
            var msgObj = new FileRequestObject()
            {
                version = 1,
                bucketname = bucketName,
                fileguid = fileGuid
            };
            var msgString = JsonConvert.SerializeObject(msgObj);
            PublishEvent(channel, msgString);
        }

        private class FileRequestObject
        {
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public int version { get; set; }
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string bucketname { get; set; }
            // ReSharper disable once InconsistentNaming
            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string fileguid { get; set; }
        }

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

        #endregion


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