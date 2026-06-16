// app/page.tsx
"use client";

import { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export default function Home() {
  const [selectedDate, setSelectedDate] = useState(new Date());

  return (
    <main className="container mx-auto max-w-7xl px-4 py-8 space-y-6">
      <h1 className="text-2xl font-bold">Bienvenido a Alfred</h1>
      <Card>
        <CardHeader>
          <CardTitle>Dashboard</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">
            Tu dashboard está listo. Los turnos aparecerán aquí cuando los pacientes agenden por WhatsApp.
          </p>
        </CardContent>
      </Card>
    </main>
  );
}
