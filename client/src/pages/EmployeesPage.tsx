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
  MenuItem,
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
import DeleteIcon from '@mui/icons-material/DeleteOutlined';
import EditIcon from '@mui/icons-material/Edit';
import RestoreIcon from '@mui/icons-material/Restore';
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

type View = 'active' | 'inactive' | 'archive';

export default function EmployeesPage() {
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [view, setView] = useState<View>('active');
  const archived = view === 'archive';
  const [loadError, setLoadError] = useState<string | null>(null);
  const [restoreError, setRestoreError] = useState<string | null>(null);
  // null = סגור, 'new' = הוספה, Employee = עריכה
  const [editing, setEditing] = useState<Employee | 'new' | null>(null);

  const load = useCallback(() => {
    api
      .employees(view === 'inactive', view === 'archive')
      .then((list) => {
        setEmployees(view === 'inactive' ? list.filter((e) => !e.isActive) : list);
        setLoadError(null);
      })
      .catch(() => setLoadError('לא ניתן לטעון את רשימת העובדות'));
  }, [view]);

  useEffect(load, [load]);

  const restore = async (emp: Employee) => {
    setRestoreError(null);
    try {
      await api.restoreEmployee(emp.id);
      load();
    } catch (e) {
      setRestoreError(e instanceof ApiError ? e.message : 'השחזור נכשל');
    }
  };

  return (
    <Stack spacing={2}>
      <Stack direction="row" sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}>
        <Typography variant="h5" component="h1" sx={{ fontWeight: 700, flexGrow: 1 }}>
          ניהול עובדות
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setEditing('new')}>
          הוספת עובדת
        </Button>
      </Stack>

      <TextField
        select
        size="small"
        label="הצגה"
        value={view}
        onChange={(e) => setView(e.target.value as View)}
        sx={{ alignSelf: 'flex-start', minWidth: 180 }}
      >
        <MenuItem value="active">פעילות</MenuItem>
        <MenuItem value="inactive">לא פעילות</MenuItem>
        <MenuItem value="archive">ארכיון</MenuItem>
      </TextField>

      {loadError && <Alert severity="error">{loadError}</Alert>}
      {restoreError && <Alert severity="error">{restoreError}</Alert>}

      <TableContainer component={Paper}>
        <Table size="small" aria-label="רשימת עובדות">
          <TableHead>
            <TableRow>
              <TableCell>שם</TableCell>
              <TableCell align="center">יעד יומי</TableCell>
              <TableCell align="center">{archived ? 'נמחקה בתאריך' : 'מצב'}</TableCell>
              <TableCell align="center" width={80}>
                {archived ? 'שחזור' : 'עריכה'}
              </TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {employees.map((emp) => (
              <TableRow key={emp.id} hover sx={{ opacity: emp.isActive ? 1 : 0.6 }}>
                <TableCell>{emp.name}</TableCell>
                <TableCell align="center">{emp.dailyTarget}</TableCell>
                <TableCell align="center">
                  {emp.deletedAt ? (
                    new Date(emp.deletedAt).toLocaleDateString('he-IL')
                  ) : (
                    <Chip
                      size="small"
                      label={emp.isActive ? 'פעילה' : 'לא פעילה'}
                      color={emp.isActive ? 'success' : 'default'}
                      variant={emp.isActive ? 'filled' : 'outlined'}
                    />
                  )}
                </TableCell>
                <TableCell align="center">
                  {emp.deletedAt ? (
                    <IconButton aria-label={`שחזור ${emp.name}`} onClick={() => restore(emp)} size="small">
                      <RestoreIcon fontSize="small" />
                    </IconButton>
                  ) : (
                    <IconButton aria-label={`עריכת ${emp.name}`} onClick={() => setEditing(emp)} size="small">
                      <EditIcon fontSize="small" />
                    </IconButton>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {employees.length === 0 && !loadError && (
              <TableRow>
                <TableCell colSpan={4} align="center" sx={{ py: 4, color: 'text.secondary' }}>
                  {archived ? 'הארכיון ריק' : view === 'inactive' ? 'אין עובדות לא פעילות' : 'אין עובדות להצגה'}
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
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
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

  const onDelete = async () => {
    if (!employee) return;
    setError(null);
    setDeleting(true);
    try {
      await api.deleteEmployee(employee.id);
      onSaved();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'המחיקה נכשלה');
      setConfirmDelete(false);
    } finally {
      setDeleting(false);
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
        {confirmDelete ? (
          <DialogActions>
            <Typography sx={{ flexGrow: 1, px: 1 }}>למחוק את {employee?.name}? היא תעבור לארכיון ואפשר לשחזר.</Typography>
            <Button onClick={() => setConfirmDelete(false)}>לא</Button>
            <Button color="error" variant="contained" onClick={onDelete} disabled={deleting}>
              כן, למחוק
            </Button>
          </DialogActions>
        ) : (
          <DialogActions>
            {employee && (
              <Button color="error" startIcon={<DeleteIcon />} onClick={() => setConfirmDelete(true)} sx={{ me: 'auto' }}>
                מחיקה
              </Button>
            )}
            <Button onClick={onClose}>ביטול</Button>
            <Button type="submit" variant="contained" disabled={isSubmitting}>
              שמירה
            </Button>
          </DialogActions>
        )}
      </form>
    </Dialog>
  );
}
