using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Alfred2.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    GoogleSub = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Picture = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Especialidad = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Matricula = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EstadoVerificacion = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TelefonoE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ZonaHorariaIana = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AutoResponderHabilitado = table.Column<bool>(type: "boolean", nullable: false),
                    TextoAutoResponder = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medicos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bloqueos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bloqueos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bloqueos_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Disponibilidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiaSemana = table.Column<int>(type: "integer", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HoraFin = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DuracionTurnoMin = table.Column<int>(type: "integer", nullable: false),
                    Habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Disponibilidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Disponibilidades_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Integraciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Proveedor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExternoAccountId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AccessTokenEnc = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RefreshTokenEnc = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integraciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Integraciones_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pacientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreCompleto = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TelefonoE164 = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notas = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pacientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pacientes_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Servicios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DuracionMin = table.Column<int>(type: "integer", nullable: false),
                    Precio = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Habilitado = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servicios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Servicios_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Canal = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    NumeroRemitenteE164 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NumeroPacienteE164 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UltimoMensajeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternoThreadId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Conversaciones_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Turnos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicoId = table.Column<Guid>(type: "uuid", nullable: false),
                    PacienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServicioId = table.Column<Guid>(type: "uuid", nullable: true),
                    InicioUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    Modalidad = table.Column<int>(type: "integer", nullable: false),
                    Origen = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    NotasInternas = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    PrecioAcordado = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Moneda = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Turnos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Turnos_Medicos_MedicoId",
                        column: x => x.MedicoId,
                        principalTable: "Medicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turnos_Pacientes_PacienteId",
                        column: x => x.PacienteId,
                        principalTable: "Pacientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Turnos_Servicios_ServicioId",
                        column: x => x.ServicioId,
                        principalTable: "Servicios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Mensajes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversacionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direccion = table.Column<int>(type: "integer", nullable: false),
                    Texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MediaUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    EnviadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EntregadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LeidoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExternoMessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Plantilla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mensajes_Conversaciones_ConversacionId",
                        column: x => x.ConversacionId,
                        principalTable: "Conversaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TurnoSincronizaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegracionCalendarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternoCalendarId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ExternoEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    UltimoError = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    UltimoSyncUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Eliminado = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TurnoSincronizaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TurnoSincronizaciones_Integraciones_IntegracionCalendarioId",
                        column: x => x.IntegracionCalendarioId,
                        principalTable: "Integraciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TurnoSincronizaciones_Turnos_TurnoId",
                        column: x => x.TurnoId,
                        principalTable: "Turnos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bloqueos_MedicoId_InicioUtc",
                table: "Bloqueos",
                columns: new[] { "MedicoId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_MedicoId_PacienteId_Canal",
                table: "Conversaciones",
                columns: new[] { "MedicoId", "PacienteId", "Canal" });

            migrationBuilder.CreateIndex(
                name: "IX_Conversaciones_PacienteId",
                table: "Conversaciones",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Disponibilidades_MedicoId_DiaSemana",
                table: "Disponibilidades",
                columns: new[] { "MedicoId", "DiaSemana" });

            migrationBuilder.CreateIndex(
                name: "IX_Integraciones_MedicoId_Proveedor",
                table: "Integraciones",
                columns: new[] { "MedicoId", "Proveedor" });

            migrationBuilder.CreateIndex(
                name: "IX_Medicos_Email",
                table: "Medicos",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicos_UserId",
                table: "Medicos",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mensajes_ConversacionId_EnviadoUtc",
                table: "Mensajes",
                columns: new[] { "ConversacionId", "EnviadoUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Pacientes_MedicoId_TelefonoE164",
                table: "Pacientes",
                columns: new[] { "MedicoId", "TelefonoE164" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Servicios_MedicoId_Nombre",
                table: "Servicios",
                columns: new[] { "MedicoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_MedicoId_InicioUtc",
                table: "Turnos",
                columns: new[] { "MedicoId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_MedicoId_InicioUtc_Estado",
                table: "Turnos",
                columns: new[] { "MedicoId", "InicioUtc", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_PacienteId",
                table: "Turnos",
                column: "PacienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_ServicioId",
                table: "Turnos",
                column: "ServicioId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnoSincronizaciones_IntegracionCalendarioId",
                table: "TurnoSincronizaciones",
                column: "IntegracionCalendarioId");

            migrationBuilder.CreateIndex(
                name: "IX_TurnoSincronizaciones_TurnoId_IntegracionCalendarioId",
                table: "TurnoSincronizaciones",
                columns: new[] { "TurnoId", "IntegracionCalendarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bloqueos");

            migrationBuilder.DropTable(
                name: "Disponibilidades");

            migrationBuilder.DropTable(
                name: "Mensajes");

            migrationBuilder.DropTable(
                name: "TurnoSincronizaciones");

            migrationBuilder.DropTable(
                name: "Conversaciones");

            migrationBuilder.DropTable(
                name: "Integraciones");

            migrationBuilder.DropTable(
                name: "Turnos");

            migrationBuilder.DropTable(
                name: "Pacientes");

            migrationBuilder.DropTable(
                name: "Servicios");

            migrationBuilder.DropTable(
                name: "Medicos");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
