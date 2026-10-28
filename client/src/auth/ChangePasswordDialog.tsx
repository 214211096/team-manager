import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { api, ApiError } from '../api/client';

const schema = z
  .object({
    currentPassword: z.string().min(1, 'חובה להזין את הסיסמה הנוכחית'),
    newPassword: z.string().min(10, 'סיסמה חדשה של 10 תווים לפחות').max(200, 'סיסמה עד 200 תווים'),
    confirm: z.string(),
  })
  .refine((v) => v.newPassword === v.confirm, { message: 'הסיסמאות לא תואמות', path: ['confirm'] })
  .refine((v) => v.newPassword !== v.currentPassword, {
    message: 'הסיסמה החדשה זהה לנוכחית',
    path: ['newPassword'],
  });
type FormValues = z.infer<typeof schema>;

export default function ChangePasswordDialog({ onClose }: { onClose: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirm: '' },
  });

  const onSubmit = async (v: FormValues) => {
    setError(null);
    try {
      await api.changePassword(v.currentPassword, v.newPassword);
      setDone(true);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'שינוי הסיסמה נכשל');
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <DialogTitle>שינוי סיסמה</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}
            {done ? (
              <Alert severity="success">הסיסמה שונתה. בכניסה הבאה יש להשתמש בסיסמה החדשה.</Alert>
            ) : (
              <>
                <TextField
                  label="סיסמה נוכחית"
                  type="password"
                  autoComplete="current-password"
                  autoFocus
                  {...register('currentPassword')}
                  error={!!errors.currentPassword}
                  helperText={errors.currentPassword?.message}
                />
                <TextField
                  label="סיסמה חדשה"
                  type="password"
                  autoComplete="new-password"
                  {...register('newPassword')}
                  error={!!errors.newPassword}
                  helperText={errors.newPassword?.message ?? '10 תווים לפחות'}
                />
                <TextField
                  label="סיסמה חדשה שוב"
                  type="password"
                  autoComplete="new-password"
                  {...register('confirm')}
                  error={!!errors.confirm}
                  helperText={errors.confirm?.message}
                />
              </>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>{done ? 'סגירה' : 'ביטול'}</Button>
          {!done && (
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              שמירה
            </Button>
          )}
        </DialogActions>
      </form>
    </Dialog>
  );
}
