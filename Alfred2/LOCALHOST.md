# 🚀 Configuración para Localhost

## Pre-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) (local o Docker)
- [Node.js 18+](https://nodejs.org/) (para el frontend)

## 1. Base de Datos (PostgreSQL)

### Opción A: PostgreSQL Local
```bash
# Instalar PostgreSQL (Windows/Mac/Linux)
# Crear base de datos
createdb alfred
```

### Opción B: Docker
```bash
docker run --name alfred-postgres \
  -e POSTGRES_PASSWORD=tu_password \
  -e POSTGRES_DB=alfred \
  -p 5432:5432 \
  -d postgres:15
```

## 2. Configurar appsettings.json

El archivo `appsettings.json` ya tiene valores por defecto para localhost. Solo necesitas actualizar:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=alfred;Username=postgres;Password=TU_PASSWORD"
  },
  "GOOGLE_CLIENT_ID": "tu-client-id.apps.googleusercontent.com",
  "GOOGLE_CLIENT_SECRET": "GOCSPX-tu-secret",
  "OpenAI": {
    "ApiKey": "sk-proj-tu-key"
  }
}
```

### Variables Opcionales (solo si necesitas WhatsApp)

```json
{
  "TWILIO_ACCOUNT_SID": "ACxxxx",
  "TWILIO_AUTH_TOKEN": "tu-token",
  "TWILIO_FROM": "whatsapp:+14155238886"
}
```

## 3. Google OAuth (Requerido)

### Configurar en Google Cloud Console

1. Ve a [Google Cloud Console](https://console.cloud.google.com/)
2. Crea un nuevo proyecto o selecciona uno existente
3. Habilita "Google+ API" y "Google Calendar API"
4. Ve a "Credentials" → "Create Credentials" → "OAuth 2.0 Client ID"
5. Tipo: Web application
6. **Authorized JavaScript origins:**
   - `http://localhost:10000`
7. **Authorized redirect URIs:**
   - `http://localhost:10000/signin-google`
   - `http://localhost:10000/calendar/oauth-callback`

8. Copia el **Client ID** y **Client Secret** al `appsettings.json`

## 4. Ejecutar Migraciones

```bash
cd Backend/Alfred2

# Restaurar paquetes
dotnet restore

# Crear/actualizar base de datos
dotnet ef database update
# O simplemente ejecuta el proyecto (las migraciones se aplican automáticamente)

# Ejecutar
dotnet run
```

El backend estará en: `http://localhost:10000`

## 5. Frontend (Opcional)

```bash
cd Frontend/FrontendWeb

# Instalar dependencias
npm install

# Configurar backend URL
# En .env.local (ya está configurado):
NEXT_PUBLIC_BACKEND_URL=http://localhost:10000

# Ejecutar
npm run dev
```

El frontend estará en: `http://localhost:3000`

## 6. Verificar que Funciona

### Backend
1. Abre: `http://localhost:10000/healthz`
   - ✅ Debe responder: `{"status":"ok"}`

### Login con Google
1. Abre: `http://localhost:3000/login`
2. Click en "Iniciar sesión con Google"
3. Autoriza la aplicación
4. ✅ Debe redirigir a `/home`

### API de Turnos
1. Después de login, abre: `http://localhost:10000/api/me`
   - ✅ Debe mostrar tu información de usuario

## 7. Feature Flags (Modo Desarrollo)

En `appsettings.json` los flags están configurados para desarrollo:

```json
{
  "FeatureFlags": {
    "INTENT_MODE": "simple",      // No requiere OpenAI
    "CALENDAR_MODE": "simulate",   // No requiere Google Calendar conectado
    "PERSIST_TURNOS": "true"      // Guarda en DB
  }
}
```

### Cambiar a modo completo:

```json
{
  "FeatureFlags": {
    "INTENT_MODE": "llm",        // Requiere OPENAI_API_KEY
    "CALENDAR_MODE": "real",     // Requiere conectar Google Calendar
    "PERSIST_TURNOS": "true"
  }
}
```

## 8. Probar WhatsApp (Opcional)

### Usando Twilio Sandbox

1. Crea cuenta en [Twilio](https://www.twilio.com/)
2. Ve a WhatsApp Sandbox
3. Conecta tu número siguiendo las instrucciones
4. Configura webhook: `http://localhost:10000/webhooks/whatsapp`
5. Necesitarás usar [ngrok](https://ngrok.com/) para exponer localhost:

```bash
ngrok http 10000
# Copia la URL HTTPS y úsala como PUBLIC_BASE_URL
```

6. Actualiza en `appsettings.json`:
```json
{
  "PUBLIC_BASE_URL": "https://tu-url-ngrok.ngrok.io",
  "TWILIO_ACCOUNT_SID": "ACxxxx",
  "TWILIO_AUTH_TOKEN": "tu-token"
}
```

## 9. Estructura de Base de Datos

Al ejecutar por primera vez, se crearán estas tablas:

- `Users` - Usuarios del sistema
- `Medicos` - Médicos/profesionales
- `Pacientes` - Pacientes
- `Servicios` - Tipos de consulta
- `Turnos` - Citas agendadas
- `Conversaciones` - Chats de WhatsApp
- `Mensajes` - Mensajes individuales
- `IntegracionCalendario` - Tokens de Google Calendar
- `DisponibilidadSemanal` - Horarios del médico
- `BloqueoAgenda` - Días bloqueados

## 10. Endpoints Disponibles

### Públicos
- `GET /healthz` - Health check
- `GET /webhooks/whatsapp` - Verificación Meta
- `POST /webhooks/whatsapp` - Recibir mensajes

### Autenticación
- `GET /login/google` - Iniciar login con Google
- `GET /signin-google` - Callback de Google OAuth
- `GET /logout` - Cerrar sesión

### Autorizados (requiere login)
- `GET /api/me` - Info del usuario actual
- `GET /api/turnos` - Lista de turnos
- `GET /api/slots?count=3` - Próximos slots disponibles
- `GET /calendar/connect` - Conectar Google Calendar
- `GET /calendar/oauth-callback` - Callback de Calendar

### Solo Development
- `POST /dev/default-medico?medicoId=<guid>` - Setear médico por defecto

## Solución de Problemas

### Error: "Cannot connect to database"
- Verifica que PostgreSQL esté corriendo
- Revisa la connection string en `appsettings.json`
- Prueba: `psql -U postgres -d alfred`

### Error: "GOOGLE_CLIENT_ID not found"
- Configura las credenciales de Google en `appsettings.json`
- Verifica que los redirect URIs estén correctos en Google Console

### Error: "OpenAI API key invalid"
- Si usas `INTENT_MODE=llm`, necesitas una API key válida
- Cambia a `INTENT_MODE=simple` para desarrollo sin OpenAI

### El login con Google no funciona
- Verifica que el `GOOGLE_CLIENT_ID` y `GOOGLE_CLIENT_SECRET` sean correctos
- Verifica los redirect URIs en Google Console
- Revisa que `ALLOWED_ORIGINS` incluya `http://localhost:3000`

### CORS Error en el frontend
- Verifica que `ALLOWED_ORIGINS` en `appsettings.json` incluya tu dominio del frontend
- Asegúrate de que el backend esté corriendo en el puerto correcto

## Tips de Desarrollo

1. **Hot Reload**: Usa `dotnet watch run` para recargar automáticamente
2. **Logs**: Revisa la consola para ver todos los logs de requests
3. **DB Reset**: `dotnet ef database drop` para limpiar la DB
4. **Seed Data**: Crea un médico manualmente en la DB o usando `/dev/default-medico`

## Próximos Pasos

Una vez que funcione en localhost:
1. Revisa `DEPLOY.md` para deploy en Render
2. Revisa `VERCEL-DEPLOY.md` para deploy del frontend
3. Configura las variables de producción en Render dashboard
