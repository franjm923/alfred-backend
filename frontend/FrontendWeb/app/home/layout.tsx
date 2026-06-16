// app/home/layout.tsx
import Header from "@/components/header";

export default function HomeLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen">
      <Header />
      {children}
    </div>
  );
}
