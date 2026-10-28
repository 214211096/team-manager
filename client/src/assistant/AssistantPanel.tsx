import { useEffect, useRef, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Drawer,
  IconButton,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import SendIcon from '@mui/icons-material/Send';
import { api, ApiError } from '../api/client';
import type { AssistantTurn } from '../api/types';

const STORAGE_KEY = 'tm_assistant_chat';

const EXAMPLES = [
  'כמה בונוס קיבלה שרה בחודש אפריל?',
  'באילו ימים רות הייתה חסרה בחודש אוקטובר?',
  'למי היה ממוצע העמידה ביעד הכי גבוה בחודש שעבר?',
];

// השיחה נשמרת רק בלשונית הנוכחית, כדי שלא תיעלם במעבר בין מסכים.
function loadChat(): AssistantTurn[] {
  try {
    return JSON.parse(sessionStorage.getItem(STORAGE_KEY) ?? '[]') as AssistantTurn[];
  } catch {
    return [];
  }
}

function saveChat(turns: AssistantTurn[]) {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(turns));
  } catch {
    // אחסון חסום (למשל גלישה פרטית): השיחה פשוט לא תישמר.
  }
}

/** חלון צ'אט עם עוזר ה-AI. נפתח מכפתור בפינת המסך. */
export default function AssistantPanel({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [turns, setTurns] = useState<AssistantTurn[]>(loadChat);
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [enabled, setEnabled] = useState<boolean | null>(null);
  const bottomRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open || enabled !== null) return;
    api
      .assistantStatus()
      .then((s) => setEnabled(s.enabled))
      .catch(() => setEnabled(false));
  }, [open, enabled]);

  useEffect(() => {
    saveChat(turns);
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [turns, busy]);

  const ask = async (question: string) => {
    const q = question.trim();
    if (!q || busy) return;
    const next: AssistantTurn[] = [...turns, { role: 'user', content: q }];
    setTurns(next);
    setInput('');
    setError(null);
    setBusy(true);
    try {
      const { answer } = await api.assistantChat(next);
      setTurns([...next, { role: 'assistant', content: answer }]);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'לא ניתן להגיע לעוזר');
      // השאלה נשארת בשדה כדי שאפשר יהיה לשלוח שוב.
      setTurns(turns);
      setInput(q);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Drawer
      // anchor="right" מוצג בצד שמאל: תוסף ה-RTL של stylis הופך צדדים. כך העוזר בצד הנגדי לתפריט.
      anchor="right"
      open={open}
      onClose={onClose}
      sx={{ '& .MuiDrawer-paper': { width: { xs: '100%', sm: 420 }, display: 'flex', flexDirection: 'column' } }}
    >
      <Stack direction="row" sx={{ alignItems: 'center', p: 1.5, gap: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Typography variant="h6" component="h2" sx={{ fontWeight: 700, flexGrow: 1 }}>
          עוזר AI
        </Typography>
        {turns.length > 0 && (
          <Button size="small" onClick={() => setTurns([])} disabled={busy}>
            שיחה חדשה
          </Button>
        )}
        <IconButton aria-label="סגירת העוזר" onClick={onClose}>
          <CloseIcon />
        </IconButton>
      </Stack>

      <Box sx={{ flexGrow: 1, overflowY: 'auto', p: 2 }} aria-live="polite">
        {enabled === false && (
          <Alert severity="info" sx={{ mb: 2 }}>
            העוזר עוד לא הוגדר: צריך להוסיף מפתח API של Anthropic בהגדרות השרת.
          </Alert>
        )}

        {turns.length === 0 && enabled !== false && (
          <Stack spacing={1.5}>
            <Typography color="text.secondary">
              אפשר לשאול על נוכחות, ביצוע, בונוסים ולוח השנה. למשל:
            </Typography>
            {EXAMPLES.map((ex) => (
              <Chip
                key={ex}
                label={ex}
                variant="outlined"
                onClick={() => ask(ex)}
                sx={{ justifyContent: 'flex-start', height: 'auto', py: 0.75, '& .MuiChip-label': { whiteSpace: 'normal' } }}
              />
            ))}
          </Stack>
        )}

        <Stack spacing={1.5}>
          {turns.map((t, i) => (
            <Box
              key={i}
              sx={{
                alignSelf: t.role === 'user' ? 'flex-start' : 'flex-end',
                maxWidth: '85%',
                px: 1.5,
                py: 1,
                borderRadius: 2,
                bgcolor: t.role === 'user' ? 'primary.main' : 'grey.100',
                color: t.role === 'user' ? 'primary.contrastText' : 'text.primary',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
              }}
            >
              {t.content}
            </Box>
          ))}
          {busy && (
            <Stack direction="row" spacing={1} sx={{ alignSelf: 'flex-end', alignItems: 'center', color: 'text.secondary' }}>
              <CircularProgress size={16} />
              <Typography variant="body2">בודקת בנתונים...</Typography>
            </Stack>
          )}
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
        <div ref={bottomRef} />
      </Box>

      <Box
        component="form"
        onSubmit={(e) => {
          e.preventDefault();
          ask(input);
        }}
        sx={{ p: 1.5, borderTop: 1, borderColor: 'divider' }}
      >
        <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-end' }}>
          <TextField
            fullWidth
            multiline
            maxRows={4}
            size="small"
            placeholder="שאלה על נתוני הצוות..."
            value={input}
            disabled={busy || enabled === false}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                ask(input);
              }
            }}
            slotProps={{ htmlInput: { maxLength: 1000, 'aria-label': 'שאלה לעוזר' } }}
          />
          <IconButton type="submit" color="primary" aria-label="שליחה" disabled={busy || !input.trim() || enabled === false}>
            <SendIcon sx={{ transform: 'scaleX(-1)' }} />
          </IconButton>
        </Stack>
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
          השאלה והנתונים הרלוונטיים נשלחים לשירות AI חיצוני (Anthropic). העוזר רק קורא נתונים ולא משנה אותם.
        </Typography>
      </Box>
    </Drawer>
  );
}
