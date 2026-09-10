import { useEffect } from 'react';

export interface MetadataManagerProps {
  title: string;
  description?: string;
  noindex?: boolean;
  canonicalUrl?: string;
  ogImage?: string;
}

const BRAND_SUFFIX = 'IEEEXtreme Kahoot';

function setMetaTag(attributeName: 'name' | 'property', key: string, content: string): () => void {
  let element = document.querySelector<HTMLMetaElement>(`meta[${attributeName}="${key}"]`);
  const created = !element;
  const prevContent = element?.content ?? null;

  if (!element) {
    element = document.createElement('meta');
    element.setAttribute(attributeName, key);
    document.head.appendChild(element);
  }

  element.content = content;

  return () => {
    if (created) {
      element?.remove();
    } else if (element && prevContent !== null) {
      element.content = prevContent;
    }
  };
}

export function MetadataManager({
  title,
  description,
  noindex = false,
  canonicalUrl,
  ogImage,
}: MetadataManagerProps) {
  useEffect(() => {
    const originalTitle = document.title;
    const fullTitle = title ? `${title} | ${BRAND_SUFFIX}` : BRAND_SUFFIX;
    document.title = fullTitle;

    const cleanupFns: Array<() => void> = [];

    if (description) {
      cleanupFns.push(setMetaTag('name', 'description', description));
      cleanupFns.push(setMetaTag('property', 'og:description', description));
      cleanupFns.push(setMetaTag('name', 'twitter:description', description));
    }

    cleanupFns.push(setMetaTag('name', 'robots', noindex ? 'noindex, nofollow' : 'index, follow'));

    cleanupFns.push(setMetaTag('property', 'og:title', fullTitle));
    cleanupFns.push(setMetaTag('property', 'og:type', 'website'));
    cleanupFns.push(setMetaTag('name', 'twitter:card', 'summary_large_image'));
    cleanupFns.push(setMetaTag('name', 'twitter:title', fullTitle));

    const resolvedCanonical =
      canonicalUrl || `${window.location.origin}${window.location.pathname}`;
    cleanupFns.push(setMetaTag('property', 'og:url', resolvedCanonical));

    const resolvedImage = ogImage || `${window.location.origin}/logo.jpeg`;
    cleanupFns.push(setMetaTag('property', 'og:image', resolvedImage));
    cleanupFns.push(setMetaTag('name', 'twitter:image', resolvedImage));

    let linkCanonical = document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    const createdCanonical = !linkCanonical;
    const prevCanonical = linkCanonical?.href ?? null;

    if (!linkCanonical) {
      linkCanonical = document.createElement('link');
      linkCanonical.rel = 'canonical';
      document.head.appendChild(linkCanonical);
    }
    linkCanonical.href = resolvedCanonical;

    return () => {
      document.title = originalTitle;
      for (const cleanup of cleanupFns) {
        cleanup();
      }
      if (createdCanonical) {
        linkCanonical?.remove();
      } else if (linkCanonical && prevCanonical !== null) {
        linkCanonical.href = prevCanonical;
      }
    };
  }, [title, description, noindex, canonicalUrl, ogImage]);

  return null;
}

export default MetadataManager;
