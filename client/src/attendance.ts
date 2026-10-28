import type { AttendanceStatus, PlannedDay, Rating } from './api/types';

export const attendanceStatuses: AttendanceStatus[] = ['Present', 'Vacation', 'Sick', 'Other'];

export const statusLabel: Record<AttendanceStatus, string> = {
  Present: 'נוכחת',
  Vacation: 'חופשה',
  Sick: 'מחלה',
  Other: 'אחר',
};

/** צבע הסטטוס אחרי בחירה: ירוק לנוכחות, צהוב למחלה, אדום להיעדרות. */
export const statusColor: Record<AttendanceStatus, 'success' | 'warning' | 'error'> = {
  Present: 'success',
  Vacation: 'error',
  Sick: 'warning',
  Other: 'error',
};

const pad = (n: number) => String(n).padStart(2, '0');

/** התאריך המקומי בפורמט yyyy-MM-dd (לא toISOString, שמחזיר תאריך UTC). */
export function todayIso(): string {
  return toIso(new Date());
}

/** dd/mm/yyyy, לפי האפיון. מקבל yyyy-MM-dd או Date. */
export function formatDate(value: string | Date): string {
  const [y, m, d] =
    typeof value === 'string'
      ? value.split('-').map(Number)
      : [value.getFullYear(), value.getMonth() + 1, value.getDate()];
  return `${pad(d)}/${pad(m)}/${y}`;
}

/** dd/mm hh:mm בשעה מקומית. */
export function formatSentAt(iso: string): string {
  const d = new Date(iso);
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export const ratingLabel: Record<Rating, string> = {
  Weak: 'חלש',
  Good: 'טוב',
  Excellent: 'מעולה',
};

export const ratingColor: Record<Rating, 'error' | 'info' | 'success'> = {
  Weak: 'error',
  Good: 'info',
  Excellent: 'success',
};

/** אותו חישוב כמו StatusCalculator בשרת, לתצוגה מיידית בזמן ההקלדה. השרת קובע את מה שנשמר. */
export function calculateRating(completed: number, target: number, weakBelow: number, excellentAbove: number) {
  const percent = (completed * 100) / target;
  const rating: Rating = percent < weakBelow ? 'Weak' : percent <= excellentAbove ? 'Good' : 'Excellent';
  return { percent: Math.round(percent * 100) / 100, rating };
}

/** תיאור קצר של רישום מלוח השנה: "חופשה", "אחר – השתלמות", "יום חלקי 50%". */
export function plannedLabel(p: PlannedDay): string {
  if (p.kind === 'PartialDay') return `יום חלקי ${p.workPercent}%`;
  const label = p.absenceStatus ? statusLabel[p.absenceStatus] : 'היעדרות';
  return p.reason ? `${label} – ${p.reason}` : label;
}

export function toIso(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
