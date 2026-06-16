"use client";

import { useState } from "react";
import Button from "@/components/ui/button";
import Input from "@/components/ui/input";
import Label from "@/components/ui/label";

export function LoginForm() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleGoogleSignIn = () => {
    // Obtener la URL del backend desde las variables de entorno
    const backendUrl = process.env.NEXT_PUBLIC_BACKEND_URL || "http://localhost:10000";
    // Redirigir al endpoint de OAuth de Google en el backend
    window.location.href = `${backendUrl}/login/google`;
  };

  return (
    <div className="w-full max-w-md bg-card border border-border rounded-2xl p-8 space-y-6">
      <div className="text-center space-y-3">
        <h1 className="text-3xl font-bold text-foreground">Iniciar sesión</h1>
        <p className="text-muted-foreground text-sm">Ingresa a tu cuenta de Alfred</p>
      </div>

      <Button
        type="button"
        onClick={handleGoogleSignIn}
        className="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-medium"
        size="lg"
      >
        {/* ícono */}
        Iniciar sesión con Google
      </Button>

      <div className="relative">
        <div className="absolute inset-0 flex items-center">
          <div className="w-full border-t border-border" />
        </div>
      </div>

      {/* 👉 submit directo al API */}
      <form action="/api/session" method="post" className="space-y-4">
        <div className="space-y-2">
          <Label htmlFor="email" className="text-muted-foreground text-sm">
            Correo electrónico
          </Label>
          <Input
            id="email"
            name="email"
            type="email"
            placeholder="tu@email.com"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            className="bg-background border-border text-foreground placeholder:text-muted-foreground/50"
            required
          />
        </div>

        <div className="space-y-2">
          <Label htmlFor="password" className="text-muted-foreground text-sm">
            Contraseña
          </Label>
          <Input
            id="password"
            name="password"
            type="password"
            placeholder="Tu contraseña"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="bg-background border-border text-foreground placeholder:text-muted-foreground/50"
            required
          />
        </div>

        <Button type="submit" className="w-full bg-primary hover:bg-primary/90 text-primary-foreground font-medium" size="lg">
          Iniciar sesión
        </Button>
      </form>

      <div className="space-y-3 text-center">
        <button type="button" className="w-full text-sm text-muted-foreground hover:text-foreground transition-colors border border-border rounded-lg py-2.5">
          Recuperar contraseña
        </button>
        <button type="button" className="w-full text-sm text-muted-foreground hover:text-foreground transition-colors border border-border rounded-lg py-2.5">
          ¿No tienes una cuenta? Regístrate
        </button>
      </div>

      <div className="text-center pt-2">
        <a href="/" className="text-xs text-muted-foreground hover:text-foreground transition-colors">
          ← Volver al inicio
        </a>
      </div>
    </div>
  );
}
