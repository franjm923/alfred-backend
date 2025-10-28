#!/usr/bin/env pwsh
# Script de verificación para localhost

Write-Host "🔍 Verificando configuración de Backend para localhost...`n" -ForegroundColor Cyan

$checks = @()

# 1. Verificar appsettings.json
$settingsPath = "appsettings.json"
if (Test-Path $settingsPath) {
    $content = Get-Content $settingsPath -Raw
    $json = $content | ConvertFrom-Json
    
    # ConnectionString
    $hasDb = $json.ConnectionStrings.DefaultConnection -ne ""
    $checks += @{
        Name = "ConnectionString configurado"
        Pass = $hasDb
        Message = if ($hasDb) { "✅ Database configurada" } else { "❌ Falta configurar ConnectionString" }
    }
    
    # Google OAuth
    $hasGoogle = ($json.GOOGLE_CLIENT_ID -ne "") -and ($json.GOOGLE_CLIENT_SECRET -ne "")
    $checks += @{
        Name = "Google OAuth configurado"
        Pass = $hasGoogle
        Message = if ($hasGoogle) { "✅ Google OAuth listo" } else { "⚠️  Falta configurar GOOGLE_CLIENT_ID y GOOGLE_CLIENT_SECRET" }
    }
    
    # GCAL_REDIRECT_URI
    $hasCalRedirect = $json.GCAL_REDIRECT_URI -ne ""
    $checks += @{
        Name = "GCAL_REDIRECT_URI presente"
        Pass = $hasCalRedirect
        Message = if ($hasCalRedirect) { "✅ GCAL_REDIRECT_URI: $($json.GCAL_REDIRECT_URI)" } else { "❌ Falta GCAL_REDIRECT_URI" }
    }
    
    # GCAL_SCOPES
    $hasScopes = $json.GCAL_SCOPES -ne ""
    $checks += @{
        Name = "GCAL_SCOPES presente"
        Pass = $hasScopes
        Message = if ($hasScopes) { "✅ GCAL_SCOPES configurado" } else { "❌ Falta GCAL_SCOPES" }
    }
    
    # Feature Flags
    $hasFlags = $null -ne $json.FeatureFlags
    $checks += @{
        Name = "Feature Flags configurados"
        Pass = $hasFlags
        Message = if ($hasFlags) { 
            "✅ Flags: INTENT=$($json.FeatureFlags.INTENT_MODE), CALENDAR=$($json.FeatureFlags.CALENDAR_MODE), PERSIST=$($json.FeatureFlags.PERSIST_TURNOS)" 
        } else { 
            "❌ Faltan FeatureFlags" 
        }
    }
    
    # CORS
    $hasOrigins = $json.ALLOWED_ORIGINS -ne ""
    $checks += @{
        Name = "CORS (ALLOWED_ORIGINS)"
        Pass = $hasOrigins
        Message = if ($hasOrigins) { "✅ Origins: $($json.ALLOWED_ORIGINS)" } else { "⚠️  Falta ALLOWED_ORIGINS" }
    }
} else {
    $checks += @{
        Name = "appsettings.json existe"
        Pass = $false
        Message = "❌ No se encuentra appsettings.json"
    }
}

# 2. Verificar .NET SDK
$dotnetVersion = dotnet --version 2>$null
$hasDotnet = $null -ne $dotnetVersion
$checks += @{
    Name = ".NET SDK instalado"
    Pass = $hasDotnet
    Message = if ($hasDotnet) { "✅ .NET $dotnetVersion" } else { "❌ .NET SDK no encontrado" }
}

# 3. Verificar build
Write-Host "`n🔨 Verificando build..." -ForegroundColor Yellow
$buildOutput = dotnet build --no-restore 2>&1
$buildSuccess = $LASTEXITCODE -eq 0
$checks += @{
    Name = "Build exitoso"
    Pass = $buildSuccess
    Message = if ($buildSuccess) { "✅ Build OK" } else { "❌ Build falló" }
}

# Mostrar resultados
Write-Host "`n📋 Resultados:`n" -ForegroundColor Cyan
$allPassed = $true
foreach ($check in $checks) {
    Write-Host $check.Message
    if (-not $check.Pass -and $check.Message -notlike "*⚠️*") {
        $allPassed = $false
    }
}

Write-Host ("`n" + "=" * 60) -ForegroundColor Gray
if ($allPassed) {
    Write-Host "✅ Backend listo para ejecutar en localhost!" -ForegroundColor Green
    Write-Host "`n📝 Para ejecutar:" -ForegroundColor Cyan
    Write-Host "   dotnet run" -ForegroundColor White
    Write-Host "   o" -ForegroundColor Gray
    Write-Host "   dotnet watch run" -ForegroundColor White -NoNewline
    Write-Host " (con hot reload)" -ForegroundColor Gray
    Write-Host "`n🌐 El backend estará en:" -ForegroundColor Cyan
    Write-Host "   http://localhost:10000" -ForegroundColor White
    Write-Host "`n📚 Revisa LOCALHOST.md para más información" -ForegroundColor Cyan
} else {
    Write-Host "⚠️  Hay configuraciones pendientes" -ForegroundColor Yellow
    Write-Host "`n📝 Próximos pasos:" -ForegroundColor Cyan
    Write-Host "1. Configura las variables faltantes en appsettings.json" -ForegroundColor White
    Write-Host "2. Revisa LOCALHOST.md para instrucciones detalladas" -ForegroundColor White
    Write-Host "3. Ejecuta este script de nuevo" -ForegroundColor White
}
Write-Host ("=" * 60 + "`n") -ForegroundColor Gray
