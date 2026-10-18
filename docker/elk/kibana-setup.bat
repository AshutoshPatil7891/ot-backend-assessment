@echo off
rem Creates the Kibana data view "ot-logs-*" (run once, after Kibana has started)
curl.exe -s -X POST "http://localhost:5601/api/data_views/data_view" -H "kbn-xsrf: true" -H "Content-Type: application/json" -d "{\"data_view\":{\"title\":\"ot-logs-*\",\"name\":\"OT logs\",\"timeFieldName\":\"@timestamp\"}}"
echo.
echo Open http://localhost:5601/app/discover
