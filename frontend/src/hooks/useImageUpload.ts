import { useCallback, useState } from 'react';

import { uploadService } from '../api/uploadService.ts';

export function useImageUpload() {
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [url, setUrl] = useState<string | null>(null);

  const upload = useCallback(async (file: File): Promise<string> => {
    setIsUploading(true);
    setError(null);
    try {
      const result = await uploadService.uploadImage(file);
      setUrl(result.url);
      return result.url;
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Image upload failed';
      setError(msg);
      throw err;
    } finally {
      setIsUploading(false);
    }
  }, []);

  const clear = useCallback(() => {
    setUrl(null);
    setError(null);
    setIsUploading(false);
  }, []);

  return {
    isUploading,
    error,
    url,
    upload,
    clear,
    setUrl,
  };
}

export default useImageUpload;
