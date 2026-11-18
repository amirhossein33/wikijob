// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthContext, type AuthContextValue } from '../auth-context'
import { api } from '../lib/api'
import type { PrivateCandidateDetail, PrivateInterviewerDetail, PrivateProfile, TokenResponse } from '../lib/types'
import { AdminDashboard, Dashboard, SubmitInterviewer } from './account'
import { Login } from './auth'
import { SubmitExperience } from './submit'

const memberAccount: AuthContextValue = {
  account: { id: 'candidate-id', userName: 'sample', email: 'sample@example.test', roles: ['Candidate'] },
  loading: false, hasRole: role => role === 'Candidate',
  acceptToken: vi.fn(), refresh: vi.fn(), logout: vi.fn(),
}

const adminAccount: AuthContextValue = {
  account: { id: 'admin-id', userName: 'local-admin', email: 'admin@example.test', roles: ['Admin'] },
  loading: false, hasRole: role => role === 'Admin',
  acceptToken: vi.fn(), refresh: vi.fn(), logout: vi.fn(),
}

const candidateExperience: PrivateCandidateDetail = {
  id: 'candidate-experience-id', authorId: 'candidate-id', authorName: 'sample', isAuthorAnonymous: true,
  companyName: 'Acme Backend', interviewDate: '2026-11-15', level: 'Senior', outcome: 'Passed',
  description: 'Architecture and API design discussion.', jobAdText: 'Backend .NET role', jobAdUrl: null,
  interviewerConductRating: 4, jobAdAlignmentRating: 5, ratingReason: 'Matched the advertisement.', status: 'Pending',
  stages: [{ id: 'stage-id', order: 1, type: 'Technical', date: '2026-11-15', description: 'Technical round', questions: [{ questionId: 'question-id', text: 'Explain dependency injection.', type: 'Conceptual', perceivedDifficulty: 3 }] }],
}

const interviewerExperience: PrivateInterviewerDetail = {
  id: 'interviewer-experience-id', interviewerName: 'Hiring Lead', companyName: 'Acme Backend',
  showInterviewerName: false, showCompanyName: false, interviewDate: '2026-11-15', level: 'Senior',
  description: 'Candidate explained trade-offs clearly.', jobAdText: null, jobAdUrl: null, interviewRating: 4,
  status: 'Pending', questions: [{ text: 'Design a cache.', type: 'SystemDesign' }],
}

const profile: PrivateProfile = {
  userId: 'interviewer-id', publicId: 'public-profile-id', displayName: 'Senior Interviewer',
  companyName: 'Acme Backend', bio: 'Backend engineer', adviceText: 'Prioritize architecture and communication.',
  showName: false, showCompany: false, showBio: false, status: 'Published',
  priorities: [{ skill: 'Architecture', importance: 5, note: 'Explain trade-offs.' }],
}

function wrap(node: React.ReactNode, auth: AuthContextValue, route = '*', initialPath = '/') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={auth}>
        <MemoryRouter initialEntries={[initialPath]}><Routes><Route path={route} element={node} /></Routes></MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>,
  )
}

afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks() })

describe('form and moderation UI', () => {
  it('requires credentials before calling login', async () => {
    wrap(<Login />, { ...memberAccount, account: null, hasRole: () => false })
    fireEvent.click(screen.getByRole('button', { name: 'ورود به حساب' }))
    expect(await screen.findByText('نام کاربری یا ایمیل را وارد کنید.')).toBeTruthy()
    expect(screen.getByText('رمز عبور را وارد کنید.')).toBeTruthy()
  })

  it('signs in through the shared login page using an Admin token', async () => {
    const response: TokenResponse = { accessToken: 'admin-jwt', tokenType: 'Bearer', expiresAt: '2026-11-19T00:00:00Z', roles: ['Admin'] }
    const login = vi.spyOn(api, 'login').mockResolvedValue(response)
    const acceptToken = vi.fn().mockResolvedValue(undefined)
    wrap(<Login />, { ...adminAccount, account: null, hasRole: () => false, acceptToken })
    fireEvent.change(screen.getByLabelText('نام کاربری یا ایمیل'), { target: { value: 'local-admin' } })
    fireEvent.change(screen.getByLabelText('رمز عبور'), { target: { value: 'LocalAdminTest!4826' } })
    fireEvent.click(screen.getByRole('button', { name: 'ورود به حساب' }))
    await waitFor(() => expect(login).toHaveBeenCalledWith({ userNameOrEmail: 'local-admin', password: 'LocalAdminTest!4826' }))
    expect(acceptToken).toHaveBeenCalledWith(response)
  })

  it('does not advance past an interview stage without a description', async () => {
    vi.stubGlobal('scrollTo', vi.fn())
    wrap(<SubmitExperience />, memberAccount)
    fireEvent.click(screen.getByRole('button', { name: /مرحلهٔ بعد/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'مراحل مصاحبه' })).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: /مرحلهٔ بعد/ }))
    expect(screen.getByRole('alert').textContent).toContain('تاریخ معتبر و توضیح')
  })

  it('loads existing Candidate data into the edit form', async () => {
    vi.spyOn(api, 'privateCandidate').mockResolvedValue(candidateExperience)
    wrap(<SubmitExperience />, memberAccount, '/dashboard/submission/candidate/:id/edit', '/dashboard/submission/candidate/candidate-experience-id/edit')
    expect(await screen.findByRole('heading', { name: 'ویرایش تجربهٔ مصاحبه' })).toBeTruthy()
    expect(screen.getByLabelText('نام شرکت (اختیاری)')).toHaveProperty('value', 'Acme Backend')
  })

  it('loads existing Interviewer data into the edit form', async () => {
    const auth: AuthContextValue = { ...memberAccount, account: { ...memberAccount.account!, roles: ['Interviewer'] }, hasRole: role => role === 'Interviewer' }
    vi.spyOn(api, 'privateInterviewer').mockResolvedValue(interviewerExperience)
    wrap(<SubmitInterviewer />, auth, '/interviewer/:id/edit', '/interviewer/interviewer-experience-id/edit')
    expect(await screen.findByRole('heading', { name: 'ویرایش دیدگاه مصاحبه' })).toBeTruthy()
    expect(screen.getByLabelText('نام شرکت')).toHaveProperty('value', 'Acme Backend')
  })

  it('lists and soft deletes the signed-in Candidate’s own experience from the dashboard', async () => {
    vi.spyOn(api, 'myCandidateExperiences').mockResolvedValue([candidateExperience])
    const remove = vi.spyOn(api, 'deleteCandidate').mockResolvedValue(undefined)
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))
    wrap(<Dashboard />, memberAccount)
    expect(await screen.findByText('Acme Backend')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'ویرایش' }).getAttribute('href')).toBe('/dashboard/submission/candidate/candidate-experience-id/edit')
    fireEvent.click(screen.getByRole('button', { name: 'حذف' }))
    await waitFor(() => expect(remove.mock.calls[0]?.[0]).toBe('candidate-experience-id'))
  })

  it('shows Interviewer advice and deletes it from the dashboard', async () => {
    const auth: AuthContextValue = { ...memberAccount, account: { ...memberAccount.account!, roles: ['Interviewer'] }, hasRole: role => role === 'Interviewer' }
    vi.spyOn(api, 'myInterviewerExperiences').mockResolvedValue([])
    vi.spyOn(api, 'myProfile').mockResolvedValue(profile)
    const remove = vi.spyOn(api, 'deleteProfile').mockResolvedValue(undefined)
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true))
    wrap(<Dashboard />, auth)
    expect(await screen.findByText('Prioritize architecture and communication.')).toBeTruthy()
    expect(screen.getByText('Architecture · 5 از ۵')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'حذف توصیه‌ها' }))
    await waitFor(() => expect(remove).toHaveBeenCalledOnce())
  })

  it('requires a reason and sends Admin approve and reject decisions with that reason', async () => {
    vi.spyOn(api, 'pending').mockResolvedValue([
      { type: 'CandidateExperience', id: 'candidate-pending', interviewDate: '2026-11-15', createdAt: '2026-11-15T12:00:00Z' },
      { type: 'InterviewerExperience', id: 'interviewer-pending', interviewDate: '2026-11-15', createdAt: '2026-11-15T13:00:00Z' },
    ])
    vi.spyOn(api, 'pendingProfiles').mockResolvedValue([])
    vi.spyOn(api, 'contentReports').mockResolvedValue([])
    const moderate = vi.spyOn(api, 'moderate').mockResolvedValue(undefined)
    wrap(<AdminDashboard />, adminAccount)
    const decisions = await screen.findAllByRole('button', { name: 'تأیید' })
    fireEvent.click(decisions[0])
    expect(moderate).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText('دلیل تصمیم (برای تأیید یا رد اجباری است)'), { target: { value: 'Reviewed and approved.' } })
    fireEvent.click(decisions[0])
    await waitFor(() => expect(moderate).toHaveBeenCalledWith('candidate', 'candidate-pending', 'publish', 'Reviewed and approved.'))

    fireEvent.change(screen.getByLabelText('دلیل تصمیم (برای تأیید یا رد اجباری است)'), { target: { value: 'Missing stage details.' } })
    fireEvent.click(screen.getAllByRole('button', { name: 'رد' })[1])
    await waitFor(() => expect(moderate).toHaveBeenCalledWith('interviewer', 'interviewer-pending', 'reject', 'Missing stage details.'))
  })
})
