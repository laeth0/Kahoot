export interface ClipboardCopyResult {
  success: boolean;
  method: 'async-clipboard' | 'exec-command' | 'none';
  error?: string;
}

export async function copyToClipboard(text: string): Promise<ClipboardCopyResult> {
  const isClipboardApiAvailable =
    typeof navigator !== 'undefined' &&
    Boolean(navigator.clipboard && typeof navigator.clipboard.writeText === 'function');

  if (isClipboardApiAvailable) {
    const copiedWithClipboardApi = await navigator.clipboard.writeText(text).then(
      () => true,
      () => false,
    );
    if (copiedWithClipboardApi) {
      return { success: true, method: 'async-clipboard' };
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
        return { success: true, method: 'exec-command' };
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      return { success: false, method: 'none', error: errorMessage };
    }
  }

  return {
    success: false,
    method: 'none',
    error: 'Clipboard not supported in this environment',
  };
}
