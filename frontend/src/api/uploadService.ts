import { axiosClient } from './axiosClient.ts';

const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);

export interface UploadImageResult {
  url: string;
}

export const uploadService = {
  async uploadImage(file: File): Promise<UploadImageResult> {
    if (!ALLOWED_IMAGE_TYPES.has(file.type)) {
      throw new Error('Unsupported image format. Please select a JPEG, PNG, WebP, or GIF image.');
    }

    if (file.size > MAX_IMAGE_BYTES) {
      throw new Error('Image exceeds the maximum allowed size of 5 MB.');
    }

    const formData = new FormData();
    formData.append('file', file);

    const response = await axiosClient.post<UploadImageResult>('/uploads/images', formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
    });

    return response.data;
  },
};
