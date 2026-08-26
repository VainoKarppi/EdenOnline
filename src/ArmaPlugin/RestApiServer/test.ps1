$body = @{
    type = "recompile"
    method = "MYTAG_fnc_test"
    code = "params [\"_unit\"];\n\ndiag_log format [\n    \"Hello %1\",\n    _unit\n];"
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "http://127.0.0.1:8765/api/command" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body