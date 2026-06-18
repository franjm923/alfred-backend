# CLAUDE.md

Guía para trabajar en **Alfred**. Leéla antes de tocar código.

---

## Qué es Alfred

Producto que **automatiza turnos médicos** mediante un **agente de IA conversacional**: una persona común chatea (texto **o audio**) y el agente le saca el turno. Se vende a **médicos particulares** (a futuro, clínicas).

- **Etapa**: pre-lanzamiento. **No hay usuarios productivos todavía** → la deuda técnica se documenta como "a resolver antes de salir", no como incendio.
- **Canal del MVP**: **Telegram primero** (más simple que WhatsApp, que tiene la complejidad de verificación de Meta). WhatsApp viene después, cuando todo esté sólido.

---

## Visión de arquitectura (IMPORTANTE)

El core del producto —el agente conversacional— **vive en n8n**, no en C#.

```
Mensaje (Telegram/WhatsApp) → n8n (agente IA: arma system prompt, resuelve el chat, maneja texto/audio)
                                  │
                                  ├── pide a la API de C#: contexto del médico, slots, crear turno
                                  └── tareas repetitivas: recordatorios, confirmaciones, sync Calendar, seguimientos
```

- **C# = infraestructura**: BD sólida, mapea **número → médico (id)**, guarda el **contexto del médico** (tipo/especialidad, config) que alimenta el system prompt del agente, y expone la **API de dominio** (turnos, slots, datos). También dispara workflows de n8n.
- **n8n = entrada + agente + orquestación** de lo repetitivo.

### Estado actual vs. objetivo (clave)

Hoy el repo es un **monolito C# que hace todo** (recibe el webhook, clasifica intención con OpenAI y propone slots dentro de C#). **Eso es legacy de una estructura rígida anterior.**

➡️ **No construir lógica nueva de agente/conversación en C#.** Ese flujo migra a n8n. Código en transición (no extender):
`Program.cs` `/webhooks/whatsapp`, `IntentService`, `OpenAIService`, `WhatsAppConversationService`, `WhatsAppCloudWebhookController`, `BotController`.

---

## Stack

- **Backend**: .NET 8 · ASP.NET Core (minimal API + controllers) · EF Core + Npgsql (**PostgreSQL**).
- **Auth**: Google OAuth → cookie httpOnly `alfred.auth`.
- **Integraciones**: Google Calendar (FreeBusy + eventos), OpenAI (intención, legacy), Twilio + Meta WhatsApp (legacy). **A futuro**: Telegram, n8n.
- **Frontend**: vanilla HTML/CSS/JS en `frontend/` (**canónico**, en rediseño). `frontend/FrontendWeb/` (Next.js) = **legacy a eliminar**.
- **Deploy**: Render (Docker, puerto 5000).

---

## Estructura del repo

```
Alfred2/                       # backend C#
  Program.cs                   # config + endpoints minimal API (auth, /api/me, /api/turnos, /calendar/*, webhooks)
  Controladores/               # AdminController, BotController, WhatsAppCloudWebhookController
  Services/                    # GCalService, GoogleOAuthService, IntentService, OpenAIService,
                               #   WhatsAppConversationService, responders, PendingSlotsService
                               #   helpers compartidos: SpanishDateParser, TimeZoneHelper, TokenProtector
  Models/Models.cs             # entidades EF (Medico, Paciente, Turno, Servicio, Conversacion, ...)
  DTOs/  DBContext/  Migrations/
frontend/                      # front vanilla (canónico)
  Js/config.js  Js/auth.js     # config (URL backend) + helpers de sesión reutilizables
  Js/*.js  css/  *.html
  FrontendWeb/                 # Next.js LEGACY → borrar cuando el vanilla esté completo
Alfred2.sln   Dockerfile
```

### Modelo de datos (resumen)
`Medico` es el núcleo → tiene `Pacientes`, `Servicios`, `Turnos`, `Conversaciones`, `Disponibilidades`, `Bloqueos`, `Integraciones`. `User` (auth) es 1–1 con `Medico`. `IntegracionCalendario` guarda los tokens de Google **cifrados** (`TokenProtector`).

---

## Comandos

```bash
dotnet build Alfred2.sln               # compilar (correr SIEMPRE antes de dar algo por terminado)
dotnet run --project Alfred2           # correr (necesita Postgres + env vars)
cd frontend && npx serve -l 3000       # servir el front (el puerto debe estar en ALLOWED_ORIGINS)
```

> Si `dotnet` no aparece en la terminal: reabrí la terminal/VS Code (PATH), o usá la ruta completa
> `"C:\Program Files\dotnet\dotnet.exe"`. Está en el PATH de Machine.

---

## Variables de entorno / config

| Área | Claves |
|------|--------|
| DB | `ConnectionStrings:DefaultConnection` |
| OpenAI | `OPENAI_API_KEY` |
| Google OAuth | `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET` |
| Google Calendar | `GCAL_REDIRECT_URI`, `GCAL_SCOPES` |
| URLs / CORS | `FRONTEND_REDIRECT_URL`, `ALLOWED_ORIGINS`, `PUBLIC_BASE_URL` |
| WhatsApp (legacy) | `WH_PROVIDER` (twilio\|meta), `TWILIO_*`, `VERIFY_TOKEN`, `WHATSAPP_VERIFY_TOKEN`, `WHATSAPP_TOKEN`, `WHATSAPP_PHONE_NUMBER_ID` |
| Feature flags | `FeatureFlags:INTENT_MODE` (simple\|llm), `FeatureFlags:CALENDAR_MODE` (simulate\|real), `FeatureFlags:PERSIST_TURNOS` (true\|false) |
| Render | `PORT` |

---

## Convenciones

- **Idioma**: dominio en **español** (`Medico`, `Turno`, `NombreCompleto`), técnico/infra en **inglés** (`GetNextSlotsAsync`), comentarios en **español**.
- **DRY**: no reintroducir duplicaciones. Ya se hizo una limpieza fuerte; usar los helpers compartidos (`SpanishDateParser`, `TimeZoneHelper`, `TokenProtector`) en vez de copiar lógica.
- **Frontend — contrato DOM** (mantener estos `id` para que el cableado siga funcionando):
  - Header: `nombreUsuario`, `avatar`, un trigger que llame `logout()`
  - Login: `googleBtn` · Home: `turnos-proximos`, `turnos-historial` · Settings: `calendar-status` · Perfil: `campo-*`
  - Cada página carga `Js/config.js` + `Js/auth.js` + su JS.

---

## Reglas de trabajo (para Claude)

1. **Antes de cambios grandes: armar el plan de ejecución completo y esperar que Fran lo valide** (él lo corrobora con sus skills) **antes de implementar.**
2. **Siempre `dotnet build`** antes de declarar algo terminado.
3. **No construir lógica nueva de agente/conversación en C#** → va a n8n. El flujo de conversación actual en C# es legacy.
4. **Dominio en español** (ver Convenciones).
5. **Frontend**: solo cablear backend; respetar el contrato DOM; **no tocar la estética** (la hace Fran).
6. **Git**: no pushear sin pedirlo. Si hay que commitear y estás en `main`, crear rama primero.

---

## Deuda técnica conocida (pre-lanzamiento, no urgente)

- **Persistir claves de DataProtection** antes de prod real: en Render (filesystem efímero) cada redeploy invalida los tokens de Calendar cifrados → los médicos tendrían que reconectar.
- **Endpoint para editar perfil** (`PUT /api/me`): hoy el Perfil del front es read-only porque no existe.
- **Login email/password sin backend**: solo hay Google OAuth. El form de email/password del front es cosmético.
- **Eliminar `frontend/FrontendWeb`** (Next.js legacy) cuando el vanilla esté completo.
- 3 warnings de compilación menores (nullable ×2, async sin await).
