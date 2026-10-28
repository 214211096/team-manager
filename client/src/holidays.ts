import { useEffect, useState } from 'react';
import { api } from './api/client';
import { toIso } from './attendance';

/** תאריך yyyy-MM-dd → שם החג. החגים מחושבים בשרת לפי הלוח העברי. */
export type HolidayMap = Map<string, string>;

export async function loadHolidays(from: string, to: string): Promise<HolidayMap> {
  const list = await api.holidays(from, to);
  return new Map(list.map((h) => [h.date, h.name]));
}

function parseIso(iso: string): Date {
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/** למה היום הזה לא יום עבודה ("שבת", "ראש השנה"), או null אם הוא יום עבודה. */
export function dayOffLabel(iso: string, holidays: HolidayMap): string | null {
  const holiday = holidays.get(iso);
  if (holiday) return holiday;
  const day = parseIso(iso).getDay();
  return day === 5 ? 'יום שישי' : day === 6 ? 'שבת' : null;
}

/** יום העבודה הקודם (א'-ה', לא חג), בפורמט yyyy-MM-dd. */
export function previousWorkdayIso(holidays: HolidayMap): string {
  const d = new Date();
  do d.setDate(d.getDate() - 1);
  while (dayOffLabel(toIso(d), holidays));
  return toIso(d);
}

/** סיבת יום החופש לתאריך שנבחר במסכי ההזנה; null ביום עבודה או כשהחגים עוד לא נטענו. */
export function useDayOff(iso: string): string | null {
  const [holidays, setHolidays] = useState<HolidayMap>(new Map());
  useEffect(() => {
    loadHolidays(iso, iso)
      .then(setHolidays)
      .catch(() => setHolidays(new Map()));
  }, [iso]);
  return dayOffLabel(iso, holidays);
}
