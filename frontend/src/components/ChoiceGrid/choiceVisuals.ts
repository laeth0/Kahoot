export const CHOICE_LETTERS = ['A', 'B', 'C', 'D', 'E', 'F'] as const;

export const CHOICE_COLORS = [
  '#B91C1C',
  '#1D4ED8',
  '#B45309',
  '#047857',
  '#6D28D9',
  '#0E7490',
] as const;

export function choiceLetter(index: number): string {
  return CHOICE_LETTERS[index % CHOICE_LETTERS.length];
}

export function choiceColor(index: number): string {
  return CHOICE_COLORS[index % CHOICE_COLORS.length];
}
