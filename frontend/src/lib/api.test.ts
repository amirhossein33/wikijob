import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, queryString, request, tokenStore } from './api'
import { safeExternalUrl } from './urls'

afterEach(() => { vi.unstubAllGlobals(); tokenStore.set(null) })

describe('API boundary', () => {
  it('keeps filters in the URL and omits empty values', () => {
    expect(queryString({ page: 2, companyId: 'abc', tagId: undefined, kind: '' })).toBe('page=2&companyId=abc')
  })
  it('sends a bearer token and preserves cancellation', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ page: 1 }), { status: 200 }))
    vi.stubGlobal('fetch', fetchMock)
    tokenStore.set('sample-token')
    const controller = new AbortController()
    await request('/api/experiences', { signal: controller.signal })
    expect(fetchMock).toHaveBeenCalledWith('/api/experiences', expect.objectContaining({ signal: controller.signal }))
    expect((fetchMock.mock.calls[0][1].headers as Headers).get('Authorization')).toBe('Bearer sample-token')
  })
  it.each([401, 403, 429])('maps HTTP %i to a readable Persian error', async status => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('{}', { status })))
    await expect(request('/api/auth/me')).rejects.toMatchObject({ status, message: expect.stringMatching(/[\u0600-\u06ff]/) } satisfies Partial<ApiError>)
  })
  it('rejects unsafe external job-ad links', () => {
    expect(safeExternalUrl('javascript:alert(1)')).toBeNull()
    expect(safeExternalUrl('https://example.com/job')).toBe('https://example.com/job')
  })
})
