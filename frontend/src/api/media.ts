const API_ORIGIN = (import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api').replace(
  /\/api\/?$/,
  '',
);

export function resolveMediaUrl(path: string | null | undefined): string | null {
  if (!path) {
    return null;
  }
  if (/^(https?:)?\/\//.test(path) || path.startsWith('data:') || path.startsWith('blob:')) {
    return path;
  }
  return `${API_ORIGIN}${path.startsWith('/') ? '' : '/'}${path}`;
}
