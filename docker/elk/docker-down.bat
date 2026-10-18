@echo off
rem Stops Elasticsearch, Kibana and Filebeat (the data volumes are kept)
cd /d "%~dp0"
docker compose down
