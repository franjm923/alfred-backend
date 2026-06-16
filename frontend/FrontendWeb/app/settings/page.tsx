// app/settings/page.tsx
"use client";

import { useState, useEffect } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Calendar, CheckCircle2, XCircle } from "lucide-react";

interface CalendarStatus {
  connected: boolean;
  provider: string;
  connectedAt: string | null;
  email: string | null;
}

export default function SettingsPage() {
  const [calendarStatus, setCalendarStatus] = useState<CalendarStatus | null>(null);
  const [loading, setLoading] = useState(true);

  const backendUrl = process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:5000';

  useEffect(() => {
    checkCalendarStatus();
  }, []);

  const checkCalendarStatus = async () => {
    try {
      const response = await fetch(`${backendUrl}/api/calendar/status`, {
        credentials: 'include',
      });

      if (response.ok) {
        const data = await response.json();
        setCalendarStatus(data);
      }
    } catch (error) {
      console.error('Error checking calendar status:', error);
    } finally {
      setLoading(false);
    }
  };

  const connectCalendar = () => {
    window.location.href = `${backendUrl}/calendar/connect`;
  };

  return (
    <main className="container mx-auto max-w-4xl px-4 py-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Configuración</h1>
        <p className="text-muted-foreground mt-2">
          Administra tus integraciones y preferencias
        </p>
      </div>

      {/* Integración Google Calendar */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-3">
            <Calendar className="h-6 w-6 text-primary" />
            <div>
              <CardTitle>Google Calendar</CardTitle>
              <CardDescription>
                Sincroniza tu disponibilidad automáticamente
              </CardDescription>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? (
            <div className="text-sm text-muted-foreground">Cargando...</div>
          ) : calendarStatus?.connected ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm">
                <CheckCircle2 className="h-5 w-5 text-green-500" />
                <span className="font-medium">Conectado exitosamente</span>
              </div>
              <div className="text-sm text-muted-foreground space-y-1">
                <p>📧 {calendarStatus.email}</p>
                <p>
                  🕐 Conectado el{" "}
                  {calendarStatus.connectedAt
                    ? new Date(calendarStatus.connectedAt).toLocaleDateString("es-AR", {
                        day: "2-digit",
                        month: "long",
                        year: "numeric",
                      })
                    : "N/A"}
                </p>
              </div>
              <p className="text-sm text-muted-foreground mt-3">
                Alfred puede consultar tu disponibilidad en tiempo real y ofrecer turnos solo cuando estés libre.
              </p>
              <Button
                variant="outline"
                onClick={connectCalendar}
                className="mt-2"
              >
                Reconectar
              </Button>
            </div>
          ) : (
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-sm">
                <XCircle className="h-5 w-5 text-muted-foreground" />
                <span className="font-medium">No conectado</span>
              </div>
              <p className="text-sm text-muted-foreground">
                Conecta tu Google Calendar para que Alfred pueda consultar tu disponibilidad automáticamente.
                Los turnos se ofrecerán solo en horarios donde no tengas eventos programados.
              </p>
              <Button onClick={connectCalendar} className="mt-2">
                <Calendar className="h-4 w-4 mr-2" />
                Conectar Google Calendar
              </Button>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Configuración de horarios */}
      <Card>
        <CardHeader>
          <CardTitle>Horarios de atención</CardTitle>
          <CardDescription>
            Define tu horario laboral predeterminado
          </CardDescription>
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          <p>
            📅 <strong>Días:</strong> Lunes a Viernes
          </p>
          <p className="mt-1">
            🕐 <strong>Horario:</strong> 9:00 AM - 6:00 PM
          </p>
          <p className="mt-1">
            ⏱️ <strong>Duración por turno:</strong> 30 minutos
          </p>
          <p className="text-xs mt-3 text-muted-foreground">
            * Los horarios se toman de tu Google Calendar cuando está conectado
          </p>
        </CardContent>
      </Card>
    </main>
  );
}
