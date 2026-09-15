export type GameStatus =
  'Created' | 'Lobby' | 'QuestionActive' | 'QuestionResults' | 'Leaderboard' | 'Finished';

export interface RealtimeError {
  code: string;
  description: string;
}

export interface RealtimeResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: RealtimeError;
}

export interface GameParticipantResponse {
  id: string;
  nickname: string;
  totalScore: number;
  rank?: number | null;
  isConnected: boolean;
  isRemoved: boolean;
}

export type ParticipantPresenceReason = 'Joined' | 'Reconnected' | 'Disconnected' | 'Removed';

export interface ParticipantPresenceResponse {
  participantCount: number;
  presenceVersion: number;
  reason: ParticipantPresenceReason;
  participant: GameParticipantResponse;
}

export interface GameDataSyncState {
  status: 'current' | 'stale';
  message: string | null;
  lastSuccessfulAt: number | null;
}

export interface HostChoiceResponse {
  id: string;
  orderIndex: number;
  text: string;
  isCorrect: boolean;
}

export interface HostQuestionResponse {
  questionId: string;
  questionIndex: number;
  totalQuestions: number;
  text: string;
  imageUrl?: string | null;
  timeLimitSeconds: number;
  startedAt: string;
  endsAt: string;
  correctChoiceIds: string[];
  choices: HostChoiceResponse[];
}

export interface ChoiceResultResponse {
  choiceId: string;
  text: string;
  answerCount: number;
  isCorrect: boolean;
}

export interface QuestionResultsResponse {
  questionId: string;
  questionIndex: number;
  correctChoiceIds: string[];
  participantCount: number;
  answerCount: number;
  choices: ChoiceResultResponse[];
}

export interface AnswerAckResponse {
  accepted: boolean;
  alreadyAnswered: boolean;
}

export interface LeaderboardEntryResponse {
  rank: number;
  participantId: string;
  nickname: string;
  totalScore: number;
}

export interface LeaderboardResponse {
  entries: LeaderboardEntryResponse[];
}

export interface PlayerChoiceResponse {
  id: string;
  orderIndex: number;
  text: string;
}

export interface PlayerQuestionResponse {
  questionId: string;
  questionIndex: number;
  totalQuestions: number;
  text: string;
  imageUrl?: string | null;
  timeLimitSeconds: number;
  endsAt: string;
  choices: PlayerChoiceResponse[];
  allowMultipleAnswers?: boolean;
}

export interface PlayerGameStateResponse {
  gameId: string;
  status: number | string;
  participantId: string;
  nickname: string;
  totalScore: number;
  rank?: number | null;
  participantCount: number;
  presenceVersion: number;
  alreadyAnsweredCurrentQuestion: boolean;
  currentQuestion?: PlayerQuestionResponse | null;
  lastQuestionResults?: QuestionResultsResponse | null;
  leaderboard?: LeaderboardResponse | null;
}

export interface GameClientEvents {
  ParticipantPresenceChanged: (payload: ParticipantPresenceResponse) => void;
  ParticipantRemoved: (participantId: string) => void;
  QuestionStarted: (payload: PlayerQuestionResponse) => void;
  QuestionStartedForHost: (payload: HostQuestionResponse) => void;
  QuestionEnded: (payload: QuestionResultsResponse) => void;
  LeaderboardUpdated: (payload: LeaderboardResponse) => void;
  GameEnded: (payload: LeaderboardResponse) => void;
}
