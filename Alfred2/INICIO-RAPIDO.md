# 🚀 INICIO RÁPIDO - Localhost

## El problema que tenías

El backend requiere PostgreSQL para funcionar, pero no estaba instalado/corriendo en tu máquina.

## Solución en 2 pasos

### 1️⃣ Iniciar PostgreSQL (elige una opción)

#### Opción A: Docker (Recomendado - si tienes Docker Desktop)
```powershell
cd f:\Alfred\Backend\Alfred2
.\start-postgres.ps1
```

Esto creará e iniciará automáticamente PostgreSQL en Docker.

#### Opción B: PostgreSQL Nativo
Si no tienes Docker, instala PostgreSQL:
- Descarga: https://www.postgresql.org/download/windows/
- Instala y recuerda la contraseña
- Luego actualiza `appsettings.json` con tu contraseña

#### Opción C: Base de datos en la nube (sin instalar nada)
- Crea cuenta gratis en https://supabase.com
- Crea un proyecto
- Copia el connection string en `appsettings.json`

### 2️⃣ Ejecutar el Backend

```powershell
cd f:\Alfred\Backend\Alfred2
dotnet run
```

El backend estará disponible en: **http://localhost:10000**

## Verificar que funciona

1. **Health check**: http://localhost:10000/healthz
   - Respuesta: `{"status":"ok"}` ✅

2. **Login con Google**: http://localhost:10000/login/google
   - (Requiere configurar GOOGLE_CLIENT_ID primero)

## Si sigues teniendo errores

### Error: "OpenAI API Key"
✅ **YA ARREGLADO** - Ahora OpenAI es opcional (solo se usa si INTENT_MODE=llm)

### Error: "Cannot connect to PostgreSQL"
📚 **Revisa**: `POSTGRES-SETUP.md` para todas las opciones de instalación

### Error: "Google OAuth"
⚙️ **Configura**: 
1. Ve a Google Cloud Console
2. Crea OAuth Client
3. Agrega las credenciales en `appsettings.json`:
   ```json
   {
     "GOOGLE_CLIENT_ID": "tu-id.apps.googleusercontent.com",
     "GOOGLE_CLIENT_SECRET": "GOCSPX-tu-secret"
   }
   ```

## Estado Actual

✅ OpenAI opcional (arreglado)
✅ appsettings.json con password para Docker
✅ Script start-postgres.ps1 creado
✅ Backend compila sin errores
⚠️ PostgreSQL necesita estar corriendo (usa script)

## Próximos pasos

1. Ejecuta `.\start-postgres.ps1` (si usas Docker)
2. Ejecuta `dotnet run`
3. Abre http://localhost:10000/healthz
4. ✅ Si ves `{"status":"ok"}` → ¡Todo funciona!

## Comandos útiles

```powershell
# Iniciar PostgreSQL (Docker)
.\start-postgres.ps1

# Ver si PostgreSQL está corriendo
docker ps | Select-String "alfred-postgres"

# Ejecutar backend
dotnet run

# Ejecutar con hot reload (recomendado)
dotnet watch run

# Ver logs de PostgreSQL
docker logs alfred-postgres
```

## Archivos de ayuda

- **POSTGRES-SETUP.md** - Guía completa de instalación de PostgreSQL
- **LOCALHOST.md** - Configuración completa paso a paso
- **start-postgres.ps1** - Script automático para Docker

---

**TL;DR**: Ejecuta `.\start-postgres.ps1` y luego `dotnet run`. Listo.
