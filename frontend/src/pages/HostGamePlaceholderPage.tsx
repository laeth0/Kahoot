import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import SportsEsportsIcon from '@mui/icons-material/SportsEsports';
import { Box, Button, Card, CardContent, Container, Typography } from '@mui/material';
import { useNavigate, useParams } from 'react-router-dom';

import { MetadataManager } from '../components/MetadataManager/MetadataManager.tsx';

export function HostGamePlaceholderPage() {
  const { gameId } = useParams<{ gameId: string }>();
  const navigate = useNavigate();

  return (
    <Box
      sx={{ minHeight: '80vh', display: 'flex', alignItems: 'center', py: 6, bgcolor: '#f4f8fc' }}
    >
      <MetadataManager
        title="Host Game Lobby - Kahoot"
        description="Host game session created. Live lobby coming in Phase 3."
      />

      <Container maxWidth="sm">
        <Card
          variant="outlined"
          sx={{
            p: 4,
            textAlign: 'center',
            borderRadius: 3,
            borderColor: '#e2e8f0',
            bgcolor: '#ffffff',
            boxShadow: '0 8px 30px rgba(0, 98, 155, 0.08)',
          }}
        >
          <CardContent>
            <Box
              sx={{
                width: 72,
                height: 72,
                borderRadius: '50%',
                bgcolor: 'rgba(0, 98, 155, 0.1)',
                color: '#00629b',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                mx: 'auto',
                mb: 2.5,
              }}
            >
              <SportsEsportsIcon sx={{ fontSize: 40 }} />
            </Box>

            <Typography variant="h5" sx={{ fontWeight: 800, color: '#09131f', mb: 1 }}>
              Game Session Initialized!
            </Typography>

            <Typography variant="body1" sx={{ color: '#486581', mb: 3 }}>
              Your game session has been created on the backend.
            </Typography>

            <Box
              sx={{
                bgcolor: '#f0f4f8',
                p: 2,
                borderRadius: 2,
                mb: 3,
                border: '1px solid #cbd5e1',
              }}
            >
              <Typography variant="caption" sx={{ color: '#627d98', display: 'block', mb: 0.5 }}>
                Session ID
              </Typography>
              <Typography
                variant="body2"
                sx={{
                  fontFamily: 'monospace',
                  fontWeight: 700,
                  color: '#00629b',
                  wordBreak: 'break-all',
                }}
              >
                {gameId}
              </Typography>
            </Box>

            <Typography variant="body2" sx={{ color: '#627d98', mb: 4 }}>
              The interactive live host lobby and real-time SignalR player coordination controls are
              being prepared for Phase 3.
            </Typography>

            <Button
              variant="contained"
              color="primary"
              startIcon={<ArrowBackIcon />}
              onClick={() => navigate('/host/quizzes')}
              sx={{ fontWeight: 700, px: 3, py: 1.25, borderRadius: 2 }}
            >
              Back to Quiz Library
            </Button>
          </CardContent>
        </Card>
      </Container>
    </Box>
  );
}

export default HostGamePlaceholderPage;
