export type GameStatus =
  | 'Created'
  | 'Lobby'
  | 'QuestionActive'
  | 'QuestionResults'
  | 'Leaderboard'
  | 'Finished';

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

export interface HostChoiceResponse {
  id: string;
  orderIndex: number;
  text?: string | null;
  imageUrl?: string | null;
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
  correctChoiceId: string;
  choices: HostChoiceResponse[];
}

export interface ChoiceResultResponse {
  choiceId: string;
  text?: string | null;
  answerCount: number;
  isCorrect: boolean;
}

export interface QuestionResultsResponse {
  questionId: string;
  questionIndex: number;
  correctChoiceId: string;
  participantCount: number;
  answerCount: number;
  choices: ChoiceResultResponse[];
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

export interface GameClientEvents {
  ParticipantJoined: (participant: GameParticipantResponse) => void;
  ParticipantLeft: (participantId: string) => void;
  ParticipantRemoved: (participantId: string) => void;
  QuestionStartedForHost: (payload: HostQuestionResponse) => void;
  QuestionEnded: (payload: QuestionResultsResponse) => void;
  LeaderboardUpdated: (payload: LeaderboardResponse) => void;
  GameEnded: (payload: LeaderboardResponse) => void;
}
