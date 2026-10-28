import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { Alert, Button, Card, CardActions, CardContent, Chip, Stack, Typography } from '@mui/material';
import { api } from '../api/client';
import type { AttendanceDay, DailyStatusDay, Rating } from '../api/types';
import { useAuth } from '../auth/AuthContext';
import { formatDate, formatSentAt, ratingColor, ratingLabel, toIso, todayIso } from '../attendance';
import { loadHolidays, previousWorkdayIso } from '../holidays';

/**
 * לוח בית: עובדות פעילות, נוכחות היום, והתפלגות הסטטוס:
 * של היום אם כבר הוזן ביצוע, אחרת של יום העבודה הקודם.
 */
export default function HomePage() {
  const { user } = useAuth();
  const [activeCount, setActiveCount] = useState<number | null>(null);
  const [attendance, setAttendance] = useState<AttendanceDay | null>(null);
  const [status, setStatus] = useState<DailyStatusDay | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // חודש אחורה מספיק כדי לדלג על כל רצף חגים (למשל ראש השנה שנופל לפני שבת).
    const monthAgo = new Date();
    monthAgo.setDate(monthAgo.getDate() - 31);
    const previousStatus = loadHolidays(toIso(monthAgo), todayIso())
      .catch(() => new Map<string, string>())
      .then((holidays) => api.dailyStatus(previousWorkdayIso(holidays)));

    Promise.all([api.employees(), api.attendance(todayIso()), api.dailyStatus(todayIso()), previousStatus])
      .then(([list, day, todayStatus, previousStatus]) => {
        setActiveCount(list.length);
        setAttendance(day);
        setStatus(todayStatus.rows.some((r) => r.rating) ? todayStatus : previousStatus);
      })
      .catch(() => setError('לא ניתן לטעון נתונים'));
  }, []);

  const rated = status?.rows.filter((r) => r.rating) ?? [];

  const reported = attendance?.rows.filter((r) => r.status).length ?? 0;
  const present = attendance?.rows.filter((r) => r.status === 'Present').length ?? 0;

  const now = new Date();
  const today = `${now.toLocaleDateString('he-IL', { weekday: 'long' })}, ${formatDate(now)}`;

  return (
    <Stack spacing={3}>
      <div>
        <Typography variant="h4" component="h1" sx={{ fontWeight: 700 }}>
          שלום {user?.displayName ?? user?.username}
        </Typography>
        <Typography color="text.secondary">{today}</Typography>
      </div>

      {error && <Alert severity="error">{error}</Alert>}

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <Card sx={{ flex: 1 }}>
          <CardContent>
            <Typography color="text.secondary" gutterBottom>
              עובדות פעילות
            </Typography>
            <Typography variant="h3" component="p" sx={{ fontWeight: 700 }}>
              {activeCount ?? '—'}
            </Typography>
          </CardContent>
        </Card>
        <Card sx={{ flex: 1 }}>
          <CardContent>
            <Typography color="text.secondary" gutterBottom>
              נוכחות היום
            </Typography>
            {attendance && attendance.rows.length > 0 ? (
              <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
                <Typography variant="h3" component="p" sx={{ fontWeight: 700 }}>
                  {present}
                  <Typography component="span" variant="h6" color="text.secondary">
                    {' '}
                    נוכחות · {reported - present} לא נוכחות
                  </Typography>
                </Typography>
                {reported < attendance.rows.length && (
                  <Typography variant="body2" color="text.secondary">
                    דווחו {reported} מתוך {attendance.rows.length}
                  </Typography>
                )}
                {attendance.reportSentAt ? (
                  <Chip size="small" color="success" label={`נשלח למנהלת ${formatSentAt(attendance.reportSentAt)}`} />
                ) : (
                  <Chip size="small" color="warning" variant="outlined" label="עוד לא נשלח למנהלת" />
                )}
              </Stack>
            ) : (
              <Typography variant="h3" component="p" sx={{ fontWeight: 700 }}>
                —
              </Typography>
            )}
          </CardContent>
          {user?.role === 'TeamLead' && (
            <CardActions>
              <Button component={RouterLink} to="/attendance" size="small">
                למסך הנוכחות
              </Button>
            </CardActions>
          )}
        </Card>
        <Card sx={{ flex: 1 }}>
          <CardContent>
            <Typography color="text.secondary" gutterBottom>
              {status?.date === todayIso() ? 'סטטוס היום' : 'סטטוס יום העבודה הקודם'}
              {status && ` (${formatDate(status.date)})`}
            </Typography>
            {rated.length > 0 ? (
              <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
                <Stack direction="row" sx={{ gap: 1, flexWrap: 'wrap' }}>
                  {(['Weak', 'Good', 'Excellent'] as Rating[]).map((r) => (
                    <Chip
                      key={r}
                      color={ratingColor[r]}
                      label={`${ratingLabel[r]}: ${rated.filter((x) => x.rating === r).length}`}
                      sx={{ fontWeight: 700 }}
                    />
                  ))}
                </Stack>
                {rated.length < status!.rows.length && (
                  <Typography variant="body2" color="text.secondary">
                    הוזן ביצוע ל-{rated.length} מתוך {status!.rows.length} נוכחות
                  </Typography>
                )}
              </Stack>
            ) : (
              <Typography variant="body1" color="text.secondary">
                לא הוזן ביצוע
              </Typography>
            )}
          </CardContent>
          {user?.role === 'TeamLead' && (
            <CardActions>
              <Button component={RouterLink} to="/daily-status" size="small">
                למסך הסטטוס
              </Button>
            </CardActions>
          )}
        </Card>
      </Stack>

      {user?.role === 'TeamLead' && activeCount === 0 && (
        <Alert
          severity="info"
          action={
            <Button component={RouterLink} to="/employees" color="inherit" size="small">
              לניהול עובדות
            </Button>
          }
        >
          עוד לא הוגדרו עובדות. התחילי בהוספת העובדות והיעד היומי של כל אחת.
        </Alert>
      )}
    </Stack>
  );
}
