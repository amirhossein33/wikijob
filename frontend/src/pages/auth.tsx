import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { toast } from 'sonner'
import { useAuth } from '../auth-context'
import { api, ApiError } from '../lib/api'
import { Button, Card, Field, Input } from '../components/ui'

const loginSchema = z.object({ userNameOrEmail: z.string().min(1, 'نام کاربری یا ایمیل را وارد کنید.'), password: z.string().min(1, 'رمز عبور را وارد کنید.') })
const registerSchema = z.object({
  userName: z.string().min(3, 'نام کاربری باید دست‌کم ۳ نویسه باشد.').max(100),
  email: z.email('ایمیل معتبر وارد کنید.'),
  password: z.string().min(8, 'رمز عبور باید دست‌کم ۸ نویسه باشد.'),
  role: z.enum(['Candidate', 'Interviewer']),
})
type LoginFields = z.infer<typeof loginSchema>
type RegisterFields = z.infer<typeof registerSchema>

export function Login() {
  const { account, acceptToken } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [serverError, setServerError] = useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<LoginFields>({ resolver: zodResolver(loginSchema) })
  if (account) return <Navigate to="/dashboard" replace />
  async function submit(values: LoginFields) {
    setServerError('')
    try { await acceptToken(await api.login(values)); toast.success('با موفقیت وارد شدید.'); navigate((location.state as { from?: string } | null)?.from || '/dashboard', { replace: true }) }
    catch (error) { setServerError(error instanceof ApiError ? error.message : 'ورود ممکن نشد. دوباره تلاش کنید.') }
  }
  return <div className="auth-page"><Card className="auth-card"><span className="eyebrow">خوش برگشتی</span><h1>ورود به حساب</h1><p>برای ثبت تجربه یا دیدگاه خود وارد شوید.</p><form onSubmit={handleSubmit(submit)} className="form-stack"><Field label="نام کاربری یا ایمیل" error={errors.userNameOrEmail?.message}><Input autoComplete="username" {...register('userNameOrEmail')} /></Field><Field label="رمز عبور" error={errors.password?.message}><Input type="password" autoComplete="current-password" {...register('password')} /></Field>{serverError && <div className="inline-error" role="alert">{serverError}</div>}<Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'در حال ورود…' : 'ورود به حساب'}</Button></form><div className="auth-switch">حساب نداری؟ <Link to="/register">ثبت‌نام کن</Link></div></Card></div>
}
export function Register() {
  const { account, acceptToken } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<RegisterFields>({ resolver: zodResolver(registerSchema), defaultValues: { role: 'Candidate' } })
  if (account) return <Navigate to="/dashboard" replace />
  async function submit(values: RegisterFields) {
    setServerError('')
    try { await acceptToken(await api.register(values)); toast.success('حساب شما ساخته شد.'); navigate('/dashboard', { replace: true }) }
    catch (error) { setServerError(error instanceof ApiError ? error.message : 'ثبت‌نام ممکن نشد.') }
  }
  return <div className="auth-page"><Card className="auth-card"><span className="eyebrow">به جامعه بپیوند</span><h1>ساخت حساب</h1><p>برای ثبت تجربه، داشتن حساب لازم است؛ هنگام انتشار می‌توانی ناشناس بمانی.</p><form onSubmit={handleSubmit(submit)} className="form-stack"><Field label="نام کاربری" error={errors.userName?.message}><Input autoComplete="username" {...register('userName')} /></Field><Field label="ایمیل" error={errors.email?.message}><Input type="email" autoComplete="email" dir="ltr" {...register('email')} /></Field><Field label="رمز عبور" error={errors.password?.message}><Input type="password" autoComplete="new-password" {...register('password')} /></Field><Field label="نوع مشارکت" error={errors.role?.message}><select className="input" {...register('role')}><option value="Candidate">مصاحبه‌شونده</option><option value="Interviewer">مصاحبه‌کننده</option></select></Field>{serverError && <div className="inline-error" role="alert">{serverError}</div>}<Button type="submit" disabled={isSubmitting}>{isSubmitting ? 'در حال ساخت حساب…' : 'ثبت‌نام'}</Button></form><div className="auth-switch">قبلاً حساب ساخته‌ای؟ <Link to="/login">وارد شو</Link></div></Card></div>
}
