// components/appointments-list.tsx
import { Calendar, Clock, Mail, Phone } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

export type Appointment = {
  id: string;
  paciente: string;
  edad?: number;
  servicio?: string;
  inicio?: string; // ISO
  telefono?: string;
  email?: string;
  estado?: "Confirmado" | "Pendiente" | "Cancelado" | "NoAsistio" | "Completado";
};

type Props = {
  title: string;
  items?: Appointment[];
  emptyHint?: string;
  variant?: "default" | "compact";
};

export default function AppointmentList({
  title,
  items = [],
  emptyHint = "Sin turnos.",
  variant = "default",
}: Props) {
  const isEmpty = items.length === 0;

  const statusToVariant: Record<string, string> = {
  Confirmado: "primary", // antes "success"
  Pendiente: "warning",
  Cancelado: "destructive",
  NoAsistio: "destructive",
  Completado: "default",
};

  if (variant === "compact") {
    return (
      <div className="space-y-3">
        {title ? <h3 className="text-lg font-semibold">{title}</h3> : null}
        {isEmpty ? (
          <div className="text-sm text-muted-foreground">{emptyHint}</div>
        ) : (
          items.map((a) => (
            <div key={a.id} className="rounded-xl border bg-card p-4">
              <div className="flex items-center justify-between">
                <div className="font-medium">{a.paciente}</div>
                {a.estado ? <Badge variant={statusToVariant[a.estado] ?? "default"}>{a.estado}</Badge> : null}
              </div>
              <div className="mt-2 space-y-1 text-sm text-muted-foreground">
                {a.inicio && (
                  <div className="flex items-center gap-2">
                    <Calendar className="h-4 w-4" />
                    {new Date(a.inicio).toLocaleString("es-AR", { day: "2-digit", month: "short", hour: "2-digit", minute: "2-digit" })}
                  </div>
                )}
                {a.telefono && (
                  <div className="flex items-center gap-2">
                    <Phone className="h-4 w-4" /> {a.telefono}
                  </div>
                )}
                {a.email && (
                  <div className="flex items-center gap-2">
                    <Mail className="h-4 w-4" /> {a.email}
                  </div>
                )}
              </div>
              {a.servicio && <div className="mt-3 text-sm">{a.servicio}</div>}
            </div>
          ))
        )}
      </div>
    );
  }

  // variante "default" (lista grande)
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        {isEmpty ? (
          <div className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
            {emptyHint}
          </div>
        ) : (
          items.map((a) => (
            <div
              key={a.id}
              className={cn(
                "flex items-center justify-between rounded-xl border bg-card p-4",
                "hover:bg-secondary transition-colors"
              )}
            >
              <div className="flex items-center gap-4">
                <div className="flex items-center gap-2 text-sm text-muted-foreground">
                  <Clock className="h-4 w-4" />
                  {a.inicio
                    ? new Date(a.inicio).toLocaleTimeString("es-AR", { hour: "2-digit", minute: "2-digit" })
                    : "--:--"}
                </div>
                <div>
                  <div className="font-medium">{a.paciente}</div>
                  {a.servicio && <div className="text-sm text-muted-foreground">{a.servicio}</div>}
                </div>
              </div>
              {a.estado ? <Badge variant={statusToVariant[a.estado] ?? "default"}>{a.estado}</Badge> : null}
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
