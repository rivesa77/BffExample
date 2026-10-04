param([int]$BffPort = 5180, [int]$BackendPort = 5181)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$logDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('bff-smoke-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$backendProcess = $null
$bffProcess = $null
Add-Type -AssemblyName System.Net.Http
$http = New-Object System.Net.Http.HttpClient
$http.Timeout = [TimeSpan]::FromSeconds(8)

function Assert-True($condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Send-Request([string]$url, [string]$json = $null) {
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $url)
    if (-not [string]::IsNullOrEmpty($json)) {
        $request.Method = [System.Net.Http.HttpMethod]::Post
        $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, 'application/json')
    }
    try {
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        try {
            [pscustomobject]@{
                Status = [int]$response.StatusCode
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                ContentType = $response.Content.Headers.ContentType.MediaType
                Location = [string]$response.Headers.Location
            }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}

function Wait-Ready([string]$url, $process) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw "El proceso termino antes de estar listo: $url" }
        try {
            if ((Send-Request "$url/health").Status -eq 200) { return }
        } catch { }
        Start-Sleep -Milliseconds 250
    }
    throw "El servicio no inicio: $url"
}

try {
    Assert-True ($BffPort -ne $BackendPort) 'Los puertos deben ser distintos.'
    # Evita consultar accidentalmente otro servidor local.
    foreach ($port in @($BffPort, $BackendPort)) {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $port)
        try { $listener.Start() } finally { $listener.Stop() }
    }

    & dotnet restore (Join-Path $projectRoot 'BffExample.slnx') --configfile (Join-Path $projectRoot 'NuGet.Config') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'La restauracion fallo.' }
    & dotnet build (Join-Path $projectRoot 'BffExample.slnx') --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'La compilacion fallo.' }

    $backendDirectory = Join-Path $projectRoot 'src/Demo.Backend'
    $bffDirectory = Join-Path $projectRoot 'src/Bff.Api'
    $backendUrl = "http://127.0.0.1:$BackendPort"
    $bffUrl = "http://127.0.0.1:$BffPort"
    $backendProcess = Start-Process dotnet -ArgumentList @(
        'bin/Debug/net10.0/Demo.Backend.dll', '--urls', $backendUrl
    ) -WorkingDirectory $backendDirectory -WindowStyle Hidden -PassThru `
      -RedirectStandardOutput (Join-Path $logDirectory 'backend.log') `
      -RedirectStandardError (Join-Path $logDirectory 'backend-error.log')
    Wait-Ready $backendUrl $backendProcess

    $bffProcess = Start-Process dotnet -ArgumentList @(
        'bin/Debug/net10.0/Bff.Api.dll', '--urls', $bffUrl,
        '--Backends:CatalogBaseUrl', "$backendUrl/",
        '--Backends:InventoryBaseUrl', "$backendUrl/"
    ) -WorkingDirectory $bffDirectory -WindowStyle Hidden -PassThru `
      -RedirectStandardOutput (Join-Path $logDirectory 'bff.log') `
      -RedirectStandardError (Join-Path $logDirectory 'bff-error.log')
    Wait-Ready $bffUrl $bffProcess

    $response = Send-Request "$bffUrl/bff/products/1"
    $product = $response.Body | ConvertFrom-Json
    Assert-True ($response.Status -eq 200 -and $product.id -eq 1) 'Producto disponible: HTTP 200.'
    Assert-True ($product.canBuy -eq $true -and $product.availability -eq 'Disponible') 'Agregacion de inventario.'
    Assert-True ($product.price -eq 899.90 -and $product.displayPrice -eq '899,90 EUR') 'Precio para la pantalla.'
    Assert-True ($product.PSObject.Properties.Name -notcontains 'supplierCost') 'El BFF expuso datos internos.'

    $soldOut = (Send-Request "$bffUrl/bff/products/2").Body | ConvertFrom-Json
    Assert-True ($soldOut.canBuy -eq $false -and $soldOut.availability -eq 'Agotado') 'Producto agotado.'
    foreach ($case in @(@{ Id = 999; Status = 404 }, @{ Id = 0; Status = 400 })) {
        $errorResponse = Send-Request "$bffUrl/bff/products/$($case.Id)"
        $problem = $errorResponse.Body | ConvertFrom-Json
        Assert-True ($errorResponse.Status -eq $case.Status -and $problem.status -eq $case.Status) 'Estado de error incorrecto.'
        Assert-True ($errorResponse.ContentType -eq 'application/problem+json') 'Se esperaba ProblemDetails.'
    }
    $invalidCreation = Send-Request "$bffUrl/bff/products" '{"name":"Raton","description":"Descripcion","price":-1,"currency":"EUR","initialStock":7}'
    $validation = $invalidCreation.Body | ConvertFrom-Json
    Assert-True ($invalidCreation.Status -eq 400 -and $validation.errors.Price.Count -eq 1) 'Alta invalida: error de entrada HTTP 400.'

    $creation = Send-Request "$bffUrl/bff/products" '{"name":"Raton","description":"Raton inalambrico.","price":35.95,"currency":"EUR","initialStock":7}'
    $created = $creation.Body | ConvertFrom-Json
    Assert-True ($creation.Status -eq 201 -and $created.id -eq 3) 'Alta de producto: HTTP 201 y nuevo identificador.'
    Assert-True ($creation.Location -eq '/bff/products/3') 'Location debe apuntar al producto en el BFF.'
    Assert-True ($created.name -eq 'Raton' -and $created.price -eq 35.95) 'Datos del producto creado.'
    Assert-True ($created.PSObject.Properties.Name -notcontains 'supplierCost') 'El alta expuso datos internos.'
    $createdPage = (Send-Request "$bffUrl$($creation.Location)").Body | ConvertFrom-Json
    Assert-True ($createdPage.id -eq 3 -and $createdPage.canBuy -eq $true -and $createdPage.displayPrice -eq '35,95 EUR') 'Consulta del producto creado con inventario inicial.'

    $freeCreation = Send-Request "$bffUrl/bff/products" '{"name":"Gratis","description":"Producto gratuito.","price":0,"currency":"EUR"}'
    Assert-True ($freeCreation.Status -eq 201) 'Precio cero y existencias iniciales opcionales.'
    $freePage = (Send-Request "$bffUrl$($freeCreation.Location)").Body | ConvertFrom-Json
    Assert-True ($freePage.price -eq 0 -and $freePage.canBuy -eq $false) 'Inventario inicial cero por defecto.'

    $page = Send-Request "$bffUrl/"
    Assert-True ($page.Status -eq 200 -and $page.Body.Contains('/bff/products/')) 'Pagina web no disponible.'

    # Solo se detiene el backend creado por este script.
    Stop-Process -Id $backendProcess.Id
    $backendProcess.WaitForExit()
    $unavailable = Send-Request "$bffUrl/bff/products/1"
    Assert-True ($unavailable.Status -eq 502) 'Un backend desconectado debe producir HTTP 502.'
    Assert-True ($unavailable.ContentType -eq 'application/problem+json') 'Error de backend sin ProblemDetails.'
    Write-Host 'OK: consulta, DTO, alta (201), validacion de entrada (400), inventario inicial, 404, pagina web y backend desconectado (502).'
} catch {
    Write-Host "Logs de diagnostico: $logDirectory"
    throw
} finally {
    foreach ($process in @($bffProcess, $backendProcess)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id
            $process.WaitForExit()
        }
    }
    $http.Dispose()
}
