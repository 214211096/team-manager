export type UserRole = 'TeamLead' | 'Manager';

export interface CurrentUser {
  id: number;
  username: string;
  displayName: string | null;
  email: string;
  role: UserRole;
}

export interface Employee {
  id: number;
  name: string;
  dailyTarget: number;
  isActive: boolean;
  createdAt: string;
}

export interface EmployeeUpsert {
  name: string;
  dailyTarget: number;
  isActive: boolean;
}

export type AttendanceStatus = 'Present' | 'Vacation' | 'Sick' | 'Other';

export type ScheduleKind = 'Absence' | 'PartialDay';

/** מה מתוכנן בלוח השנה לעובדת ביום מסוים */
export interface PlannedDay {
  kind: ScheduleKind;
  absenceStatus: AttendanceStatus | null;
  reason: string | null;
  workPercent: number | null;
}

export interface AttendanceRow {
  employeeId: number;
  employeeName: string;
  isActive: boolean;
  status: AttendanceStatus | null;
  otherReason: string | null;
  planned: PlannedDay | null;
}

export interface AttendanceDay {
  /** yyyy-MM-dd */
  date: string;
  locked: boolean;
  /** מועד השליחה האחרונה למנהלת (ISO, UTC), או null אם לא נשלח */
  reportSentAt: string | null;
  rows: AttendanceRow[];
}

export interface AttendanceEntry {
  employeeId: number;
  status: AttendanceStatus;
  otherReason: string | null;
}

export type Rating = 'Weak' | 'Good' | 'Excellent';

export interface DailyStatusRow {
  employeeId: number;
  employeeName: string;
  /** היעד של אותו יום (נשמר בהזנה הראשונה) */
  target: number;
  completed: number | null;
  percent: number | null;
  rating: Rating | null;
  /** אחוז העבודה באותו יום (100 = יום מלא; פחות = יום חלקי מלוח השנה) */
  workPercent: number;
}

export interface DailyStatusDay {
  date: string;
  locked: boolean;
  weakThreshold: number;
  excellentThreshold: number;
  /** כמה עובדות דווחו בנוכחות באותו יום; 0 = עוד לא דווחה נוכחות */
  attendanceCount: number;
  rows: DailyStatusRow[];
}

export interface DailyStatusEntry {
  employeeId: number;
  completed: number | null;
}

export interface ScheduleEntry {
  id: number;
  employeeId: number;
  employeeName: string;
  /** yyyy-MM-dd */
  startDate: string;
  /** yyyy-MM-dd, כולל */
  endDate: string;
  kind: ScheduleKind;
  absenceStatus: AttendanceStatus | null;
  reason: string | null;
  workPercent: number | null;
}

export type ScheduleEntryInput = Omit<ScheduleEntry, 'id' | 'employeeName'>;

export interface MonthlySummaryRow {
  employeeId: number;
  employeeName: string;
  isActive: boolean;
  presentDays: number;
  partialDays: number;
  vacationDays: number;
  sickDays: number;
  otherDays: number;
  weakDays: number;
  goodDays: number;
  excellentDays: number;
  daysWithoutOutput: number;
  averagePercent: number | null;
}

export interface MonthlySummary {
  year: number;
  month: number;
  locked: boolean;
  /** שליחה אחרונה במייל (ISO, UTC), או null */
  sentAt: string | null;
  teamAveragePercent: number | null;
  rows: MonthlySummaryRow[];
}

export interface BonusRow {
  summary: MonthlySummaryRow;
  amount: number | null;
  note: string | null;
}

export interface BonusMonth {
  year: number;
  month: number;
  /** החודש הסתיים ואפשר לשלוח לשכר */
  ended: boolean;
  locked: boolean;
  /** ISO, UTC */
  lockedAt: string | null;
  lockedBy: string | null;
  bonusCap: number;
  total: number;
  rows: BonusRow[];
}

export interface BonusEntry {
  employeeId: number;
  amount: number | null;
  note: string | null;
}

export interface AssistantTurn {
  role: 'user' | 'assistant';
  content: string;
}

export interface AppSettings {
  managerEmail: string;
  reminderTime: string;
  weakThreshold: number;
  excellentThreshold: number;
  bonusCap: number;
  /** ימים (חגים) בפורמט yyyy-MM-dd שבהם לא נשלחת תזכורת */
  noReminderDates: string[];
}
