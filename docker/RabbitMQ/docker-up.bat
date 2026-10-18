@echo off
rem Starts RabbitMQ (guest/guest, management UI at http://localhost:15672)
cd /d "%~dp0"
docker compose up -d
