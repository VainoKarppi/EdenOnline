$body = @{
    type = "function"
    method = "MYTAG_fnc_test"
    code = 'hint "Hello from PowerShell";'
} | ConvertTo-Json

Invoke-RestMethod `
    -Uri "http://127.0.0.1:8765/api/command" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body