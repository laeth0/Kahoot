export type GameStatus =
  | 'Created'
  | 'Lobby'
  | 'QuestionActive'
  | 'QuestionResults'
  | 'Leaderboard'
  | 'Finished';

export const GAME_PHASES: { id: GameStatus; label: string }[] = [
  { id: 'Lobby', label: 'Lobby' },
  { id: 'QuestionActive', label: 'Question' },
  { id: 'QuestionResults', label: 'Results' },
  { id: 'Leaderboard', label: 'Leaderboard' },
  { id: 'Finished', label: 'Finished' },
];

export function getActiveStepIndex(status: GameStatus): number {
  switch (status) {
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
