// app/settings/layout.tsx
import Header from "@/components/header";

export default function SettingsLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <Header />
      {children}
    </div>
  );
}
