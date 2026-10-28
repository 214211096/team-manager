import { useState } from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import {
  AppBar,
  Box,
  Button,
  Chip,
  Divider,
  Drawer,
  Fab,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import HomeIcon from '@mui/icons-material/Home';
import HowToRegIcon from '@mui/icons-material/HowToReg';
import AssessmentIcon from '@mui/icons-material/Assessment';
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth';
import EventNoteIcon from '@mui/icons-material/EventNote';
import PaymentsIcon from '@mui/icons-material/Payments';
import GroupIcon from '@mui/icons-material/Group';
import SettingsIcon from '@mui/icons-material/Settings';
import { roleLabel, useAuth } from '../auth/AuthContext';
import ChangePasswordDialog from '../auth/ChangePasswordDialog';
import AssistantPanel from '../assistant/AssistantPanel';
import SmartToyIcon from '@mui/icons-material/SmartToy';
import type { UserRole } from '../api/types';

const DRAWER_WIDTH = 240;

interface NavItem {
  to: string;
  label: string;
  icon: React.ReactNode;
  roles: UserRole[];
  /** מסך שעוד לא נבנה; מוצג בתפריט כ"בקרוב" כדי שיהיה ברור מה מתוכנן. */
  soon?: boolean;
}

const navItems: NavItem[] = [
  { to: '/', label: 'לוח בית', icon: <HomeIcon />, roles: ['TeamLead', 'Manager'] },
  { to: '/attendance', label: 'נוכחות יומית', icon: <HowToRegIcon />, roles: ['TeamLead'] },
  { to: '/calendar', label: 'לוח שנה', icon: <EventNoteIcon />, roles: ['TeamLead', 'Manager'] },
  { to: '/daily-status', label: 'סטטוס יומי', icon: <AssessmentIcon />, roles: ['TeamLead'] },
  { to: '/monthly', label: 'סיכום חודשי', icon: <CalendarMonthIcon />, roles: ['TeamLead', 'Manager'] },
  { to: '/bonuses', label: 'בונוסים', icon: <PaymentsIcon />, roles: ['TeamLead', 'Manager'] },
  { to: '/employees', label: 'ניהול עובדות', icon: <GroupIcon />, roles: ['TeamLead'] },
  { to: '/settings', label: 'הגדרות', icon: <SettingsIcon />, roles: ['TeamLead'] },
];

export default function AppLayout() {
  const { user, logout } = useAuth();
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [changingPassword, setChangingPassword] = useState(false);
  const [assistantOpen, setAssistantOpen] = useState(false);
  const location = useLocation();

  const items = navItems.filter((i) => user && i.roles.includes(user.role));

  const drawerContent = (
    <Box component="nav" aria-label="ניווט ראשי">
      <Toolbar>
        <Typography variant="h6" component="div" sx={{ fontWeight: 700 }}>
          ניהול צוות
        </Typography>
      </Toolbar>
      <Divider />
      <List>
        {items.map((item) => (
          <ListItemButton
            key={item.to}
            component={item.soon ? 'div' : NavLink}
            {...(item.soon ? {} : { to: item.to, end: item.to === '/' })}
            disabled={item.soon}
            selected={!item.soon && location.pathname === item.to}
            onClick={() => setMobileOpen(false)}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
            {item.soon && <Chip label="בקרוב" size="small" variant="outlined" />}
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        // ml/anchor="left" כי תוסף ה-RTL של stylis הופך שמאל וימין, כך שבפועל זה בצד ימין.
        sx={{ width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, ml: { md: `${DRAWER_WIDTH}px` } }}
      >
        <Toolbar sx={{ gap: 1 }}>
          {!isDesktop && (
            <IconButton color="inherit" edge="start" aria-label="פתיחת תפריט" onClick={() => setMobileOpen(true)}>
              <MenuIcon />
            </IconButton>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Typography variant="body2" sx={{ display: { xs: 'none', sm: 'block' } }}>
            {user?.displayName ?? user?.username} · {user && roleLabel[user.role]}
          </Typography>
          <Button color="inherit" onClick={() => setChangingPassword(true)}>
            שינוי סיסמה
          </Button>
          <Button color="inherit" onClick={logout}>
            יציאה
          </Button>
        </Toolbar>
      </AppBar>

      <Box component="aside" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant={isDesktop ? 'permanent' : 'temporary'}
          anchor="left"
          open={isDesktop || mobileOpen}
          onClose={() => setMobileOpen(false)}
          sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}
        >
          {drawerContent}
        </Drawer>
      </Box>

      {/* ריווח תחתון כדי שכפתור העוזר לא יסתיר את כפתורי השמירה בתחתית המסכים. */}
      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, p: { xs: 2, md: 3 }, pb: { xs: 12, md: 12 } }}>
        <Toolbar />
        <Outlet />
      </Box>

      {changingPassword && <ChangePasswordDialog onClose={() => setChangingPassword(false)} />}

      <Fab
        color="primary"
        variant="extended"
        aria-label="עוזר AI"
        onClick={() => setAssistantOpen(true)}
        // right מוצג משמאל (תוסף ה-RTL הופך צדדים): הכפתור בפינה הנגדית לתפריט.
        sx={{ position: 'fixed', bottom: 24, right: 24, gap: 1 }}
      >
        <SmartToyIcon />
        עוזר AI
      </Fab>
      <AssistantPanel open={assistantOpen} onClose={() => setAssistantOpen(false)} />
    </Box>
  );
}
