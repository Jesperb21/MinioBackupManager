# MinioBackupManager
simple manager for copying files from Minio A to Minio B, triggered by RabbitMQ events

***NOTE*** *this is a proof of concept solution*


The MinioBackupManager is a tool for copying files from one [Minio](https://minio.io/) to another, triggered by a [RabbitMQ](https://www.rabbitmq.com/) event containing a bucket name and a file id.
Access key, Secret key, hostname and port can be changed through environment variables in the dockerfile.
