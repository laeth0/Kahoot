export interface ClipboardCopyResult {
  success: boolean;
  method: 'async-clipboard' | 'exec-command' | 'none';
  error?: string;
}

export async function copyToClipboard(
  text: string,
  label: string = 'Content',
): Promise<ClipboardCopyResult> {
  const isClipboardApiAvailable =
    typeof navigator !== 'undefined' &&
    Boolean(navigator.clipboard && typeof navigator.clipboard.writeText === 'function');

  console.info(`[Clipboard] Copy requested: ${label}`, {
    environment: import.meta.env.MODE,
    isSecureContext: typeof window !== 'undefined' ? window.isSecureContext : false,
    clipboardSupport: isClipboardApiAvailable ? 'Supported' : 'Not Supported (Fallback active)',
    currentOrigin: typeof window !== 'undefined' ? window.location.origin : 'N/A',
  });

  if (isClipboardApiAvailable) {
    try {
      await navigator.clipboard.writeText(text);
      console.info(`[Clipboard] Copy Operation: Success (Async Clipboard API) for ${label}`);
      return { success: true, method: 'async-clipboard' };
    } catch (err) {
      console.warn(
        `[Clipboard] Async Clipboard API failed for ${label}, attempting fallback:`,
        err instanceof Error ? err.message : err,
      );
    }
  }

  if (typeof document !== 'undefined') {
    try {
      const textarea = document.createElement('textarea');
      textarea.value = text;
      textarea.setAttribute('readonly', '');
      textarea.style.position = 'fixed';
      textarea.style.top = '0';
      textarea.style.left = '0';
      textarea.style.width = '2em';
      textarea.style.height = '2em';
      textarea.style.padding = '0';
      textarea.style.border = 'none';
      textarea.style.outline = 'none';
      textarea.style.boxShadow = 'none';
      textarea.style.background = 'transparent';
      textarea.style.opacity = '0';

      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();
      textarea.setSelectionRange(0, textarea.value.length);

      const successful = document.execCommand('copy');
      document.body.removeChild(textarea);

      if (successful) {
        console.info(`[Clipboard] Copy Operation: Success (execCommand fallback) for ${label}`);
        return { success: true, method: 'exec-command' };
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      console.error(`[Clipboard] Copy Operation: Failed for ${label}`, { error: errorMessage });
      return { success: false, method: 'none', error: errorMessage };
    }
  }

  console.error(
    `[Clipboard] Copy Operation: Failed for ${label} (Clipboard not supported in this environment)`,
  );
  return {
    success: false,
    method: 'none',
    error: 'Clipboard not supported in this environment',
  };
}
