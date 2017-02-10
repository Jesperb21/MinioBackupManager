#!/bin/bash
docker-compose -f ./docker-compose.ci.build.yml up

docker build -t jesperb21/miniobackupmanager:latest ./MinioBackupManager/.
docker build -t miniobackupmanager ./MinioBackupManager/.
#docker push jesperb21/miniobackupmanager