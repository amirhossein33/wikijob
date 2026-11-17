// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthContext, type AuthContextValue } from '../auth-context'
import { Login } from './auth'
import { SubmitExperience } from './submit'

const account: AuthContextValue = {
  account: { id: 'candidate-id', userName: 'sample', email: 'sample@example.test', roles: ['Candidate'] },
  loading: false, hasRole: role => role === 'Candidate',
  acceptToken: vi.fn(), refresh: vi.fn(), logout: vi.fn(),
}
function wrap(node: React.ReactNode, auth: AuthContextValue) {
  return render(<QueryClientProvider client={new QueryClient()}><AuthContext.Provider value={auth}><MemoryRouter>{node}</MemoryRouter></AuthContext.Provider></QueryClientProvider>)
}
afterEach(() => { cleanup(); vi.unstubAllGlobals() })

describe('form boundaries', () => {
  it('requires credentials before calling login', async () => {
    wrap(<Login />, { ...account, account: null, hasRole: () => false })
    fireEvent.click(screen.getByRole('button', { name: 'ورود به حساب' }))
    expect(await screen.findByText('نام کاربری یا ایمیل را وارد کنید.')).toBeTruthy()
    expect(screen.getByText('رمز عبور را وارد کنید.')).toBeTruthy()
  })
  it('does not advance past an interview stage without a description', async () => {
    vi.stubGlobal('scrollTo', vi.fn())
    wrap(<SubmitExperience />, account)
    fireEvent.click(screen.getByRole('button', { name: /مرحلهٔ بعد/ }))
    await waitFor(() => expect(screen.getByRole('heading', { name: 'مراحل مصاحبه' })).toBeTruthy())
    fireEvent.click(screen.getByRole('button', { name: /مرحلهٔ بعد/ }))
    expect(screen.getByRole('alert').textContent).toContain('تاریخ معتبر و توضیح')
  })
})
