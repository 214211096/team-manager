import { useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Button, Chip, CircularProgress, Paper, Stack, TextField, Typography } from '@mui/material';
import { api, ApiError } from '../api/client';
import { formatDate } from '../attendance';

const percent = z.coerce
  .number<string | number>({ error: 'יש להזין מספר' })
  .min(0, 'בין 0 ל-100')
  .max(100, 'בין 0 ל-100');

const schema = z
  .object({
    managerEmail: z.union([z.literal(''), z.email('כתובת מייל לא תקינה')]),
    reminderTime: z.string().regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'שעה בפורמט HH:mm'),
    weakThreshold: percent,
    excellentThreshold: percent,
    bonusCap: z.coerce.number<string | number>({ error: 'יש להזין מספר' }).min(0, 'לא יכול להיות שלילי'),
    noReminderDates: z.array(z.string()).max(80, 'עד 80 ימים'),
  })
  .refine((v) => v.weakThreshold <= v.excellentThreshold, {
    message: "סף 'חלש' חייב להיות קטן או שווה לסף 'מעולה'",
    path: ['excellentThreshold'],
  });
type FormInput = z.input<typeof schema>;
type FormValues = z.output<typeof schema>;

export default function SettingsPage() {
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const {
    register,
    handleSubmit,
    reset,
    watch,
    control,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormValues>({ resolver: zodResolver(schema) });

  useEffect(() => {
    api
      .settings()
      .then((s) => reset(s))
      .catch(() => setMessage({ type: 'error', text: 'לא ניתן לטעון הגדרות' }))
      .finally(() => setLoading(false));
  }, [reset]);

  const onSubmit = async (values: FormValues) => {
    setMessage(null);
    try {
      reset(await api.saveSettings(values));
      setMessage({ type: 'success', text: 'ההגדרות נשמרו' });
    } catch (e) {
      setMessage({ type: 'error', text: e instanceof ApiError ? e.message : 'השמירה נכשלה' });
    }
  };

  if (loading) return <CircularProgress aria-label="טוען" />;

  const weak = watch('weakThreshold');
  const excellent = watch('excellentThreshold');

  return (
    <Stack spacing={2} sx={{ maxWidth: 560 }}>
      <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }}>
        הגדרות
      </Typography>

      {message && <Alert severity={message.type}>{message.text}</Alert>}

      <Paper sx={{ p: { xs: 2, sm: 3 } }}>
        <Stack component="form" spacing={3} onSubmit={handleSubmit(onSubmit)} noValidate>
          <Section title="מיילים">
            <TextField
              label="מייל המנהלת"
              type="email"
              dir="ltr"
              {...register('managerEmail')}
              error={!!errors.managerEmail}
              helperText={errors.managerEmail?.message ?? 'לכאן נשלחים דיווח הנוכחות וטבלת הבונוסים'}
            />
            <TextField
              label="שעת תזכורת נוכחות"
              type="time"
              {...register('reminderTime')}
              error={!!errors.reminderTime}
              helperText={errors.reminderTime?.message ?? 'אם הדיווח לא נשלח עד שעה זו, תישלח תזכורת למייל של מנהלת הצוות (ימים א׳-ה׳)'}
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <Controller
              name="noReminderDates"
              control={control}
              render={({ field, fieldState }) => (
                <NoReminderDates value={field.value ?? []} onChange={field.onChange} error={fieldState.error?.message} />
              )}
            />
          </Section>

          <Section title="ספי סטטוס יומי (%)">
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField
                label="'חלש' מתחת ל-"
                type="number"
                fullWidth
                slotProps={{ htmlInput: { min: 0, max: 100, step: 'any' } }}
                {...register('weakThreshold')}
                error={!!errors.weakThreshold}
                helperText={errors.weakThreshold?.message}
              />
              <TextField
                label="'מעולה' מעל"
                type="number"
                fullWidth
                slotProps={{ htmlInput: { min: 0, max: 100, step: 'any' } }}
                {...register('excellentThreshold')}
                error={!!errors.excellentThreshold}
                helperText={errors.excellentThreshold?.message}
              />
            </Stack>
            <Typography variant="body2" color="text.secondary">
              חלש: פחות מ-{String(weak ?? '')}% · טוב: {String(weak ?? '')}% עד {String(excellent ?? '')}% כולל ·
              מעולה: מעל {String(excellent ?? '')}%
            </Typography>
          </Section>

          <Section title="בונוס">
            <TextField
              label="תקרת בונוס חודשי (ש״ח)"
              type="number"
              slotProps={{ htmlInput: { min: 0 } }}
              {...register('bonusCap')}
              error={!!errors.bonusCap}
              helperText={errors.bonusCap?.message}
            />
          </Section>

          <Stack direction="row">
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              שמירת הגדרות
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </Stack>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Stack spacing={2} component="fieldset" sx={{ border: 0, p: 0, m: 0 }}>
      <Typography component="legend" variant="subtitle1" sx={{ fontWeight: 700, mb: 1 }}>
        {title}
      </Typography>
      {children}
    </Stack>
  );
}

/** ימים (חגים) שבהם לא נשלחת תזכורת: בוחרים תאריך ומוסיפים לרשימה. */
function NoReminderDates({
  value,
  onChange,
  error,
}: {
  value: string[];
  onChange: (dates: string[]) => void;
  error?: string;
}) {
  const [pending, setPending] = useState('');

  const add = () => {
    if (pending && !value.includes(pending)) onChange([...value, pending].sort());
    setPending('');
  };

  return (
    <Stack spacing={1}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
        <TextField
          label="ימים ללא תזכורת (חגים)"
          type="date"
          value={pending}
          onChange={(e) => setPending(e.target.value)}
          error={!!error}
          helperText={error ?? 'בימים אלה לא תישלח תזכורת נוכחות'}
          slotProps={{ inputLabel: { shrink: true } }}
          sx={{ flexGrow: 1 }}
        />
        <Button variant="outlined" onClick={add} disabled={!pending} sx={{ mt: 1 }}>
          הוספה
        </Button>
      </Stack>
      {value.length > 0 && (
        <Stack direction="row" sx={{ flexWrap: 'wrap', gap: 1 }}>
          {value.map((d) => (
            <Chip
              key={d}
              label={formatDate(d)}
              onDelete={() => onChange(value.filter((x) => x !== d))}
            />
          ))}
        </Stack>
      )}
    </Stack>
  );
}
