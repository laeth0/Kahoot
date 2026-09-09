import { useEffect } from 'react';

export interface MetadataManagerProps {
  /** Page title without the brand suffix */
  title: string;
  /** Meta description for search engines and social cards */
  description?: string;
  /** When true or when on non-public routes, sets robots to noindex, nofollow */
  noindex?: boolean;
  /** Optional canonical URL override */
  canonicalUrl?: string;
}

const BRAND_SUFFIX = 'IEEEXtreme Kahoot';

/**
 * Centralized metadata boundary managing document head properties:
 * title, meta description, robots indexing rules, and canonical link.
 */
export function MetadataManager({
  title,
  description,
  noindex = false,
  canonicalUrl,
}: MetadataManagerProps) {
  useEffect(() => {
    // 1. Update Title
    const originalTitle = document.title;
    document.title = title ? `${title} | ${BRAND_SUFFIX}` : BRAND_SUFFIX;

    // 2. Update Meta Description
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

    // 3. Update Robots Tag
    let metaRobots = document.querySelector<HTMLMetaElement>('meta[name="robots"]');
    if (!metaRobots) {
      metaRobots = document.createElement('meta');
      metaRobots.name = 'robots';
      document.head.appendChild(metaRobots);
    }
    const prevRobots = metaRobots.content;
    metaRobots.content = noindex ? 'noindex, nofollow' : 'index, follow';

    // 4. Update Canonical Link
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
