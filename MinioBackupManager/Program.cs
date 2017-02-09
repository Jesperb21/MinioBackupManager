using System;
using MinioBackupManager;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Initializing backup manager");
        Console.WriteLine("Getting environment configurations");

        var srcName = Environment.GetEnvironmentVariable("MINIO_SRC_NAME");
        var srcPort = Environment.GetEnvironmentVariable("MINIO_SRC_PORT");
        var srcAccessKey = Environment.GetEnvironmentVariable("MINIO_SRC_ACCESS_KEY");
        var srcSecretKey = Environment.GetEnvironmentVariable("MINIO_SRC_SECRET_KEY");

        var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
        var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
        var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
        var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

        var rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ");

        Console.WriteLine($"Configuration loaded, manager will copy objects from \"{srcName}\" to \"{dstName}\" when getting events from \"{rabbitMqHostname}\"");


        Console.WriteLine("Initializing minio clients");
        var minioClient = MinioService.GetClient(accessKey: srcAccessKey, secretAccesKey: srcSecretKey, endpoint: $"http://{srcName}:{srcPort}");
        var minioBackupClient = MinioService.GetClient(accessKey: dstAccessKey, secretAccesKey: dstSecretKey, endpoint: $"http://{dstName}:{dstPort}");
        Console.WriteLine($"src = http://{srcName}:{srcPort}");
        Console.WriteLine($"dst = http://{dstName}:{dstPort}");

        Console.WriteLine("Generating backup manager");
        var backupManager = new BackupServiceManager(minioClient, minioBackupClient);

        Console.WriteLine("Subscribing to events");
        backupManager.SubscribeToEvents(rabbitMqHostname);

        Console.Out.WriteLine("Backup manager exit unexpectedly");
    }
}