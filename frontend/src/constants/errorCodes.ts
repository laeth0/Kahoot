export const ERROR_MESSAGES: Record<string, string> = {
  'Auth.InvalidCredentials': 'The username or password you entered is incorrect.',
  'Auth.HostNotFound': 'Host account not found.',
  'Auth.RateLimited': 'Too many requests. Please wait a moment and try again.',
  'Auth.InvalidRefreshToken': 'Your session has expired. Please sign in again.',
  'Game.InvalidPin': 'No active game session found for that PIN.',
  'Game.NicknameTaken': 'That nickname is already taken in this game. Choose another!',
  'Game.NotJoinable': 'This game has already started or ended.',
  'Game.InvalidSessionToken': 'Your player session has expired. Rejoin the game to continue.',
  'Game.TooManyAnswerAttempts': 'Too many answer attempts. Please slow down.',
  'Game.QuestionClosed': 'Time is up for this question. Your answer was not counted.',
  'Game.QuestionNotActive': 'There is no question open for answers right now.',
  'Game.NotCurrentQuestion': "That is not the question that's open. Try the current one.",
  'Game.ChoiceNotInQuestion': "That answer doesn't belong to the current question.",
  'Game.ParticipantRemoved': 'You have been removed from the session by the host.',
  'Game.InvalidStateTransition': 'That action is not available for the current game state.',
  'Game.NoMoreQuestions': 'That was the last question. End the game to see final results.',
  'Game.ConcurrentModification': 'The game changed while processing that. Please try again.',
  'Quiz.InUse': 'This quiz currently has an active session and cannot be modified.',
};

export function getFriendlyErrorMessage(
  codeOrMessage?: string | null,
  fallback = 'An unexpected error occurred',
): string {
  if (!codeOrMessage) return fallback;
  return ERROR_MESSAGES[codeOrMessage] ?? codeOrMessage;
}
