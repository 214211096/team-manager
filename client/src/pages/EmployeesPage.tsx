import { useCallback, useEffect, useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import {
  Alert,
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  IconButton,
  Paper,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import EditIcon from '@mui/icons-material/Edit';
import { api, ApiError } from '../api/client';
import type { Employee } from '../api/types';

const schema = z.object({
  name: z.string().trim().min(1, 'חובה להזין שם').max(100, 'שם עד 100 תווים'),
  dailyTarget: z.coerce
    .number<string | number>({ error: 'יש להזין מספר' })
    .int('מספר שלם בלבד')
    .min(1, 'יעד יומי לפחות 1')
    .max(10000, 'יעד יומי עד 10000'),
  isActive: z.boolean(),
});
type FormInput = z.input<typeof schema>;
type FormValues = z.output<typeof schema>;

export default function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  // null = סגור, 'new' = הוספה, Employee = עריכה
  const [editing, setEditing] = useState<Employee | 'new' | null>(null);

  const load = useCallback(() => {
    api
      .employees(showInactive)
      .then((list) => {
        setEmployees(list);
        setLoadError(null);
      })
      .catch(() => setLoadError('לא ניתן לטעון את רשימת העובדות'));
  }, [showInactive]);

  useEffect(load, [load]);

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          ניהול עובדות
        </Typography>
        <FormControlLabel
          control={<Switch checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />}
          label="הצגת לא פעילות"
        />
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')}>
          הוספת עובדת
        </Button>
      </Stack>

      {loadError && <Alert severity="error">{loadError}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="רשימת עובדות">
          <TableHead>
            <TableRow>
              <TableCell>שם</TableCell>
              <TableCell align="center">יעד יומי</TableCell>
              <TableCell align="center">מצב</TableCell>
              <TableCell align="center" width={80}>
                עריכה
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {employees.map((emp) => (
              <TableRow key={emp.id} hover sx={{ opacity: emp.isActive ? 1 : 0.6 }}>
                <TableCell>{emp.name}</TableCell>
                <TableCell align="center">{emp.dailyTarget}</TableCell>
                <TableCell align="center">
                  <Chip
                    size="small"
                    label={emp.isActive ? 'פעילה' : 'לא פעילה'}
                    color={emp.isActive ? 'success' : 'default'}
                    variant={emp.isActive ? 'filled' : 'outlined'}
                  />
                </TableCell>
                <TableCell align="center">
                  <IconButton aria-label={`עריכת ${emp.name}`} onClick={() => setEditing(emp)} size="small">
                    <EditIcon fontSize="small" />
                  </IconButton>
                </TableCell>
              </TableRow>
            ))}
            {employees.length === 0 && !loadError && (
              <TableRow>
                <TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                  אין עובדות להצגה
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      {editing && (
        <EmployeeDialog
          employee={editing === 'new' ? null : editing}
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

function EmployeeDialog({
  employee,
  onClose,
  onSaved,
}: {
  employee: Employee | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: employee?.name ?? '',
      dailyTarget: employee?.dailyTarget ?? '',
      isActive: employee?.isActive ?? true,
    },
  });

  const onSubmit = async (values: FormValues) => {
    setError(null);
    try {
      if (employee) await api.updateEmployee(employee.id, values);
      else await api.createEmployee(values);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'השמירה נכשלה');
    }
  };

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <form onSubmit={handleSubmit(onSubmit)} noValidate>
        <DialogTitle>{employee ? 'עריכת עובדת' : 'הוספת עובדת'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              label="שם"
              autoFocus
              {...register('name')}
              error={!!errors.name}
              helperText={errors.name?.message ?? 'שם בלבד, בלי פרטים מזהים'}
            />
            <TextField
              label="יעד יומי (פריטים)"
              type="number"
              slotProps={{ htmlInput: { min: 1, inputMode: 'numeric' } }}
              {...register('dailyTarget')}
              error={!!errors.dailyTarget}
              helperText={errors.dailyTarget?.message ?? 'שינוי יעד לא משנה ימים שכבר נשמרו'}
            />
            {employee && (
              <Controller
                name="isActive"
                control={control}
                render={({ field }) => (
                  <FormControlLabel
                    control={<Switch checked={field.value} onChange={(e) => field.onChange(e.target.checked)} />}
                    label={field.value ? 'פעילה' : 'לא פעילה (לא תופיע בנוכחות ובסטטוס)'}
                  />
                )}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose}>ביטול</Button>
          <Button type="submit" variant="contained" disabled={isSubmitting}>
            שמירה
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
