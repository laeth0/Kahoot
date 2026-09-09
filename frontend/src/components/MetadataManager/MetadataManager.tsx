import { useEffect } from 'react';

export interface MetadataManagerProps {
  title: string;
  description?: string;
  noindex?: boolean;
  canonicalUrl?: string;
}

const BRAND_SUFFIX = 'IEEEXtreme Kahoot';

export function MetadataManager({
  title,
  description,
  noindex = false,
  canonicalUrl,
}: MetadataManagerProps) {
  useEffect(() => {
    const originalTitle = document.title;
    document.title = title ? `${title} | ${BRAND_SUFFIX}` : BRAND_SUFFIX;

    let metaDescription = document.querySelector<HTMLMetaElement>('meta[name="description"]');
    if (!metaDescription) {
      metaDescription = document.createElement('meta');
      metaDescription.name = 'description';
      document.head.appendChild(metaDescription);
    }
    const prevDescription = metaDescription.content;
    if (description) {
      metaDescription.content = description;
    }

    let metaRobots = document.querySelector<HTMLMetaElement>('meta[name="robots"]');
    if (!metaRobots) {
      metaRobots = document.createElement('meta');
      metaRobots.name = 'robots';
      document.head.appendChild(metaRobots);
    }
    const prevRobots = metaRobots.content;
    metaRobots.content = noindex ? 'noindex, nofollow' : 'index, follow';

    let linkCanonical = document.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!linkCanonical) {
      linkCanonical = document.createElement('link');
      linkCanonical.rel = 'canonical';
      document.head.appendChild(linkCanonical);
    }
    const prevCanonical = linkCanonical.href;
    if (canonicalUrl) {
      linkCanonical.href = canonicalUrl;
    } else {
      linkCanonical.href = window.location.origin + window.location.pathname;
    }

    return () => {
      document.title = originalTitle;
      if (metaDescription) metaDescription.content = prevDescription;
      if (metaRobots) metaRobots.content = prevRobots;
      if (linkCanonical) linkCanonical.href = prevCanonical;
    };
  }, [title, description, noindex, canonicalUrl]);

  return null;
}

export default MetadataManager;
