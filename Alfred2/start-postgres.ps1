#!/usr/bin/env pwsh
# Script para iniciar PostgreSQL en Docker para desarrollo local

Write-Host "`n🐘 Iniciando PostgreSQL para Alfred...`n" -ForegroundColor Cyan

# Verificar si Docker está instalado
$dockerInstalled = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerInstalled) {
    Write-Host "❌ Docker no está instalado" -ForegroundColor Red
    Write-Host "`n📥 Opciones:" -ForegroundColor Yellow
    Write-Host "  1. Instalar Docker Desktop: https://www.docker.com/products/docker-desktop" -ForegroundColor White
    Write-Host "  2. Instalar PostgreSQL nativo: https://www.postgresql.org/download/" -ForegroundColor White
    Write-Host "  3. Usar base de datos en la nube (Supabase/ElephantSQL)" -ForegroundColor White
    Write-Host "`n📚 Revisa POSTGRES-SETUP.md para más información`n" -ForegroundColor Cyan
    exit 1
}

Write-Host "✅ Docker encontrado" -ForegroundColor Green

# Verificar si el contenedor ya existe
$containerExists = docker ps -a --filter "name=alfred-postgres" --format "{{.Names}}" 2>$null

if ($containerExists) {
    Write-Host "📦 Contenedor alfred-postgres ya existe" -ForegroundColor Yellow
    
    # Verificar si está corriendo
    $containerRunning = docker ps --filter "name=alfred-postgres" --format "{{.Names}}" 2>$null
    
    if ($containerRunning) {
        Write-Host "✅ PostgreSQL ya está corriendo en puerto 5432" -ForegroundColor Green
    } else {
        Write-Host "🔄 Iniciando contenedor existente..." -ForegroundColor Yellow
        docker start alfred-postgres
        Start-Sleep -Seconds 2
        Write-Host "✅ PostgreSQL iniciado" -ForegroundColor Green
    }
} else {
    Write-Host "🚀 Creando nuevo contenedor PostgreSQL..." -ForegroundColor Yellow
    
    docker run --name alfred-postgres `
        -e POSTGRES_PASSWORD=alfred123 `
        -e POSTGRES_DB=alfred `
        -p 5432:5432 `
        -d postgres:15
    
    Write-Host "⏳ Esperando a que PostgreSQL inicie..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    Write-Host "✅ PostgreSQL creado e iniciado" -ForegroundColor Green
}

# Verificar conexión
Write-Host "`n🔍 Verificando conexión..." -ForegroundColor Cyan
$connectionTest = docker exec alfred-postgres psql -U postgres -d alfred -c "SELECT version();" 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Conexión exitosa a PostgreSQL" -ForegroundColor Green
    
    Write-Host "`n📋 Información de conexión:" -ForegroundColor Cyan
    Write-Host "  Host: localhost" -ForegroundColor White
    Write-Host "  Port: 5432" -ForegroundColor White
    Write-Host "  Database: alfred" -ForegroundColor White
    Write-Host "  Username: postgres" -ForegroundColor White
    Write-Host "  Password: alfred123" -ForegroundColor White
    
    Write-Host "`n💡 Connection String para appsettings.json:" -ForegroundColor Cyan
    Write-Host '  "Host=localhost;Port=5432;Database=alfred;Username=postgres;Password=alfred123"' -ForegroundColor Yellow
    
    Write-Host "`n🚀 Ahora puedes ejecutar:" -ForegroundColor Cyan
    Write-Host "  dotnet run" -ForegroundColor White
    
} else {
    Write-Host "⚠️  PostgreSQL iniciado pero aún no está listo" -ForegroundColor Yellow
    Write-Host "   Espera 10-15 segundos e intenta de nuevo" -ForegroundColor White
}

Write-Host "`n📚 Comandos útiles:" -ForegroundColor Cyan
Write-Host "  Ver logs:        docker logs alfred-postgres" -ForegroundColor White
Write-Host "  Detener:         docker stop alfred-postgres" -ForegroundColor White
Write-Host "  Iniciar:         docker start alfred-postgres" -ForegroundColor White
Write-Host "  Conectar:        docker exec -it alfred-postgres psql -U postgres -d alfred" -ForegroundColor White
Write-Host "  Eliminar:        docker rm -f alfred-postgres" -ForegroundColor White
Write-Host ""
