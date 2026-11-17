// User-supplied job-ad links are rendered only when they use an ordinary web scheme.
export function safeExternalUrl(value: string | null | undefined): string | null {
  if (!value) return null
  try { const url = new URL(value); return url.protocol === 'https:' || url.protocol === 'http:' ? url.toString() : null }
  catch { return null }
}
