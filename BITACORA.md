# Bitácora — Alfred

Registro de lo que se va haciendo. Lo más reciente arriba.

---

## 2026-06-18

### Fase 2 — Verificación de matrícula
- **Modelo**: `EstadoVerificacion` (Borrador/Pendiente/Autorizado/Rechazado) en `Medico`.
- **⚠️ Reset de migraciones**: el historial era todo del esquema e-commerce viejo (no había migración para el esquema médico). Se borraron las migraciones viejas y se generó un `InitialCreate` limpio desde el modelo médico. **Pendiente: resetear la DB de Render** (dropear tablas) antes del próximo deploy — no hay datos reales.
- **Backend (TDD)**: `MedicoPerfilService` (guardar perfil, enviar a verificación con validación) + `AdminMedicoService` (listar pendientes, aprobar, rechazar) → **12 tests verdes**.
- **Endpoints**: `PUT /api/medico/perfil`, `POST /api/medico/verificacion`, `GET /api/admin/verificaciones/pendientes`, `POST /api/admin/medicos/{id}/aprobar|rechazar`; `/api/me` ahora devuelve `EstadoVerificacion`.
- **Frontend**: Perfil editable (especialidad + matrícula) con "Enviar para verificación" → mensaje *"Se ha enviado su información, espere a que sea aceptado"*; panel admin lista pendientes con Aprobar/Rechazar. Helpers `Alfred.apiPut`/`apiPost`.
- Decisiones: horarios = solo Google Calendar (FreeBusy); aprobación manual del admin.
- Setup: instalado `dotnet-ef` + `AppDbContextFactory` (design-time) para generar migraciones.

### Fase 1 — Roles + médico de prueba (admin)
- **`AdminMedicoService`** ([Alfred2/Services/AdminMedicoService.cs](Alfred2/Services/AdminMedicoService.cs)): upsert de un médico de prueba (Id + número), 2 tests → **7 verdes**.
- **`POST /api/admin/medicos`** (solo rol `admin`): registra/actualiza el médico de prueba.
- **Frontend**: panel admin role-gated en Settings (form Id+número+nombre) + helper `Alfred.apiPost`.
- **Fix auth**: `RoleClaimsTransformation` mete el rol (desde la BD) en los claims → `[Authorize(Roles=...)]` ahora funciona (antes daba 403 para todos).
- Commits `ed2f42b`, `64fab3b` (sin pushear).
- Decisiones: matrícula = aprobación manual del admin; admin entra ingresando médico Id+número a mano.

### Core de turnos del agente (TDD) + fixes
- **`AgentTurnoService.CrearTurnoAsync`** (nuevo, [Alfred2/Services/AgentTurnoService.cs](Alfred2/Services/AgentTurnoService.cs)): crea turno, valida médico existente, valida solape de horario, `Origen` configurable (default `Telegram`), duración desde el `Servicio` (fallback a `DuracionMin`).
- **Proyecto de tests `Alfred2.Tests`** (xUnit + EF InMemory) → **5 tests verdes** (ciclo red-green-refactor).
- Excepciones de dominio movidas a `Alfred2.Domain.Exceptions`.
- `OrigenTurno.Telegram` agregado al enum.
- **Dockerfile**: ahora restaura/publica `Alfred2/Alfred2.csproj` (no la solución), así el test project no rompe el build de Render.
- Commits `3cc4ba2`, `4bab7f1` → pusheados a `main`.

---

## 2026-06-17

### Planificación (skills)
- **CLAUDE.md** creado vía skill `grill-me` (arquitectura, convenciones, reglas de trabajo).
- **PRD** publicado como issue **#1** (skill `to-prd`).
- **6 vertical slices** como issues **#2–#7** con bloqueos para paralelizar (skill `to-issues`).
- **TDD** iniciado sobre el tracer bullet (Slice A) → core `AgentTurnoService`.

### Frontend
- Reescrito al **dominio de turnos médicos** (Home = turnos, Perfil, Settings/Calendar).
- **Login con Google OAuth real**; borradas las páginas e-commerce huérfanas (Pedido/Register).
- Integrado el **nuevo diseño** (index/login) + unificada la config de URL del backend.
- Contrato DOM documentado para que el rediseño no rompa el cableado.

### Backend (limpieza Clean Code / Pragmático)
- DRY: extraídos `SpanishDateParser` y `TimeZoneHelper` (3–4 copias eliminadas; corregido bug de días).
- **Tokens OAuth cifrados** con `TokenProtector` (IDataProtector).
- Webhook gigante de WhatsApp extraído a `WhatsAppConversationService`.
- Firma Twilio: **rechaza** requests inválidos en prod.
- `Id` duplicado en `Medico`/`User` removido (warnings CS0108).
- Commit `97c9c43`.

### Infra
- Monorepo: frontend integrado en `frontend/` (antes repo aparte).
- Deploy en Render (Docker, puerto 5000).
