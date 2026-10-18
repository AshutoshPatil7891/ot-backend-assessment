@echo off
rem Starts Prometheus (http://localhost:9090) and Grafana (http://localhost:3000, admin / admin)
cd /d "%~dp0"
docker compose up -d
