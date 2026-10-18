@echo off
rem Stops Prometheus and Grafana (the data volumes are kept)
cd /d "%~dp0"
docker compose down
