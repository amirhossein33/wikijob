import type { Level, ModerationStatus, Outcome, QuestionType, StageType } from './types'

export const levelLabel: Record<Level, string> = { Unknown: 'نامشخص', Intern: 'کارآموز', Junior: 'جونیور', Mid: 'میدلول', Senior: 'سینیور', Lead: 'لید' }
export const typeLabel: Record<QuestionType, string> = { Algorithm: 'الگوریتم', LiveCode: 'لایوکد', Conceptual: 'مفهومی', SystemDesign: 'طراحی سیستم' }
export const stageLabel: Record<StageType, string> = { HR: 'منابع انسانی', Technical: 'فنی', LiveCoding: 'کدنویسی زنده', SystemDesign: 'طراحی سیستم', Other: 'سایر' }
export const outcomeLabel: Record<Outcome, string> = { Unknown: 'نامشخص', Passed: 'قبول', Rejected: 'رد شده' }
export const moderationStatusLabel: Record<ModerationStatus, string> = {
  Pending: 'در انتظار بررسی', Published: 'منتشرشده', Hidden: 'پنهان', Rejected: 'ردشده', Deleted: 'حذف‌شده',
}
export const formatDate = (date: string) => new Intl.DateTimeFormat('fa-IR', { dateStyle: 'medium' }).format(new Date(`${date}T12:00:00`))
export const digits = (value: number) => new Intl.NumberFormat('fa-IR').format(value)
