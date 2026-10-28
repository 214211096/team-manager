import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { useAuth } from '../auth/AuthContext';
import { ApiError } from '../api/client';

const schema = z.object({
  username: z.string().trim().min(1, 'חובה להזין שם משתמש'),
  password: z.string().min(1, 'חובה להזין סיסמה'),
});
type FormValues = z.infer<typeof schema>;

export default function LoginPage() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { username: '', password: '' } });

  const onSubmit = async (values: FormValues) => {
    setError(null);
    try {
      await login(values.username, values.password);
      navigate('/', { replace: true });
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'לא ניתן להתחבר לשרת');
    }
  };

  return (
    <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh', p: 2 }}>
      <Paper sx={{ p: { xs: 3, sm: 4 }, width: '100%', maxWidth: 380 }} elevation={2}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700 }} gutterBottom>
          ניהול צוות
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
          כניסה למערכת
        </Typography>
        <Stack component="form" spacing={2} onSubmit={handleSubmit(onSubmit)} noValidate>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField
            label="שם משתמש"
            autoComplete="username"
            autoFocus
            {...register('username')}
            error={!!errors.username}
            helperText={errors.username?.message}
          />
          <TextField
            label="סיסמה"
            type="password"
            autoComplete="current-password"
            {...register('password')}
            error={!!errors.password}
            helperText={errors.password?.message}
          />
          <Button type="submit" variant="contained" size="large" disabled={isSubmitting}>
            כניסה
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}
