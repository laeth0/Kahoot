export type NormalizedGameStatus =
  'Created' | 'Lobby' | 'QuestionActive' | 'QuestionResults' | 'Leaderboard' | 'Finished';

export type GameStatus = NormalizedGameStatus | number | string;

export const GAME_PHASES: { id: NormalizedGameStatus; label: string }[] = [
  { id: 'Lobby', label: 'Lobby' },
  { id: 'QuestionActive', label: 'Question' },
  { id: 'QuestionResults', label: 'Results' },
  { id: 'Leaderboard', label: 'Leaderboard' },
  { id: 'Finished', label: 'Finished' },
];

export function normalizeGameStatus(status: GameStatus): NormalizedGameStatus {
  if (status === 0 || status === '0' || status === 'Created') return 'Created';
  if (status === 1 || status === '1' || status === 'Lobby') return 'Lobby';
  if (status === 2 || status === '2' || status === 'QuestionActive') return 'QuestionActive';
  if (status === 3 || status === '3' || status === 'QuestionResults') return 'QuestionResults';
  if (status === 4 || status === '4' || status === 'Leaderboard') return 'Leaderboard';
  if (status === 5 || status === '5' || status === 'Finished') return 'Finished';
  return 'Lobby';
}

export function isLobbyStatus(status: GameStatus): boolean {
  const s = normalizeGameStatus(status);
  return s === 'Lobby' || s === 'Created';
}

export function isFinishedStatus(status: GameStatus): boolean {
  return normalizeGameStatus(status) === 'Finished';
}

export function getActiveStepIndex(status: GameStatus): number {
  const normalized = normalizeGameStatus(status);
  switch (normalized) {
    case 'Created':
    case 'Lobby':
      return 0;
    case 'QuestionActive':
      return 1;
    case 'QuestionResults':
      return 2;
    case 'Leaderboard':
      return 3;
    case 'Finished':
      return 4;
    default:
      return 0;
  }
}
