export const VALIDATION = {
  PIN: {
    MIN_LENGTH: 4,
    MAX_LENGTH: 8,
    PATTERN: /^[0-9]+$/,
  },
  NICKNAME: {
    MIN_LENGTH: 2,
    MAX_LENGTH: 30,
  },
  HOST: {
    USERNAME_MIN_LENGTH: 3,
    USERNAME_MAX_LENGTH: 50,
    PASSWORD_MIN_LENGTH: 6,
  },
} as const;
