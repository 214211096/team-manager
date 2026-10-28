import { useCallback, useEffect, useState } from 'react';
import {
  Alert,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import SendIcon from '@mui/icons-material/Send';
import EventNoteIcon from '@mui/icons-material/EventNote';
import { api, ApiError } from '../api/client';
import type { AttendanceDay, AttendanceEntry, AttendanceStatus } from '../api/types';
import { attendanceStatuses, formatDate, formatSentAt, plannedLabel, statusColor, statusLabel, todayIso } from '../attendance';
import { useDayOff } from '../holidays';

interface Draft {
  status: AttendanceStatus | null;
  otherReason: string;
}

const REASON_MAX = 200;

/**
 * ברירת מחדל לעובדת שעוד לא דווחה: היעדרות מלוח השנה אם יש, אחרת "נוכחת" (גם ביום חלקי).
 * כך מסמנים ידנית רק חריגים.
 */
function toDrafts(day: AttendanceDay): Record<number, Draft> {
  return Object.fromEntries(
    day.rows.map((r) => {
      if (r.status) return [r.employeeId, { status: r.status, otherReason: r.otherReason ?? '' }];
      const p = r.planned;
      if (p?.kind === 'Absence' && p.absenceStatus)
        return [r.employeeId, { status: p.absenceStatus, otherReason: p.reason ?? '' }];
      return [r.employeeId, { status: r.isActive ? 'Present' : null, otherReason: '' }];
    }),
  );
}

export default function AttendancePage() {
  const [date, setDate] = useState(todayIso);
  const dayOff = useDayOff(date);
  const [day, setDay] = useState<AttendanceDay | null>(null);
  const [drafts, setDrafts] = useState<Record<number, Draft>>({});
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  // שגיאות שדה מוצגות רק אחרי ניסיון שמירה, לא בזמן ההקלדה הראשונה.
  const [showErrors, setShowErrors] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const apply = useCallback((d: AttendanceDay) => {
    setDay(d);
    setDrafts(toDrafts(d));
    setDirty(false);
    setShowErrors(false);
  }, []);

  useEffect(() => {
    setDay(null);
    setMessage(null);
    api
      .attendance(date)
      .then(apply)
      .catch(() => setMessage({ type: 'error', text: 'לא ניתן לטעון את הנוכחות' }));
  }, [date, apply]);

  // אזהרה לפני סגירת הדף עם שינויים שלא נשמרו.
  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const update = (employeeId: number, patch: Partial<Draft>) => {
    setDrafts((prev) => ({ ...prev, [employeeId]: { ...prev[employeeId], ...patch } }));
    setDirty(true);
    setMessage(null);
  };

  const changeDate = (next: string) => {
    if (!next || next === date) return;
    if (dirty && !window.confirm('יש שינויים שלא נשמרו. לעבור לתאריך אחר בלי לשמור?')) return;
    setDate(next);
  };

  if (!day) {
    return message ? <Alert severity="error">{message.text}</Alert> : <CircularProgress aria-label="טוען" />;
  }

  const reasonMissing = (id: number) => drafts[id]?.status === 'Other' && !drafts[id].otherReason.trim();
  const missing = day.rows.filter((r) => r.isActive && !drafts[r.employeeId]?.status);
  const presentCount = day.rows.filter((r) => drafts[r.employeeId]?.status === 'Present').length;
  const readOnly = day.locked || busy;
  // ברירת המחדל "נוכחת" עוד לא שמורה בשרת, אז גם בלי שינוי של המשתמשת יש מה לשמור.
  const unsaved = dirty || day.rows.some((r) => r.isActive && !r.status);

  const save = async (send: boolean) => {
    setShowErrors(true);
    setMessage(null);
    if (day.rows.some((r) => reasonMissing(r.employeeId))) {
      setMessage({ type: 'error', text: 'בסטטוס "אחר" חובה לכתוב סיבה' });
      return;
    }
    if (send && missing.length > 0) {
      setMessage({ type: 'error', text: `חסר דיווח עבור: ${missing.map((r) => r.employeeName).join(', ')}` });
      return;
    }

    const entries: AttendanceEntry[] = day.rows.flatMap((r) => {
      const d = drafts[r.employeeId];
      return d?.status
        ? [{ employeeId: r.employeeId, status: d.status, otherReason: d.status === 'Other' ? d.otherReason.trim() : null }]
        : [];
    });

    setBusy(true);
    try {
      if (unsaved) apply(await api.saveAttendance(date, entries));
      if (send) {
        apply(await api.sendAttendance(date));
        setMessage({ type: 'success', text: 'הדיווח נשלח למנהלת' });
      } else {
        setMessage({ type: 'success', text: 'הנוכחות נשמרה' });
      }
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'הפעולה נכשלה' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          נוכחות יומית
        </Typography>
        <TextField
          label="תאריך"
          type="date"
          size="small"
          value={date}
          onChange={(e) => changeDate(e.target.value)}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: todayIso() } }}
        />
        {date !== todayIso() && <Button onClick={() => changeDate(todayIso())}>היום</Button>}
      </Stack>

      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        {day.reportSentAt ? (
          <Chip color="success" label={`נשלח למנהלת ${formatSentAt(day.reportSentAt)}`} />
        ) : (
          <Chip color="warning" variant="outlined" label="עוד לא נשלח למנהלת" />
        )}
        <Typography variant="body2" color="text.secondary">
          {formatDate(date)} · נוכחות {presentCount} מתוך {day.rows.length}
        </Typography>
        {dirty && <Chip size="small" label="יש שינויים שלא נשמרו" />}
      </Stack>

      {dayOff && <Alert severity="info">{dayOff}: לא יום עבודה. אפשר להזין בכל זאת אם עבדו.</Alert>}
      {day.locked && <Alert severity="info">החודש נעול אחרי שליחה לשכר, אי אפשר לשנות נוכחות.</Alert>}
      {message && <Alert severity={message.type}>{message.text}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="נוכחות עובדות">
          <TableHead>
            <TableRow>
              <TableCell>שם</TableCell>
              <TableCell>סטטוס</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {day.rows.map((row) => {
              const d = drafts[row.employeeId];
              const reasonError = showErrors && reasonMissing(row.employeeId);
              return (
                <TableRow key={row.employeeId} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>
                    {row.employeeName}
                    {!row.isActive && <Chip size="small" label="לא פעילה" variant="outlined" sx={{ ml: 1 }} />}
                    {row.planned && (
                      <Chip
                        size="small"
                        variant="outlined"
                        color={row.planned.kind === 'PartialDay' ? 'info' : 'default'}
                        icon={<EventNoteIcon />}
                        label={plannedLabel(row.planned)}
                        title="מלוח השנה"
                        sx={{ ml: 1 }}
                      />
                    )}
                  </TableCell>
                  <TableCell>
                    <Stack spacing={1} sx={{ py: 0.5 }}>
                      <ToggleButtonGroup
                        exclusive
                        size="small"
                        value={d?.status ?? null}
                        disabled={readOnly}
                        aria-label={`סטטוס של ${row.employeeName}`}
                        // לחיצה על הכפתור שכבר נבחר מחזירה null; משאירים את הבחירה.
                        onChange={(_, v: AttendanceStatus | null) => v && update(row.employeeId, { status: v })}
                        sx={{ flexWrap: 'wrap' }}
                      >
                        {attendanceStatuses.map((s) => (
                          <ToggleButton
                            key={s}
                            value={s}
                            sx={{
                              px: 2,
                              '&.Mui-selected, &.Mui-selected:hover': {
                                bgcolor: `${statusColor[s]}.main`,
                                color: `${statusColor[s]}.contrastText`,
                                fontWeight: 700,
                              },
                            }}
                          >
                            {statusLabel[s]}
                          </ToggleButton>
                        ))}
                      </ToggleButtonGroup>
                      {d?.status === 'Other' && (
                        <TextField
                          size="small"
                          label="סיבה"
                          required
                          value={d.otherReason}
                          disabled={readOnly}
                          onChange={(e) => update(row.employeeId, { otherReason: e.target.value })}
                          error={reasonError}
                          helperText={reasonError ? 'חובה לכתוב סיבה' : 'סיבה כללית בלבד, בלי פרטים רפואיים או אישיים'}
                          slotProps={{ htmlInput: { maxLength: REASON_MAX } }}
                          sx={{ maxWidth: 420 }}
                        />
                      )}
                    </Stack>
                  </TableCell>
                </TableRow>
              );
            })}
            {day.rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={2} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                  אין עובדות פעילות
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {!day.locked && day.rows.length > 0 && (
        <Stack direction="row" sx={{ justifyContent: 'flex-end', gap: 1 }}>
          <Button variant="outlined" onClick={() => save(false)} disabled={busy || !unsaved}>
            שמירה
          </Button>
          <Button variant="contained" startIcon={<SendIcon />} onClick={() => save(true)} disabled={busy}>
            {day.reportSentAt ? 'שליחת עדכון למנהלת' : 'שליחה למנהלת'}
          </Button>
        </Stack>
      )}
    </Stack>
  );
}
