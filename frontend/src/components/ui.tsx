import * as React from 'react'
import { Link } from 'react-router-dom'
import { cn } from '../lib/utils'

// Small shadcn-style primitives keep focus and spacing consistent across pages.
export function Button({ className, variant = 'primary', ...props }: React.ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'outline' | 'ghost' | 'danger' }) {
  return <button className={cn('button', `button-${variant}`, className)} {...props} />
}
export function ButtonLink({ to, children, variant = 'primary', className }: { to: string; children: React.ReactNode; variant?: 'primary' | 'outline' | 'ghost'; className?: string }) {
  return <Link to={to} className={cn('button', `button-${variant}`, className)}>{children}</Link>
}
export function Card({ children, className }: { children: React.ReactNode; className?: string }) {
  return <div className={cn('card', className)}>{children}</div>
}
export const Input = React.forwardRef<HTMLInputElement, React.InputHTMLAttributes<HTMLInputElement>>(function Input(props, ref) {
  return <input ref={ref} className={cn('input', props.className)} {...props} />
})
export const Textarea = React.forwardRef<HTMLTextAreaElement, React.TextareaHTMLAttributes<HTMLTextAreaElement>>(function Textarea(props, ref) {
  return <textarea ref={ref} className={cn('input textarea', props.className)} {...props} />
})
export function Field({ label, error, hint, children }: { label: string; error?: string; hint?: string; children: React.ReactNode }) {
  return <label className="field"><span className="field-label">{label}</span>{children}{hint && <small className="hint">{hint}</small>}{error && <small className="field-error" role="alert">{error}</small>}</label>
}
export function Status({ kind, children, retry }: { kind: 'loading' | 'error' | 'empty'; children: React.ReactNode; retry?: () => void }) {
  return <div className={`status status-${kind}`} role={kind === 'error' ? 'alert' : 'status'}><div>{children}</div>{retry && <Button variant="outline" onClick={retry}>تلاش دوباره</Button>}</div>
}
