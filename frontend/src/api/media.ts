const API_ORIGIN = (import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api').replace(
  /\/api\/?$/,
  '',
);

export function resolveMediaUrl(path: string | null | undefined): string | null {
  if (!path) {
    return null;
  }
  const trimmed = path.trim();
  if (!trimmed) {
    return null;
  }
  if (
    /^(https?:)?\/\//.test(trimmed) ||
    trimmed.startsWith('data:') ||
    trimmed.startsWith('blob:')
  ) {
    return trimmed;
  }
  return `${API_ORIGIN}${trimmed.startsWith('/') ? '' : '/'}${trimmed}`;
}
