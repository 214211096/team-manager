import { useCallback, useEffect, useMemo, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Alert,
  Box,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  IconButton,
  MenuItem,
  Paper,
  Stack,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import ChevronRightIcon from '@mui/icons-material/ChevronRight';
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft';
import AddIcon from '@mui/icons-material/Add';
import { api, ApiError } from '../api/client';
import type { AttendanceStatus, Employee, ScheduleEntry } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { formatDate, plannedLabel, statusColor, statusLabel, toIso, todayIso } from '../attendance';

const WEEKDAYS = ['א׳', 'ב׳', 'ג׳', 'ד׳', 'ה׳', 'ו׳', 'ש׳'];

/** 6 שבועות מיום ראשון שלפני תחילת החודש, כמו בלוח של Outlook. */
function monthGrid(month: Date): Date[] {
  const start = new Date(month.getFullYear(), month.getMonth(), 1);
  start.setDate(start.getDate() - start.getDay());
  return Array.from({ length: 42 }, (_, i) => new Date(start.getFullYear(), start.getMonth(), start.getDate() + i));
}

function entryColor(e: ScheduleEntry) {
  return e.kind === 'PartialDay' ? 'info' : statusColor[e.absenceStatus ?? 'Other'];
}

export default function CalendarPage() {
  const { user } = useAuth();
  const canEdit = user?.role === 'TeamLead';
  const [month, setMonth] = useState(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1);
  });
  const [entries, setEntries] = useState<ScheduleEntry[]>([]);
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [error, setError] = useState<string | null>(null);
  // null = סגור; { date } = רישום חדש מיום מסוים; { entry } = עריכה
  const [editing, setEditing] = useState<{ entry?: ScheduleEntry; date?: string } | null>(null);

  const days = useMemo(() => monthGrid(month), [month]);
  const from = toIso(days[0]);
  const to = toIso(days[days.length - 1]);

  const load = useCallback(() => {
    api
      .schedule(from, to)
      .then((list) => {
        setEntries(list);
        setError(null);
      })
      .catch(() => setError('לא ניתן לטעון את לוח השנה'));
  }, [from, to]);

  useEffect(load, [load]);
  useEffect(() => {
    api
      .employees()
      .then(setEmployees)
      .catch(() => setError('לא ניתן לטעון את רשימת העובדות'));
  }, []);

  const shiftMonth = (delta: number) => setMonth((m) => new Date(m.getFullYear(), m.getMonth() + delta, 1));
  const today = todayIso();
  const monthEntries = entries.filter(
    (e) => e.endDate >= toIso(month) && e.startDate <= toIso(new Date(month.getFullYear(), month.getMonth() + 1, 0)),
  );

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          לוח שנה
        </Typography>
        <IconButton aria-label="החודש הקודם" onClick={() => shiftMonth(-1)}>
          <ChevronRightIcon />
        </IconButton>
        <Typography variant="h6" component="p" sx={{ minWidth: 130, textAlign: 'center' }}>
          {month.toLocaleDateString('he-IL', { month: 'long', year: 'numeric' })}
        </Typography>
        <IconButton aria-label="החודש הבא" onClick={() => shiftMonth(1)}>
          <ChevronLeftIcon />
        </IconButton>
        <Button
          onClick={() => {
            const d = new Date();
            setMonth(new Date(d.getFullYear(), d.getMonth(), 1));
          }}
        >
          היום
        </Button>
        {canEdit && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing({ date: today })}>
            רישום חדש
          </Button>
        )}
      </Stack>

      {error && <Alert severity="error">{error}</Alert>}

      <Paper sx={{ overflow: 'hidden' }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))' }}>
          {WEEKDAYS.map((d) => (
            <Box
              key={d}
              sx={{ p: 1, textAlign: 'center', fontWeight: 700, bgcolor: 'grey.100', borderBottom: 1, borderColor: 'divider' }}
            >
              {d}
            </Box>
          ))}
          {days.map((d) => {
            const iso = toIso(d);
            const inMonth = d.getMonth() === month.getMonth();
            const weekend = d.getDay() === 5 || d.getDay() === 6;
            const dayEntries = entries.filter((e) => e.startDate <= iso && e.endDate >= iso);
            return (
              <Box
                key={iso}
                role={canEdit ? 'button' : undefined}
                tabIndex={canEdit ? 0 : undefined}
                aria-label={canEdit ? `הוספת רישום ל-${formatDate(iso)}` : undefined}
                onClick={canEdit ? () => setEditing({ date: iso }) : undefined}
                onKeyDown={
                  canEdit
                    ? (e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          setEditing({ date: iso });
                        }
                      }
                    : undefined
                }
                sx={{
                  minHeight: { xs: 64, md: 104 },
                  p: 0.5,
                  borderBottom: 1,
                  borderInlineStart: 1,
                  borderColor: 'divider',
                  bgcolor: weekend ? 'grey.50' : 'background.paper',
                  opacity: inMonth ? 1 : 0.45,
                  cursor: canEdit ? 'pointer' : 'default',
                  '&:hover': canEdit ? { bgcolor: 'action.hover' } : undefined,
                  overflow: 'hidden',
                }}
              >
                <Typography
                  variant="body2"
                  sx={{
                    fontWeight: iso === today ? 700 : 400,
                    color: iso === today ? 'primary.contrastText' : 'text.secondary',
                    bgcolor: iso === today ? 'primary.main' : undefined,
                    borderRadius: '50%',
                    width: 24,
                    height: 24,
                    display: 'grid',
                    placeItems: 'center',
                    mb: 0.5,
                  }}
                >
                  {d.getDate()}
                </Typography>
                <Stack spacing={0.5}>
                  {dayEntries.map((e) => (
                    <Chip
                      key={e.id}
                      size="small"
                      color={entryColor(e)}
                      label={`${e.employeeName} · ${plannedLabel(e)}`}
                      title={`${e.employeeName} · ${plannedLabel(e)}`}
                      onClick={
                        canEdit
                          ? (ev) => {
                              ev.stopPropagation();
                              setEditing({ entry: e });
                            }
                          : undefined
                      }
                      sx={{ justifyContent: 'flex-start', maxWidth: '100%', fontSize: { xs: 10, md: 12 } }}
                    />
                  ))}
                </Stack>
              </Box>
            );
          })}
        </Box>
      </Paper>

      <Typography variant="h6" component="h2">
        רישומים בחודש
      </Typography>
      <Paper>
        {monthEntries.length === 0 ? (
          <Typography sx={{ p: 2, color: 'text.secondary' }}>אין רישומים בחודש הזה</Typography>
        ) : (
          <Stack divider={<Box sx={{ borderBottom: 1, borderColor: 'divider' }} />}>
            {monthEntries.map((e) => (
              <Stack key={e.id} direction="row" sx={{ alignItems: 'center', gap: 1, p: 1.5, flexWrap: 'wrap' }}>
                <Chip size="small" color={entryColor(e)} label={plannedLabel(e)} />
                <Typography sx={{ fontWeight: 500 }}>{e.employeeName}</Typography>
                <Typography color="text.secondary" sx={{ flexGrow: 1 }}>
                  {e.startDate === e.endDate
                    ? formatDate(e.startDate)
                    : `${formatDate(e.startDate)} – ${formatDate(e.endDate)}`}
                </Typography>
                {canEdit && (
                  <Button size="small" onClick={() => setEditing({ entry: e })}>
                    עריכה
                  </Button>
                )}
              </Stack>
            ))}
          </Stack>
        )}
      </Paper>

      {editing && (
        <EntryDialog
          entry={editing.entry ?? null}
          date={editing.date ?? today}
          employees={employees}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            load();
          }}
        />
      )}
    </Stack>
  );
}

const absenceStatuses: AttendanceStatus[] = ['Vacation', 'Sick', 'Other'];

const schema = z
  .object({
    employeeId: z.coerce.number<string | number>().int().min(1, 'יש לבחור עובדת'),
    kind: z.enum(['Absence', 'PartialDay']),
    absenceStatus: z.enum(['Vacation', 'Sick', 'Other']),
    reason: z.string().trim().max(200, 'סיבה עד 200 תווים'),
    workPercent: z.coerce.number<string | number>({ error: 'יש להזין מספר' }),
    startDate: z.string().min(1, 'יש לבחור תאריך'),
    endDate: z.string().min(1, 'יש לבחור תאריך'),
  })
  .superRefine((v, ctx) => {
    if (v.endDate < v.startDate)
      ctx.addIssue({ code: 'custom', path: ['endDate'], message: 'תאריך הסיום לפני תאריך ההתחלה' });
    if (v.kind === 'Absence' && v.absenceStatus === 'Other' && !v.reason)
      ctx.addIssue({ code: 'custom', path: ['reason'], message: "בהיעדרות מסוג 'אחר' חובה לכתוב סיבה" });
    if (v.kind === 'PartialDay' && !(Number.isInteger(v.workPercent) && v.workPercent >= 1 && v.workPercent <= 99))
      ctx.addIssue({ code: 'custom', path: ['workPercent'], message: 'אחוז עבודה בין 1 ל-99' });
  });
type FormInput = z.input<typeof schema>;
type FormValues = z.output<typeof schema>;

function EntryDialog({
  entry,
  date,
  employees,
  onClose,
  onSaved,
}: {
  entry: ScheduleEntry | null;
  date: string;
  employees: Employee[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    control,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      employeeId: entry?.employeeId ?? '',
      kind: entry?.kind ?? 'Absence',
      absenceStatus: (entry?.absenceStatus as 'Vacation' | 'Sick' | 'Other' | null) ?? 'Vacation',
      reason: entry?.reason ?? '',
      workPercent: entry?.workPercent ?? 50,
      startDate: entry?.startDate ?? date,
      endDate: entry?.endDate ?? date,
    },
  });
  const kind = watch('kind');
  const absenceStatus = watch('absenceStatus');

  // עובדת שכבר לא פעילה עדיין צריכה להופיע ברשימה כשעורכים רישום ישן שלה.
  const options =
    entry && !employees.some((e) => e.id === entry.employeeId)
      ? [...employees, { id: entry.employeeId, name: entry.employeeName } as Employee]
      : employees;

  const onSubmit = async (v: FormValues) => {
    setError(null);
    const data = {
      employeeId: v.employeeId,
      startDate: v.startDate,
      endDate: v.endDate,
      kind: v.kind,
      absenceStatus: v.kind === 'Absence' ? v.absenceStatus : null,
      reason: v.kind === 'Absence' && v.absenceStatus === 'Other' ? v.reason : null,
      workPercent: v.kind === 'PartialDay' ? v.workPercent : null,
    };
    try {
      if (entry) await api.updateScheduleEntry(entry.id, data);
      else await api.createScheduleEntry(data);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'השמירה נכשלה');
    }
  };

  const remove = async () => {
    if (!entry || !window.confirm(`למחוק את הרישום של ${entry.employeeName}?`)) return;
    try {
      await api.deleteScheduleEntry(entry.id);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'המחיקה נכשלה');
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <DialogTitle>{entry ? 'עריכת רישום' : 'רישום חדש'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}

            <Controller
              name="employeeId"
              control={control}
              render={({ field }) => (
                <TextField
                  select
                  label="עובדת"
                  value={field.value}
                  onChange={field.onChange}
                  error={!!errors.employeeId}
                  helperText={errors.employeeId?.message}
                >
                  {options.map((e) => (
                    <MenuItem key={e.id} value={e.id}>
                      {e.name}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />

            <Controller
              name="kind"
              control={control}
              render={({ field }) => (
                <ToggleButtonGroup
                  exclusive
                  fullWidth
                  color="primary"
                  value={field.value}
                  onChange={(_, v) => v && field.onChange(v)}
                  aria-label="סוג רישום"
                >
                  <ToggleButton value="Absence">היעדרות</ToggleButton>
                  <ToggleButton value="PartialDay">יום חלקי</ToggleButton>
                </ToggleButtonGroup>
              )}
            />

            {kind === 'Absence' ? (
              <>
                <Controller
                  name="absenceStatus"
                  control={control}
                  render={({ field }) => (
                    <TextField select label="סוג היעדרות" value={field.value} onChange={field.onChange}>
                      {absenceStatuses.map((s) => (
                        <MenuItem key={s} value={s}>
                          {statusLabel[s]}
                        </MenuItem>
                      ))}
                    </TextField>
                  )}
                />
                {absenceStatus === 'Other' && (
                  <TextField
                    label="סיבה"
                    required
                    {...register('reason')}
                    error={!!errors.reason}
                    helperText={errors.reason?.message ?? 'סיבה כללית בלבד, בלי פרטים רפואיים או אישיים'}
                    slotProps={{ htmlInput: { maxLength: 200 } }}
                  />
                )}
              </>
            ) : (
              <Stack spacing={1}>
                <TextField
                  label="אחוז עבודה ביום"
                  type="number"
                  {...register('workPercent')}
                  error={!!errors.workPercent}
                  helperText={errors.workPercent?.message ?? 'היעד היומי יוקטן לפי האחוז, למשל 50% = חצי מהיעד'}
                  slotProps={{ htmlInput: { min: 1, max: 99, inputMode: 'numeric' } }}
                />
                <Stack direction="row" spacing={1}>
                  {[25, 50, 75].map((p) => (
                    <Chip
                      key={p}
                      label={`${p}%`}
                      variant="outlined"
                      onClick={() => setValue('workPercent', p, { shouldValidate: true })}
                    />
                  ))}
                </Stack>
              </Stack>
            )}

            <Stack direction="row" spacing={2}>
              <TextField
                label="מתאריך"
                type="date"
                fullWidth
                {...register('startDate')}
                error={!!errors.startDate}
                helperText={errors.startDate?.message}
                slotProps={{ inputLabel: { shrink: true } }}
              />
              <TextField
                label="עד תאריך"
                type="date"
                fullWidth
                {...register('endDate')}
                error={!!errors.endDate}
                helperText={errors.endDate?.message}
                slotProps={{ inputLabel: { shrink: true } }}
              />
            </Stack>
          </Stack>
        </DialogContent>
        <DialogActions>
          {entry && (
            <Button color="error" onClick={remove}>
              מחיקה
            </Button>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Button onClick={onClose}>ביטול</Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            שמירה
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
