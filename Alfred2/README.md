# Alfred Backend - Entorno local (WhatsApp + IA + Calendar)

Este backend .NET expone Webhooks de WhatsApp (Twilio Sandbox primero), login con Google (cookie httpOnly), slots de Google Calendar (stub por ahora) e IA de intención básica para proponer turnos.

## Requisitos
- .NET 8 SDK
- PostgreSQL (cadena en `ConnectionStrings:DefaultConnection`)
- Twilio Sandbox for WhatsApp (o Meta Cloud API)
- Ngrok (para exponer el webhook)

## Variables de entorno
Copiá `.env.example` a `.env` o usá `appsettings.Development.json`. Claves:

- FRONTEND_REDIRECT_URL, PUBLIC_BASE_URL, ALLOWED_ORIGINS
- GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET
- GCAL_SCOPES, GCAL_REDIRECT_URI
- OPENAI_API_KEY
- WH_PROVIDER=twilio | meta
- TWILIO_ACCOUNT_SID, TWILIO_AUTH_TOKEN, TWILIO_FROM (whatsapp:+14155238886)
- VERIFY_TOKEN (Meta), WH_META_TOKEN, WH_META_PHONE_ID
- ConnectionStrings__DefaultConnection

## Correr local
1. Levantar backend:
```powershell
# en la carpeta del proyecto Alfred2
dotnet run
```
Por defecto escucha en `http://0.0.0.0:10000` (Render usa PORT). Ver `GET /healthz`.

2. Exponer con ngrok
```powershell
ngrok http http://localhost:10000
```
Copiá la URL pública en `PUBLIC_BASE_URL`.

3. Twilio Sandbox
- En Twilio Console > Sandbox for WhatsApp:
  - When a message comes in: `https://{PUBLIC_BASE_URL}/webhooks/whatsapp`
- Desde tu WhatsApp unite al sandbox con el código de Twilio.

4. (Opcional) Google OAuth Calendar
- Agregá `https://{PUBLIC_BASE_URL}/calendar/oauth-callback` como redirect en Google Console.
- `GET /calendar/connect` y `/calendar/oauth-callback` están como stub. (Podemos completar con el SDK de Google.)

## Flujo de prueba
- Enviá “turno” por WhatsApp al Sandbox ⇒ el bot responde con 3 opciones enumeradas.
- Respondé “1” ⇒ se crea un Turno Confirmado en DB (evento en GCal simulado con id `simulated-...`).
- `GET /api/turnos` (como usuario logueado) devuelve el turno creado.

## Endpoints
- `GET /healthz` ⇒ { status: ok }
- `GET /api/me` ⇒ usuario logueado (cookie httpOnly Google)
- `GET /api/turnos` ⇒ turnos del médico logueado (requiere auth)
- `GET /api/slots?count=3` ⇒ próximos slots (requiere auth)
- `GET /webhooks/whatsapp` ⇒ verificación Meta (hub.challenge)
- `POST /webhooks/whatsapp` ⇒ webhook Twilio/Meta, IA, propuesta 1/2/3 y confirmación
- `GET /calendar/connect` ⇒ stub OAuth Calendar
- `GET /calendar/oauth-callback` ⇒ stub

## Notas
- `IntentService` usa heurística simple para intención `solicitar_turno` y una etiqueta de tiempo (`whenTag`). Se puede enriquecer con OpenAI.
- `GCalService` calcula slots en 9–18h evitando solapamiento con Turnos Confirmados. `CreateEventAsync` devuelve id simulado hasta integrar el SDK de Google.
- `PendingSlotsService` guarda temporalmente los slots por teléfono para reconocer una respuesta “1/2/3”.
