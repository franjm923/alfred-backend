
"use client"; 
import { Calendar as CalendarIcon } from "lucide-react";
import { cn } from "@/lib/utils";

type DayStat = { date: string; count: number }; // ISO YYYY-MM-DD
type Props = {
  selectedDate?: Date;
  onSelect?: (d: Date) => void;
  stats?: DayStat[];
};

function startOfWeek(date = new Date()) {
  const d = new Date(date);
  const day = (d.getDay() + 6) % 7; // lunes=0
  d.setDate(d.getDate() - day);
  d.setHours(0, 0, 0, 0);
  return d;
}

function toISODate(d: Date) {
  const z = new Date(d);
  z.setHours(0, 0, 0, 0);
  return z.toISOString().slice(0, 10);
}

export default function WeeklyCalendar({ selectedDate = new Date(), onSelect, stats = [] }: Props) {
  const weekStart = startOfWeek(selectedDate);
  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(weekStart);
    d.setDate(weekStart.getDate() + i);
    return d;
  });

  const counts = new Map(stats.map(s => [s.date, s.count]));

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between text-sm text-muted-foreground">
        <div className="flex items-center gap-2">
          <CalendarIcon className="h-4 w-4" />
          <span>
            {selectedDate.toLocaleDateString("es-AR", { month: "long", year: "numeric" })}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-7 gap-3">
        {days.map((d, idx) => {
          const isSelected = toISODate(d) === toISODate(selectedDate);
          const count = counts.get(toISODate(d)) ?? 0;
          const weekday = d.toLocaleDateString("es-AR", { weekday: "short" }).replace(".", "");
          const dayNum = d.getDate();

          return (
            <button
              key={idx}
              onClick={() => onSelect?.(d)}
              className={cn(
                "rounded-xl border px-3 py-4 text-left transition-colors",
                "bg-card text-card-foreground hover:bg-secondary hover:text-secondary-foreground",
                isSelected && "ring-2 ring-ring ring-offset-2 ring-offset-background"
              )}
            >
              <div className="text-sm text-muted-foreground">{weekday.charAt(0).toUpperCase() + weekday.slice(1)}</div>
              <div className="text-2xl font-semibold">{dayNum}</div>
              <div className={cn("mt-1 text-xs", isSelected ? "font-semibold" : "text-muted-foreground")}>
                {count} {count === 1 ? "turno" : "turnos"}
              </div>
            </button>
          );
        })}
      </div>
    </div>
  );
}
