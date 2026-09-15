const API_ORIGIN = (import.meta.env.VITE_API_URL ?? '').replace(/\/api\/?$/, '');

const CANONICAL_MEDIA_REGEX =
  /^\/uploads\/([0-9a-fA-F]{32}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\.(jpg|jpeg|png|webp)$/i;

export function isValidMediaUrl(path: string | null | undefined): boolean {
  if (!path || typeof path !== 'string') {
    return false;
  }
  return CANONICAL_MEDIA_REGEX.test(path.trim());
}

export function resolveMediaUrl(path: string | null | undefined): string | null {
  if (!path || typeof path !== 'string') {
    return null;
  }
  const trimmed = path.trim();
  if (!CANONICAL_MEDIA_REGEX.test(trimmed)) {
    return null;
  }
  return `${API_ORIGIN}${trimmed}`;
}
