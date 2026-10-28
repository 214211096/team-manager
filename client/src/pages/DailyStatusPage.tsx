import { useCallback, useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
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
  Typography,
} from '@mui/material';
import { api, ApiError } from '../api/client';
import type { DailyStatusDay, DailyStatusEntry, Rating } from '../api/types';
import { calculateRating, formatDate, ratingColor, ratingLabel, todayIso } from '../attendance';
import { useDayOff } from '../holidays';

const COMPLETED_MAX = 100000;

/** הערך בשדה: ריק, מספר שלם תקין, או שגיאה. */
function parseCompleted(text: string): { value: number | null; error?: string } {
  const t = text.trim();
  if (t === '') return { value: null };
  if (!/^\d+$/.test(t)) return { value: null, error: 'מספר שלם בלבד' };
  const n = Number(t);
  if (n > COMPLETED_MAX) return { value: null, error: `עד ${COMPLETED_MAX}` };
  return { value: n };
}

function toDrafts(day: DailyStatusDay): Record<number, string> {
  return Object.fromEntries(day.rows.map((r) => [r.employeeId, r.completed?.toString() ?? '']));
}

export default function DailyStatusPage() {
  const [date, setDate] = useState(todayIso);
  const dayOff = useDayOff(date);
  const [day, setDay] = useState<DailyStatusDay | null>(null);
  const [drafts, setDrafts] = useState<Record<number, string>>({});
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const apply = useCallback((d: DailyStatusDay) => {
    setDay(d);
    setDrafts(toDrafts(d));
    setDirty(false);
  }, []);

  useEffect(() => {
    setDay(null);
    setMessage(null);
    api
      .dailyStatus(date)
      .then(apply)
      .catch(() => setMessage({ type: 'error', text: 'לא ניתן לטעון את הסטטוס היומי' }));
  }, [date, apply]);

  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const changeDate = (next: string) => {
    if (!next || next === date) return;
    if (dirty && !window.confirm('יש שינויים שלא נשמרו. לעבור לתאריך אחר בלי לשמור?')) return;
    setDate(next);
  };

  if (!day) {
    return message ? <Alert severity="error">{message.text}</Alert> : <CircularProgress aria-label="טוען" />;
  }

  const live = day.rows.map((row) => {
    const parsed = parseCompleted(drafts[row.employeeId] ?? '');
    const calc =
      parsed.value === null
        ? null
        : calculateRating(parsed.value, row.target, day.weakThreshold, day.excellentThreshold);
    return { row, parsed, calc };
  });
  const hasErrors = live.some((l) => l.parsed.error);
  const counts = (['Weak', 'Good', 'Excellent'] as Rating[]).map((r) => ({
    rating: r,
    count: live.filter((l) => l.calc?.rating === r).length,
  }));
  const filled = live.filter((l) => l.calc).length;

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setMessage(null);
    if (hasErrors) {
      setMessage({ type: 'error', text: 'יש לתקן את השדות המסומנים' });
      return;
    }
    const entries: DailyStatusEntry[] = live.map((l) => ({ employeeId: l.row.employeeId, completed: l.parsed.value }));
    setBusy(true);
    try {
      apply(await api.saveDailyStatus(date, entries));
      setMessage({ type: 'success', text: 'הביצוע נשמר' });
    } catch (err) {
      setMessage({ type: 'error', text: err instanceof ApiError ? err.message : 'השמירה נכשלה' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Stack spacing={2} component="form" onSubmit={save} noValidate>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          סטטוס יומי
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
        <Typography variant="body2" color="text.secondary">
          {formatDate(date)} · הוזן ביצוע ל-{filled} מתוך {day.rows.length}
        </Typography>
        {counts.map((c) => (
          <Chip
            key={c.rating}
            size="small"
            variant="outlined"
            color={ratingColor[c.rating]}
            label={`${ratingLabel[c.rating]}: ${c.count}`}
          />
        ))}
        {dirty && <Chip size="small" label="יש שינויים שלא נשמרו" />}
      </Stack>

      {dayOff && <Alert severity="info">{dayOff}: לא יום עבודה. אפשר להזין בכל זאת אם עבדו.</Alert>}
      {day.locked && <Alert severity="info">החודש נעול אחרי שליחה לשכר, אי אפשר לשנות ביצוע.</Alert>}
      {message && <Alert severity={message.type}>{message.text}</Alert>}

      {day.attendanceCount === 0 ? (
        <Alert
          severity="info"
          action={
            <Button component={RouterLink} to="/attendance" color="inherit" size="small">
              למסך הנוכחות
            </Button>
          }
        >
          עוד לא דווחה נוכחות ליום הזה. הסטטוס היומי מוצג רק לעובדות שסומנו "נוכחת".
        </Alert>
      ) : (
        <TableContainer component={Paper}>
          <Table size="small" aria-label="ביצוע יומי">
            <TableHead>
              <TableRow>
                <TableCell>שם</TableCell>
                <TableCell align="center">יעד</TableCell>
                <TableCell align="center" width={140}>
                  ביצוע
                </TableCell>
                <TableCell align="center">אחוז</TableCell>
                <TableCell align="center">סטטוס</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {live.map(({ row, parsed, calc }) => (
                <TableRow key={row.employeeId} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{row.employeeName}</TableCell>
                  <TableCell align="center">
                    {row.target}
                    {row.workPercent < 100 && (
                      <Chip size="small" color="info" variant="outlined" label={`יום חלקי ${row.workPercent}%`} sx={{ ml: 1 }} />
                    )}
                  </TableCell>
                  <TableCell align="center">
                    <TextField
                      size="small"
                      value={drafts[row.employeeId] ?? ''}
                      disabled={day.locked || busy}
                      onChange={(e) => {
                        setDrafts((prev) => ({ ...prev, [row.employeeId]: e.target.value }));
                        setDirty(true);
                        setMessage(null);
                      }}
                      error={!!parsed.error}
                      helperText={parsed.error}
                      slotProps={{
                        htmlInput: {
                          inputMode: 'numeric',
                          'aria-label': `ביצוע של ${row.employeeName}`,
                          style: { textAlign: 'center' },
                        },
                      }}
                    />
                  </TableCell>
                  <TableCell align="center">{calc ? `${calc.percent.toFixed(2)}%` : '—'}</TableCell>
                  <TableCell align="center">
                    {calc ? (
                      <Chip
                        size="small"
                        color={ratingColor[calc.rating]}
                        label={ratingLabel[calc.rating]}
                        sx={{ fontWeight: 700, minWidth: 64 }}
                      />
                    ) : (
                      '—'
                    )}
                  </TableCell>
                </TableRow>
              ))}
              {day.rows.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                    אין עובדות שסומנו "נוכחת" ביום הזה
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {!day.locked && day.rows.length > 0 && (
        <Stack direction="row" sx={{ justifyContent: 'flex-end' }}>
          <Button type="submit" variant="contained" disabled={busy || !dirty}>
            שמירה
          </Button>
        </Stack>
      )}

      <Typography variant="caption" color="text.secondary">
        חלש: פחות מ-{day.weakThreshold}% · טוב: {day.weakThreshold}% עד {day.excellentThreshold}% כולל · מעולה: מעל{' '}
        {day.excellentThreshold}%
      </Typography>
    </Stack>
  );
}
