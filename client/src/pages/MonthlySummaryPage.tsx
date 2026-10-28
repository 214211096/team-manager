import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
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
  TableFooter,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import DownloadIcon from '@mui/icons-material/Download';
import SendIcon from '@mui/icons-material/Send';
import { api, ApiError } from '../api/client';
import type { MonthlySummary, MonthlySummaryRow } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { formatSentAt, todayIso } from '../attendance';

const currentMonth = () => todayIso().slice(0, 7);

const pct = (p: number | null) => (p === null ? '—' : `${p.toFixed(2)}%`);

type NumericKey = Exclude<keyof MonthlySummaryRow, 'employeeId' | 'employeeName' | 'isActive' | 'averagePercent'>;

const columns: { key: NumericKey; label: string }[] = [
  { key: 'presentDays', label: 'ימי נוכחות' },
  { key: 'partialDays', label: 'מתוכם חלקיים' },
  { key: 'vacationDays', label: 'חופשה' },
  { key: 'sickDays', label: 'מחלה' },
  { key: 'otherDays', label: 'אחר' },
  { key: 'weakDays', label: 'חלש' },
  { key: 'goodDays', label: 'טוב' },
  { key: 'excellentDays', label: 'מעולה' },
  { key: 'daysWithoutOutput', label: 'בלי ביצוע' },
];

export default function MonthlySummaryPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  // הקישור במייל מגיע עם ?month=yyyy-MM
  const month = /^\d{4}-\d{2}$/.test(params.get('month') ?? '') ? params.get('month')! : currentMonth();
  const [year, mon] = month.split('-').map(Number);

  const [summary, setSummary] = useState<MonthlySummary | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    setSummary(null);
    setMessage(null);
    api
      .monthlySummary(year, mon)
      .then(setSummary)
      .catch((e) => setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'לא ניתן לטעון את הסיכום' }));
  }, [year, mon]);

  const send = async () => {
    setBusy(true);
    setMessage(null);
    try {
      setSummary(await api.sendMonthlySummary(year, mon));
      setMessage({ type: 'success', text: 'הסיכום נשלח במייל' });
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'השליחה נכשלה' });
    } finally {
      setBusy(false);
    }
  };

  const missing = summary?.rows.reduce((sum, r) => sum + r.daysWithoutOutput, 0) ?? 0;
  const monthName = new Date(year, mon - 1, 1).toLocaleDateString('he-IL', { month: 'long', year: 'numeric' });

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          סיכום חודשי
        </Typography>
        <TextField
          label="חודש"
          type="month"
          size="small"
          value={month}
          onChange={(e) => e.target.value && setParams({ month: e.target.value })}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: currentMonth() } }}
        />
      </Stack>

      {message && <Alert severity={message.type}>{message.text}</Alert>}

      {!summary ? (
        !message && <CircularProgress aria-label="טוען" />
      ) : (
        <>
          <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
            <Typography sx={{ fontWeight: 500 }}>{monthName}</Typography>
            <Typography color="text.secondary">· ממוצע צוות: {pct(summary.teamAveragePercent)}</Typography>
            {summary.locked && <Chip size="small" label="נעול" />}
            {summary.sentAt ? (
              <Chip size="small" color="success" label={`נשלח במייל ${formatSentAt(summary.sentAt)}`} />
            ) : (
              <Chip size="small" variant="outlined" label="עוד לא נשלח במייל" />
            )}
            <Stack direction="row" sx={{ flexGrow: 1, justifyContent: 'flex-end', gap: 1 }}>
              <Button
                variant="outlined"
                startIcon={<DownloadIcon />}
                component="a"
                href={api.monthlySummaryExcelUrl(year, mon)}
                download
              >
                הורדת אקסל
              </Button>
              {user?.role === 'TeamLead' && (
                <Button variant="contained" startIcon={<SendIcon />} onClick={send} disabled={busy}>
                  {summary.sentAt ? 'שליחה שוב במייל' : 'שליחה במייל'}
                </Button>
              )}
            </Stack>
          </Stack>

          {missing > 0 && (
            <Alert severity="warning">
              {missing} ימי נוכחות בלי ביצוע. הם לא נכללים בממוצע; אפשר להשלים אותם במסך הסטטוס היומי.
            </Alert>
          )}

          <TableContainer component={Paper}>
            <Table size="small" aria-label={`סיכום ${monthName}`}>
              <TableHead>
                <TableRow>
                  <TableCell>שם</TableCell>
                  {columns.map((c) => (
                    <TableCell key={c.key} align="center">
                      {c.label}
                    </TableCell>
                  ))}
                  <TableCell align="center">ממוצע עמידה</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {summary.rows.map((r) => (
                  <TableRow key={r.employeeId} hover>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>
                      {r.employeeName}
                      {!r.isActive && <Chip size="small" label="לא פעילה" variant="outlined" sx={{ ml: 1 }} />}
                    </TableCell>
                    {columns.map((c) => (
                      <TableCell key={c.key} align="center" sx={{ color: r[c.key] === 0 ? 'text.disabled' : undefined }}>
                        {r[c.key]}
                      </TableCell>
                    ))}
                    <TableCell align="center" sx={{ fontWeight: 700 }}>
                      {pct(r.averagePercent)}
                    </TableCell>
                  </TableRow>
                ))}
                {summary.rows.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={columns.length + 2} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                      אין נתונים לחודש הזה
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
              {summary.rows.length > 0 && (
                <TableFooter>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>סה״כ צוות</TableCell>
                    {columns.map((c) => (
                      <TableCell key={c.key} align="center" sx={{ fontWeight: 700 }}>
                        {summary.rows.reduce((sum, r) => sum + r[c.key], 0)}
                      </TableCell>
                    ))}
                    <TableCell align="center" sx={{ fontWeight: 700 }}>
                      {pct(summary.teamAveragePercent)}
                    </TableCell>
                  </TableRow>
                </TableFooter>
              )}
            </Table>
          </TableContainer>

          <Typography variant="caption" color="text.secondary">
            הסיכום של החודש הקודם נשלח אוטומטית במייל ב-1 לכל חודש ב-08:00, עם קובץ אקסל. הממוצע מחושב רק מימים
            שבהם העובדת נכחה והוזן ביצוע.
          </Typography>
        </>
      )}
    </Stack>
  );
}
