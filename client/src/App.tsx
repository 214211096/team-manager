import { Navigate, Route, Routes } from 'react-router-dom';
import { Box, CircularProgress } from '@mui/material';
import { useAuth } from './auth/AuthContext';
import type { UserRole } from './api/types';
import AppLayout from './layout/AppLayout';
import LoginPage from './pages/LoginPage';
import HomePage from './pages/HomePage';
import EmployeesPage from './pages/EmployeesPage';
import AttendancePage from './pages/AttendancePage';
import DailyStatusPage from './pages/DailyStatusPage';
import CalendarPage from './pages/CalendarPage';
import MonthlySummaryPage from './pages/MonthlySummaryPage';
import BonusesPage from './pages/BonusesPage';
import SettingsPage from './pages/SettingsPage';

function RequireRole({ role, children }: { role: UserRole; children: React.ReactNode }) {
  const { user } = useAuth();
  return user?.role === role ? children : <Navigate to="/" replace />;
}

export default function App() {
  const { user, loading } = useAuth();

  if (loading) {
    return (
      <Box sx={{ display: 'grid', placeItems: 'center', minHeight: '100vh' }}>
        <CircularProgress aria-label="טוען" />
      </Box>
    );
  }

  if (!user) {
    return (
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    );
  }

  return (
    <Routes>
      <Route element={<AppLayout />}>
        <Route index element={<HomePage />} />
        <Route
          path="attendance"
          element={
            <RequireRole role="TeamLead">
              <AttendancePage />
            </RequireRole>
          }
        />
        <Route path="calendar" element={<CalendarPage />} />
        <Route path="monthly" element={<MonthlySummaryPage />} />
        <Route path="bonuses" element={<BonusesPage />} />
        <Route
          path="daily-status"
          element={
            <RequireRole role="TeamLead">
              <DailyStatusPage />
            </RequireRole>
          }
        />
        <Route
          path="employees"
          element={
            <RequireRole role="TeamLead">
              <EmployeesPage />
            </RequireRole>
          }
        />
        <Route
          path="settings"
          element={
            <RequireRole role="TeamLead">
              <SettingsPage />
            </RequireRole>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
