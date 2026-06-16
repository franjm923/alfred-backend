// app/(auth)/login/page.tsx
import Button from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { LoginForm } from "@/components/login-form";

export default function LoginPage() {
  return (
    <main className="min-h-[calc(100vh-0px)] grid place-items-center px-4">
      <LoginForm />
    </main>
  );
}
