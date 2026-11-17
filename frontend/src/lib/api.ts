import type { Account, CandidateDetail, CandidateInput, Company, CompanyReport, ExperienceItem, InterviewerDetail, MarketReport, Page, PrivateCandidateDetail, PrivateInterviewerDetail, PrivateProfile, Question, Tag, TokenResponse } from './types'

const base = (import.meta.env.VITE_API_BASE_URL || '').replace(/\/$/, '')
let accessToken: string | null = null
export const tokenStore = {
  get: () => accessToken,
  set: (value: string | null) => { accessToken = value },
}

export class ApiError extends Error {
  constructor(public status: number, message: string, public errors?: Record<string, string[]>) { super(message) }
}

export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers)
  if (options.body) headers.set('Content-Type', 'application/json')
  if (accessToken) headers.set('Authorization', `Bearer ${accessToken}`)
  const response = await fetch(`${base}${path}`, { ...options, headers })
  if (!response.ok) {
    const payload = await response.json().catch(() => null) as { error?: string; title?: string; errors?: Record<string, string[]> } | null
    const message = response.status === 401 ? 'برای ادامه وارد حساب شوید.'
      : response.status === 403 ? 'دسترسی لازم برای این بخش را ندارید.'
        : response.status === 429 ? 'تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید.'
          : payload?.error || payload?.title || `خطای سرور (${response.status})`
    throw new ApiError(response.status, message, payload?.errors)
  }
  return response.status === 204 ? undefined as T : await response.json() as T
}

export function queryString(values: Record<string, string | number | null | undefined>) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  return params.toString()
}

export const api = {
  login: (body: { userNameOrEmail: string; password: string }) => request<TokenResponse>('/api/auth/login', { method: 'POST', body: JSON.stringify(body) }),
  register: (body: { userName: string; email: string; password: string; role: 'Candidate' | 'Interviewer' }) => request<TokenResponse>('/api/auth/register', { method: 'POST', body: JSON.stringify(body) }),
  me: () => request<Account>('/api/auth/me'),
  addRole: (role: 'Candidate' | 'Interviewer') => request<void>(`/api/auth/roles/${role}`, { method: 'POST' }),
  experiences: (params: Record<string, string | number | undefined>, signal?: AbortSignal) => request<Page<ExperienceItem>>(`/api/experiences?${queryString(params)}`, { signal }),
  companies: (signal?: AbortSignal) => request<Company[]>('/api/companies', { signal }),
  candidate: (id: string, signal?: AbortSignal) => request<CandidateDetail>(`/api/candidate-experiences/${id}`, { signal }),
  interviewer: (id: string, signal?: AbortSignal) => request<InterviewerDetail>(`/api/interviewer-experiences/${id}`, { signal }),
  privateCandidate: (id: string) => request<PrivateCandidateDetail>(`/api/candidate-experiences/${id}/mine`),
  privateInterviewer: (id: string) => request<PrivateInterviewerDetail>(`/api/interviewer-experiences/${id}/mine`),
  submitCandidate: (body: CandidateInput) => request<PrivateCandidateDetail>('/api/candidate-experiences', { method: 'POST', body: JSON.stringify(body) }),
  submitInterviewer: (body: unknown) => request<PrivateInterviewerDetail>('/api/interviewer-experiences', { method: 'POST', body: JSON.stringify(body) }),
  questions: (params: Record<string, string | number | undefined>, signal?: AbortSignal) => request<Page<Question>>(`/api/questions?${queryString(params)}`, { signal }),
  question: (id: string, signal?: AbortSignal) => request<Question>(`/api/questions/${id}`, { signal }),
  tags: (signal?: AbortSignal) => request<Tag[]>('/api/tags', { signal }),
  market: (signal?: AbortSignal) => request<MarketReport>('/api/reports/market', { signal }),
  companyReport: (id: string, signal?: AbortSignal) => request<CompanyReport>(`/api/reports/companies/${id}`, { signal }),
  reportContent: (experienceId: string, reason: string) => request<{ id: string; status: string }>('/api/content-reports', { method: 'POST', body: JSON.stringify({ experienceId, reason }) }),
  myProfile: () => request<PrivateProfile>('/api/interviewer-profiles/me'),
  saveProfile: (body: unknown) => request<PrivateProfile>('/api/interviewer-profiles/me', { method: 'PUT', body: JSON.stringify(body) }),
  pending: () => request<{ type: 'CandidateExperience' | 'InterviewerExperience'; id: string; interviewDate: string; createdAt: string }[]>('/api/admin/experiences/pending'),
  moderate: (kind: string, id: string, action: 'publish' | 'reject', reason: string) => request<void>(`/api/admin/experiences/${kind}/${id}/${action}`, { method: 'POST', body: JSON.stringify({ reason }) }),
  pendingProfiles: () => request<{ userId: string; publicId: string; updatedAt: string }[]>('/api/admin/interviewer-profiles/pending'),
  adminProfile: (userId: string) => request<PrivateProfile>(`/api/admin/interviewer-profiles/${userId}`),
  moderateProfile: (userId: string, action: 'publish' | 'reject', reason: string) => request<void>(`/api/admin/interviewer-profiles/${userId}/${action}`, { method: 'POST', body: JSON.stringify({ reason }) }),
  contentReports: () => request<{ id: string; interviewExperienceId: string; reason: string; createdAt: string }[]>('/api/admin/content-reports'),
  moderateReport: (id: string, action: 'resolve' | 'dismiss') => request<void>(`/api/admin/content-reports/${id}/${action}`, { method: 'POST' }),
}
