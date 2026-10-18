@echo off
rem Starts Elasticsearch, Kibana and Filebeat (needs about 2 GB of free memory)
rem Kibana: http://localhost:5601   (after it is ready, run kibana-setup.bat once)
cd /d "%~dp0"
docker compose up -d
