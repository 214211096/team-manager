import { useCallback, useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Alert,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
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
import LockIcon from '@mui/icons-material/Lock';
import SendIcon from '@mui/icons-material/Send';
import { api, ApiError } from '../api/client';
import type { BonusEntry, BonusMonth } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { formatSentAt, toIso } from '../attendance';

interface Draft {
  amount: string;
  note: string;
}

const NOTE_MAX = 500;

/** ברירת המחדל היא החודש הקודם: הבונוס משולם על חודש שהסתיים. */
function previousMonth(): string {
  const d = new Date();
  return toIso(new Date(d.getFullYear(), d.getMonth() - 1, 1)).slice(0, 7);
}

const shekel = (n: number) => `${n.toLocaleString('he-IL', { maximumFractionDigits: 2 })} ש״ח`;

function parseAmount(text: string, cap: number): { value: number | null; error?: string } {
  const t = text.trim();
  if (t === '') return { value: null };
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return { value: null, error: 'מספר עם עד שתי ספרות אחרי הנקודה' };
  const n = Number(t);
  if (n > cap) return { value: null, error: `עד ${cap} ש״ח` };
  return { value: n };
}

function toDrafts(b: BonusMonth): Record<number, Draft> {
  return Object.fromEntries(
    b.rows.map((r) => [r.summary.employeeId, { amount: r.amount?.toString() ?? '', note: r.note ?? '' }]),
  );
}

export default function BonusesPage() {
  const { user } = useAuth();
  const [params, setParams] = useSearchParams();
  const month = /^\d{4}-\d{2}$/.test(params.get('month') ?? '') ? params.get('month')! : previousMonth();
  const [year, mon] = month.split('-').map(Number);

  const [data, setData] = useState<BonusMonth | null>(null);
  const [drafts, setDrafts] = useState<Record<number, Draft>>({});
  const [dirty, setDirty] = useState(false);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  const apply = useCallback((b: BonusMonth) => {
    setData(b);
    setDrafts(toDrafts(b));
    setDirty(false);
  }, []);

  useEffect(() => {
    setData(null);
    setMessage(null);
    api
      .bonuses(year, mon)
      .then(apply)
      .catch((e) => setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'לא ניתן לטעון את הבונוסים' }));
  }, [year, mon, apply]);

  useEffect(() => {
    if (!dirty) return;
    const warn = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [dirty]);

  const changeMonth = (next: string) => {
    if (!next || next === month) return;
    if (dirty && !window.confirm('יש שינויים שלא נשמרו. לעבור לחודש אחר בלי לשמור?')) return;
    setParams({ month: next });
  };

  const monthName = new Date(year, mon - 1, 1).toLocaleDateString('he-IL', { month: 'long', year: 'numeric' });

  if (!data) {
    return (
      <Stack spacing={2}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
          בונוסים
        </Typography>
        {message ? <Alert severity="error">{message.text}</Alert> : <CircularProgress aria-label="טוען" />}
      </Stack>
    );
  }

  const canEdit = user?.role === 'TeamLead' && !data.locked;
  const live = data.rows.map((r) => ({ row: r, parsed: parseAmount(drafts[r.summary.employeeId]?.amount ?? '', data.bonusCap) }));
  const hasErrors = live.some((l) => l.parsed.error);
  const total = live.reduce((sum, l) => sum + (l.parsed.value ?? 0), 0);
  const missing = live.filter((l) => l.parsed.value === null).map((l) => l.row.summary.employeeName);

  const entries = (): BonusEntry[] =>
    live.map((l) => ({
      employeeId: l.row.summary.employeeId,
      amount: l.parsed.value,
      note: drafts[l.row.summary.employeeId]?.note.trim() || null,
    }));

  const save = async () => {
    setMessage(null);
    if (hasErrors) {
      setMessage({ type: 'error', text: 'יש לתקן את השדות המסומנים' });
      return false;
    }
    setBusy(true);
    try {
      apply(await api.saveBonuses(year, mon, entries()));
      return true;
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'השמירה נכשלה' });
      return false;
    } finally {
      setBusy(false);
    }
  };

  const openConfirm = () => {
    setMessage(null);
    if (hasErrors) {
      setMessage({ type: 'error', text: 'יש לתקן את השדות המסומנים' });
      return;
    }
    if (missing.length > 0) {
      setMessage({ type: 'error', text: `חסר סכום (אפשר גם 0) עבור: ${missing.join(', ')}` });
      return;
    }
    setConfirming(true);
  };

  const sendToPayroll = async () => {
    if (dirty && !(await save())) {
      setConfirming(false);
      return;
    }
    setBusy(true);
    try {
      apply(await api.sendBonuses(year, mon));
      setMessage({ type: 'success', text: 'הבונוסים נשלחו למנהלת והחודש ננעל' });
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'השליחה נכשלה' });
    } finally {
      setBusy(false);
      setConfirming(false);
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          בונוסים
        </Typography>
        <TextField
          label="חודש"
          type="month"
          size="small"
          value={month}
          onChange={(e) => changeMonth(e.target.value)}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: toIso(new Date()).slice(0, 7) } }}
        />
      </Stack>

      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography sx={{ fontWeight: 500 }}>{monthName}</Typography>
        {data.locked ? (
          <Chip
            color="success"
            icon={<LockIcon />}
            label={`נשלח לשכר ${data.lockedAt ? formatSentAt(data.lockedAt) : ''} · ${data.lockedBy ?? ''}`}
          />
        ) : (
          <Chip variant="outlined" label="עוד לא נשלח לשכר" />
        )}
        {dirty && <Chip size="small" label="יש שינויים שלא נשמרו" />}
        <Stack direction="row" sx={{ flexGrow: 1, justifyContent: 'flex-end', gap: 1 }}>
          <Button
            variant="outlined"
            startIcon={<DownloadIcon />}
            component="a"
            href={api.bonusesExcelUrl(year, mon)}
            download
          >
            הורדת אקסל
          </Button>
        </Stack>
      </Stack>

      {data.locked && (
        <Alert severity="info">החודש נשלח לשכר ונעול: אי אפשר לשנות בונוסים, נוכחות, ביצוע או לוח שנה בחודש הזה.</Alert>
      )}
      {!data.locked && !data.ended && (
        <Alert severity="info">אפשר למלא סכומים כבר עכשיו, אבל השליחה לשכר תתאפשר רק אחרי שהחודש יסתיים.</Alert>
      )}
      {message && <Alert severity={message.type}>{message.text}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small" aria-label={`בונוסים ${monthName}`}>
          <TableHead>
            <TableRow>
              <TableCell>שם</TableCell>
              <TableCell align="center">ימי נוכחות</TableCell>
              <TableCell align="center">היעדרויות</TableCell>
              <TableCell align="center">חלש / טוב / מעולה</TableCell>
              <TableCell align="center">ממוצע</TableCell>
              <TableCell align="center" width={150}>
                סכום (0-{data.bonusCap})
              </TableCell>
              <TableCell>הערה</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {live.map(({ row, parsed }) => {
              const s = row.summary;
              const d = drafts[s.employeeId];
              return (
                <TableRow key={s.employeeId} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>
                    {s.employeeName}
                    {!s.isActive && <Chip size="small" label="לא פעילה" variant="outlined" sx={{ ml: 1 }} />}
                  </TableCell>
                  <TableCell align="center">
                    {s.presentDays}
                    {s.partialDays > 0 && ` (${s.partialDays} חלקיים)`}
                  </TableCell>
                  <TableCell align="center">{s.vacationDays + s.sickDays + s.otherDays}</TableCell>
                  <TableCell align="center" sx={{ whiteSpace: 'nowrap' }}>
                    {s.weakDays} / {s.goodDays} / {s.excellentDays}
                  </TableCell>
                  <TableCell align="center">{s.averagePercent === null ? '—' : `${s.averagePercent.toFixed(2)}%`}</TableCell>
                  <TableCell align="center">
                    {canEdit ? (
                      <TextField
                        size="small"
                        value={d?.amount ?? ''}
                        disabled={busy}
                        onChange={(e) => {
                          setDrafts((prev) => ({ ...prev, [s.employeeId]: { ...prev[s.employeeId], amount: e.target.value } }));
                          setDirty(true);
                          setMessage(null);
                        }}
                        error={!!parsed.error}
                        helperText={parsed.error}
                        slotProps={{
                          htmlInput: {
                            inputMode: 'decimal',
                            'aria-label': `בונוס של ${s.employeeName}`,
                            style: { textAlign: 'center' },
                          },
                        }}
                      />
                    ) : parsed.value === null ? (
                      '—'
                    ) : (
                      shekel(parsed.value)
                    )}
                  </TableCell>
                  <TableCell>
                    {canEdit ? (
                      <TextField
                        size="small"
                        fullWidth
                        value={d?.note ?? ''}
                        disabled={busy}
                        onChange={(e) => {
                          setDrafts((prev) => ({ ...prev, [s.employeeId]: { ...prev[s.employeeId], note: e.target.value } }));
                          setDirty(true);
                          setMessage(null);
                        }}
                        slotProps={{ htmlInput: { maxLength: NOTE_MAX, 'aria-label': `הערה ל${s.employeeName}` } }}
                      />
                    ) : (
                      d?.note
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
            {data.rows.length === 0 && (
              <TableRow>
                <TableCell colSpan={7} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                  אין עובדות בחודש הזה
                </TableCell>
              </TableRow>
            )}
          </TableBody>
          {data.rows.length > 0 && (
            <TableFooter>
              <TableRow>
                <TableCell colSpan={5} sx={{ fontWeight: 700 }}>
                  סה״כ בונוסים לצוות
                </TableCell>
                <TableCell align="center" sx={{ fontWeight: 700 }}>
                  {shekel(total)}
                </TableCell>
                <TableCell />
              </TableRow>
            </TableFooter>
          )}
        </Table>
      </TableContainer>

      {canEdit && data.rows.length > 0 && (
        <Stack direction="row" sx={{ justifyContent: 'flex-end', gap: 1 }}>
          <Button variant="outlined" onClick={save} disabled={busy || !dirty}>
            שמירה
          </Button>
          <Button variant="contained" startIcon={<SendIcon />} onClick={openConfirm} disabled={busy || !data.ended}>
            שליחה לשכר
          </Button>
        </Stack>
      )}

      {confirming && (
        <Dialog open onClose={() => !busy && setConfirming(false)} fullWidth maxWidth="sm">
          <DialogTitle>אישור שליחה לשכר – {monthName}</DialogTitle>
          <DialogContent>
            <Stack spacing={2}>
              <Alert severity="warning">
                הטבלה תישלח במייל למנהלת עם קובץ אקסל, והחודש יינעל: לא יהיה אפשר לשנות בונוסים, נוכחות, ביצוע או לוח שנה
                בחודש הזה.
              </Alert>
              <Table size="small" aria-label="טבלת בונוסים לאישור">
                <TableHead>
                  <TableRow>
                    <TableCell>שם</TableCell>
                    <TableCell align="center">סכום</TableCell>
                    <TableCell>הערה</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {live.map(({ row, parsed }) => (
                    <TableRow key={row.summary.employeeId}>
                      <TableCell>{row.summary.employeeName}</TableCell>
                      <TableCell align="center">{shekel(parsed.value ?? 0)}</TableCell>
                      <TableCell>{drafts[row.summary.employeeId]?.note}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
                <TableFooter>
                  <TableRow>
                    <TableCell sx={{ fontWeight: 700 }}>סה״כ</TableCell>
                    <TableCell align="center" sx={{ fontWeight: 700 }}>
                      {shekel(total)}
                    </TableCell>
                    <TableCell />
                  </TableRow>
                </TableFooter>
              </Table>
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setConfirming(false)} disabled={busy}>
              ביטול
            </Button>
            <Button variant="contained" color="primary" startIcon={<SendIcon />} onClick={sendToPayroll} disabled={busy}>
              אישור, שליחה ונעילה
            </Button>
          </DialogActions>
        </Dialog>
      )}
    </Stack>
  );
}
