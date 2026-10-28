import type {
  AppSettings,
  AssistantTurn,
  AttendanceDay,
  AttendanceEntry,
  BonusEntry,
  BonusMonth,
  CurrentUser,
  DailyStatusDay,
  DailyStatusEntry,
  Employee,
  EmployeeUpsert,
  MonthlySummary,
  ScheduleEntry,
  ScheduleEntryInput,
} from './types';

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public fieldErrors: Record<string, string[]> = {},
  ) {
    super(message);
  }
}

// מאזין שה-AuthContext רושם, כדי להחזיר למסך כניסה כשהעוגייה פגה.
let onUnauthorized: (() => void) | null = null;
export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler;
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    credentials: 'same-origin',
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (res.status === 401 && !url.endsWith('/auth/login')) onUnauthorized?.();

  if (!res.ok) {
    // ASP.NET מחזיר ProblemDetails: title כללי ו-errors לפי שדה.
    const problem = await res.json().catch(() => null);
    const fieldMessages = Object.values<string[]>(problem?.errors ?? {}).flat();
    const message = fieldMessages.length
      ? fieldMessages.join(' · ')
      : problem?.title && res.status !== 500
        ? problem.title
        : res.status === 403
          ? 'אין הרשאה לפעולה זו'
          : res.status >= 500
            ? 'שגיאת שרת, נסי שוב'
            : 'הבקשה נכשלה';
    throw new ApiError(res.status, message, problem?.errors ?? {});
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

export const api = {
  login: (username: string, password: string) =>
    request<CurrentUser>('POST', '/api/auth/login', { username, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  me: () => request<CurrentUser>('GET', '/api/auth/me'),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('POST', '/api/auth/change-password', { currentPassword, newPassword }),

  employees: (includeInactive = false) =>
    request<Employee[]>('GET', `/api/employees?includeInactive=${includeInactive}`),
  createEmployee: (data: EmployeeUpsert) => request<Employee>('POST', '/api/employees', data),
  updateEmployee: (id: number, data: EmployeeUpsert) =>
    request<Employee>('PUT', `/api/employees/${id}`, data),

  attendance: (date: string) => request<AttendanceDay>('GET', `/api/attendance/${date}`),
  saveAttendance: (date: string, entries: AttendanceEntry[]) =>
    request<AttendanceDay>('PUT', `/api/attendance/${date}`, { entries }),
  sendAttendance: (date: string) => request<AttendanceDay>('POST', `/api/attendance/${date}/send`),

  dailyStatus: (date: string) => request<DailyStatusDay>('GET', `/api/daily-status/${date}`),
  saveDailyStatus: (date: string, entries: DailyStatusEntry[]) =>
    request<DailyStatusDay>('PUT', `/api/daily-status/${date}`, { entries }),

  schedule: (from: string, to: string) => request<ScheduleEntry[]>('GET', `/api/schedule?from=${from}&to=${to}`),
  createScheduleEntry: (data: ScheduleEntryInput) => request<ScheduleEntry>('POST', '/api/schedule', data),
  updateScheduleEntry: (id: number, data: ScheduleEntryInput) =>
    request<ScheduleEntry>('PUT', `/api/schedule/${id}`, data),
  deleteScheduleEntry: (id: number) => request<void>('DELETE', `/api/schedule/${id}`),

  monthlySummary: (year: number, month: number) =>
    request<MonthlySummary>('GET', `/api/monthly-summary/${year}/${month}`),
  /** קישור ישיר להורדה; העוגייה נשלחת אוטומטית כי זו אותה כתובת. */
  monthlySummaryExcelUrl: (year: number, month: number) => `/api/monthly-summary/${year}/${month}/excel`,
  sendMonthlySummary: (year: number, month: number) =>
    request<MonthlySummary>('POST', `/api/monthly-summary/${year}/${month}/send`),

  bonuses: (year: number, month: number) => request<BonusMonth>('GET', `/api/bonuses/${year}/${month}`),
  saveBonuses: (year: number, month: number, entries: BonusEntry[]) =>
    request<BonusMonth>('PUT', `/api/bonuses/${year}/${month}`, { entries }),
  sendBonuses: (year: number, month: number) => request<BonusMonth>('POST', `/api/bonuses/${year}/${month}/send`),
  bonusesExcelUrl: (year: number, month: number) => `/api/bonuses/${year}/${month}/excel`,

  assistantStatus: () => request<{ enabled: boolean }>('GET', '/api/assistant/status'),
  assistantChat: (messages: AssistantTurn[]) =>
    request<{ answer: string }>('POST', '/api/assistant/chat', { messages }),

  settings: () => request<AppSettings>('GET', '/api/settings'),
  saveSettings: (data: AppSettings) => request<AppSettings>('PUT', '/api/settings', data),
};
