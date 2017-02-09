using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MinioBackupManager
{
    public class BackupServiceManager
    {
        private readonly AmazonS3Client _minioClient;
        private readonly AmazonS3Client _minioBackupClient;

        public BackupServiceManager(AmazonS3Client minioClient, AmazonS3Client minioBackupClient)
        {
            _minioClient = minioClient;
            _minioBackupClient = minioBackupClient;
        }

        private async Task CopyFileToBackup(string bucket, string guid)
        {
            var memStream = new MemoryStream();

            _minioClient.DownloadFileAsync(bucket, guid).CopyTo(memStream);

            await _minioBackupClient.UploadFileAsync(bucket, guid, memStream);

            try
            {
                await _minioClient.UploadFileAsync(bucket, guid, memStream);
            }
            catch (AmazonS3Exception e)
            {
                await Console.Out.WriteLineAsync(
                    $"an error occured while backing up file {guid} to bucket {bucket} error message: {e.Message}");
            }
        }

        public void SubscribeToEvents(string rabbitMqHostname)
        {
            var factory = new ConnectionFactory() {HostName = rabbitMqHostname};
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "file_transfer_queue",//queue name
                    durable: true,//keep msgs on disk in case rabbitmq crashes
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.BasicQos(0, 1, false);
                //0 = unlimited max size per msg.
                //1 = 1 msg may be delivered without ack. 
                //false = only apply to this channel.

                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += async (model, ea) =>
                {
                    var body = ea.Body;
                    var msg = Encoding.UTF8.GetString(body);

                    //parse to dynamic to avoid possible strong typing issues
                    dynamic msgObj = JObject.Parse(msg);

                    //only conusme version 1 events that contain a bucketname and a fileguid.
                    if (msgObj.version != 1 || msgObj.bucketname == null || msgObj.fileguid == null) return;

                    Console.WriteLine($"Event received, Beginning to copy ${msgObj.fileguid} from the ${msgObj.bucketname} bucket");

                    await CopyFileToBackup(msgObj.bucketname, msgObj.fileguid);

                    Console.WriteLine($"File has successfully been copied to backup");

                    //send acknowledgement that this event has been processed back to rabbitmq
                    channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false); 
                };

                channel.BasicConsume(queue: "file_transfer_queue",
                    noAck: false,
                    consumer: consumer);

                Console.WriteLine("Listening...");
                Console.ReadLine(); //don't exit
            }
        }
    }
}