using System;
using MinioBackupManager;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Initializing backup manager");
        Console.WriteLine("Getting environment configurations");

        #region get minio settings from env

        var srcName = Environment.GetEnvironmentVariable("MINIO_SRC_NAME");
        var srcPort = Environment.GetEnvironmentVariable("MINIO_SRC_PORT");
        var srcAccessKey = Environment.GetEnvironmentVariable("MINIO_SRC_ACCESS_KEY");
        var srcSecretKey = Environment.GetEnvironmentVariable("MINIO_SRC_SECRET_KEY");

        var source = new MinioSettings() { Endpoint = $"http://{srcName}:{srcPort}", AccessKey = srcAccessKey, SecretKey = srcSecretKey };

        var dstName = Environment.GetEnvironmentVariable("MINIO_DST_NAME");
        var dstPort = Environment.GetEnvironmentVariable("MINIO_DST_PORT");
        var dstAccessKey = Environment.GetEnvironmentVariable("MINIO_DST_ACCESS_KEY");
        var dstSecretKey = Environment.GetEnvironmentVariable("MINIO_DST_SECRET_KEY");

        var destination = new MinioSettings() { Endpoint = $"http://{dstName}:{dstPort}", AccessKey = dstAccessKey, SecretKey = dstSecretKey };

        #endregion

        var rabbitMqHostname = Environment.GetEnvironmentVariable("RABBITMQ");

        Console.WriteLine($"Configuration loaded, manager will copy objects from \"{srcName}\" to \"{dstName}\" when getting events from \"{rabbitMqHostname}\"");

        Console.WriteLine($"src = http://{srcName}:{srcPort}");
        Console.WriteLine($"dst = http://{dstName}:{dstPort}");

        IMinioService minioService = new MinioService();

        Console.WriteLine("Generating backup manager");
        var backupManager = new BackupServiceManager(minioService, source, destination);

        Console.WriteLine("Subscribing to events");
        backupManager.SubscribeToEvents(rabbitMqHostname);

        Console.Out.WriteLine("Backup manager exit unexpectedly");
    }
}