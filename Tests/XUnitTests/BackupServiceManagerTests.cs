using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using MinioBackupManager;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Xunit;

namespace XUnitTests
{
    public class BackupServiceManagerTests : IDisposable
    {
        private AmazonS3Client Minio { get; }
        private AmazonS3Client BackupMinio { get; }
        private BackupServiceManager backupManager { get; }


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

            var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
            var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
            var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
            var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

            #endregion

            Minio = MinioService.GetClient(accessKey: srcAccessKey, secretAccesKey: srcSecretKey, endpoint: $"http://{srcName}:{srcPort}");
            BackupMinio = MinioService.GetClient(accessKey: dstAccessKey, secretAccesKey: dstSecretKey, endpoint: $"http://{dstName}:{dstPort}");

            backupManager = new BackupServiceManager(Minio, BackupMinio);
            
            //remove all buckets before tests to avoid the default bucket "Docker" on the minio docker image interferes with tests
            NukeBuckets(Minio);
            NukeBuckets(BackupMinio);
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
            var bucketname = Guid.NewGuid().ToString();
            var fileguid = Guid.NewGuid().ToString();

            var obj = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
            var objSrcStream = new MemoryStream(obj);

            var rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ");
            var factory = new ConnectionFactory() { HostName = rabbitMqHostname };
            
            var subscriberThread = new Thread(() => backupManager.SubscribeToEvents(rabbitMqHostname));
            subscriberThread.Start();
            //act

            Task.WaitAll(Task.Run(() => Minio.UploadFileAsync(bucketname, fileguid, objSrcStream)));

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
            Task.WaitAll(Task.Delay(100));

            //download file
            var objStream = BackupMinio.DownloadFileAsync(bucketname, fileguid);

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
            public int version { get; set; }
            public string bucketname { get; set; }
            public string fileguid { get; set; }
        }

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

        #endregion


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