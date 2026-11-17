import type { ReactNode } from 'react'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../auth-context'

// Route guards improve navigation; backend authorization remains authoritative.
export function RequireAuth({ children, role }: { children: ReactNode; role?: 'Candidate' | 'Interviewer' | 'Admin' }) {
  const { account } = useAuth()
  const location = useLocation()
  if (!account) return <Navigate to="/login" state={{ from: location.pathname }} replace />
  if (role && !account.roles.includes(role)) return <div className="container page-space center-state"><h1>دسترسی ندارید</h1><p>این بخش به نقش {role === 'Candidate' ? 'مصاحبه‌شونده' : role === 'Interviewer' ? 'مصاحبه‌کننده' : 'مدیر'} نیاز دارد.</p><Link to="/dashboard" className="button button-outline">بازگشت به حساب</Link></div>
  return <>{children}</>
}
