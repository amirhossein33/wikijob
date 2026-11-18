import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useNavigate, useParams } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Plus, Trash2 } from 'lucide-react'
import { toast } from 'sonner'
import { z } from 'zod'
import { useAuth } from '../auth-context'
import { api, ApiError } from '../lib/api'
import { levelLabel, outcomeLabel, stageLabel, typeLabel } from '../lib/labels'
import type { CandidateInput, Level, Outcome, PrivateCandidateDetail, QuestionType, StageType } from '../lib/types'
import { Button, ButtonLink, Card, Field, Input, Status, Textarea } from '../components/ui'

type DraftQuestion = { text: string; type: QuestionType; perceivedDifficulty: number | undefined }
type DraftStage = { type: StageType; date: string; description: string; questions: DraftQuestion[] }
type Draft = Omit<CandidateInput, 'stages'> & { stages: DraftStage[] }
const newQuestion = (): DraftQuestion => ({ text: '', type: 'Conceptual', perceivedDifficulty: undefined })
const newStage = (): DraftStage => ({ type: 'Technical', date: new Date().toISOString().slice(0, 10), description: '', questions: [newQuestion()] })
const initialDraft: Draft = { companyName: '', isAuthorAnonymous: true, level: 'Unknown', outcome: 'Unknown', description: '', jobAdText: '', jobAdUrl: '', ratingReason: '', stages: [newStage()] }
const urlSchema = z.union([z.literal(''), z.url('لینک آگهی معتبر نیست.')])
const steps = ['موقعیت و شرکت', 'مراحل مصاحبه', 'سؤال‌ها', 'نتیجه و ارزیابی', 'بازبینی']

// Private details map back to the editable form; ownership and moderation stay server-controlled.
function toDraft(item: PrivateCandidateDetail): Draft {
  return {
    companyName: item.companyName || '', isAuthorAnonymous: item.isAuthorAnonymous, level: item.level,
    outcome: item.outcome, description: item.description || '', jobAdText: item.jobAdText || '',
    jobAdUrl: item.jobAdUrl || '', interviewerConductRating: item.interviewerConductRating || undefined,
    jobAdAlignmentRating: item.jobAdAlignmentRating || undefined, ratingReason: item.ratingReason || '',
    stages: item.stages.map(stage => ({ type: stage.type, date: stage.date, description: stage.description,
      questions: stage.questions.map(question => ({ text: question.text, type: question.type, perceivedDifficulty: question.perceivedDifficulty || undefined })) })),
  }
}

export function SubmitExperience() {
  const { hasRole } = useAuth()
  const { id } = useParams()
  const existing = useQuery({ queryKey: ['private-submission', 'candidate', id], queryFn: () => api.privateCandidate(id!), enabled: !!id, retry: false })
  if (!hasRole('Candidate') && !hasRole('Admin')) return <div className="container page-space center-state"><h1>نقش مصاحبه‌شونده لازم است</h1><p>می‌توانی همین نقش را به حساب فعلی‌ات اضافه کنی.</p><ButtonLink to="/dashboard">رفتن به حساب</ButtonLink></div>
  if (id && existing.isPending) return <div className="container page-space"><Status kind="loading">در حال دریافت تجربه…</Status></div>
  if (id && existing.isError) return <div className="container page-space"><Status kind="error" retry={() => existing.refetch()}>این تجربه برای حساب شما پیدا نشد.</Status></div>
  if (id && existing.data?.status === 'Deleted') return <div className="container page-space"><Status kind="empty">این تجربه حذف شده و قابل ویرایش نیست.</Status><ButtonLink to="/dashboard">بازگشت به حساب</ButtonLink></div>
  return <CandidateExperienceEditor key={id || 'new'} id={id} initialDraft={existing.data ? toDraft(existing.data) : initialDraft} />
}

function CandidateExperienceEditor({ id, initialDraft: sourceDraft }: { id?: string; initialDraft: Draft }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<Draft>(sourceDraft)
  const [step, setStep] = useState(0)
  const [error, setError] = useState('')
  const mutation = useMutation({ mutationFn: (body: CandidateInput) => id ? api.updateCandidate(id, body) : api.submitCandidate(body), onSuccess: async result => { await queryClient.invalidateQueries({ queryKey: ['experiences'] }); await queryClient.invalidateQueries({ queryKey: ['my-candidate-experiences'] }); await queryClient.invalidateQueries({ queryKey: ['private-submission', 'candidate', id] }); toast.success(id ? 'ویرایش ثبت شد و دوباره در انتظار بررسی مدیر است.' : 'تجربه ثبت شد و در انتظار تأیید مدیر است.'); navigate(`/dashboard/submission/candidate/${result.id}`) } })
  function patch<K extends keyof Draft>(key: K, value: Draft[K]) { setDraft(old => ({ ...old, [key]: value })) }
  function patchStage(index: number, value: Partial<DraftStage>) { setDraft(old => ({ ...old, stages: old.stages.map((s, i) => i === index ? { ...s, ...value } : s) })) }
  function patchQuestion(stageIndex: number, questionIndex: number, value: Partial<DraftQuestion>) { setDraft(old => ({ ...old, stages: old.stages.map((s, i) => i === stageIndex ? { ...s, questions: s.questions.map((q, j) => j === questionIndex ? { ...q, ...value } : q) } : s) })) }
  function validate(current: number) {
    if (current === 0 && !urlSchema.safeParse(draft.jobAdUrl || '').success) return 'لینک آگهی معتبر نیست.'
    if (current === 1 && draft.stages.some(s => !s.date || s.date > new Date().toISOString().slice(0, 10) || !s.description.trim())) return 'برای هر مرحله، تاریخ معتبر و توضیح لازم است.'
    if (current === 2 && draft.stages.some(s => s.questions.some(q => !q.text.trim() || q.text.length > 2000))) return 'متن همهٔ سؤال‌ها را وارد کنید.'
    if (current === 3 && draft.ratingReason && draft.ratingReason.length > 2000) return 'دلیل امتیاز طولانی است.'
    return ''
  }
  function next() { const issue = validate(step); if (issue) setError(issue); else { setError(''); setStep(step + 1); window.scrollTo(0, 0) } }
  function submit() {
    for (let i = 0; i < 4; i++) { const issue = validate(i); if (issue) { setStep(i); setError(issue); return } }
    const payload: CandidateInput = { ...draft, companyName: draft.companyName?.trim() || undefined, description: draft.description?.trim() || undefined, jobAdText: draft.jobAdText?.trim() || undefined, jobAdUrl: draft.jobAdUrl?.trim() || undefined, ratingReason: draft.ratingReason?.trim() || undefined, stages: draft.stages.map(s => ({ ...s, description: s.description.trim(), questions: s.questions.map(q => ({ ...q, text: q.text.trim() })) })) }
    mutation.mutate(payload, { onError: err => setError(err instanceof ApiError ? err.message : 'ثبت تجربه ممکن نشد.') })
  }
  return <div className="container page-space form-page"><div className="page-heading"><div><span className="eyebrow">سهم تو در جامعه</span><h1>{id ? 'ویرایش تجربهٔ مصاحبه' : 'ثبت تجربهٔ مصاحبه'}</h1><p>در چند قدم ساده، آنچه را تجربه کرده‌ای ثبت کن.</p>{id && <p className="hint">پس از ویرایش، تجربه برای بررسی دوبارهٔ مدیر به صف برمی‌گردد.</p>}</div></div><div className="wizard-steps" aria-label="مراحل ثبت تجربه">{steps.map((name, index) => <div key={name} className={index === step ? 'active' : index < step ? 'done' : ''}><span>{index + 1}</span><small>{name}</small></div>)}</div><Card className="wizard-card">
    {step === 0 && <div className="form-stack"><h2>موقعیت و شرکت</h2><p>اگر شرکت را ذکر نکنی، فقط محتوای مصاحبه منتشر می‌شود.</p><Field label="نام شرکت (اختیاری)"><Input value={draft.companyName || ''} maxLength={200} onChange={e => patch('companyName', e.target.value)} placeholder="نام شرکتی که مصاحبه داشتی" /></Field><Field label="سطح مصاحبه"><select className="input" value={draft.level} onChange={e => patch('level', e.target.value as Level)}>{(['Unknown', 'Intern', 'Junior', 'Mid', 'Senior', 'Lead'] as Level[]).map(level => <option key={level} value={level}>{levelLabel[level]}</option>)}</select></Field><Field label="متن آگهی شغلی (اختیاری)"><Textarea rows={4} maxLength={10000} value={draft.jobAdText || ''} onChange={e => patch('jobAdText', e.target.value)} placeholder="اگر آگهی را داری، اینجا قرار بده" /></Field><Field label="لینک آگهی (اختیاری)"><Input dir="ltr" type="url" maxLength={2048} value={draft.jobAdUrl || ''} onChange={e => patch('jobAdUrl', e.target.value)} placeholder="https://…" /></Field><label className="check-row"><input type="checkbox" checked={draft.isAuthorAnonymous} onChange={e => patch('isAuthorAnonymous', e.target.checked)} /><span>نام کاربری من در نمایش عمومی پنهان باشد.</span></label></div>}
    {step === 1 && <div className="form-stack"><h2>مراحل مصاحبه</h2><p>برای هر مرحله، تاریخ و شرح کوتاهی بنویس.</p>{draft.stages.map((stage, index) => <div className="stage-editor" key={index}><div className="editor-heading"><h3>مرحلهٔ {index + 1}</h3>{draft.stages.length > 1 && <Button variant="ghost" aria-label={`حذف مرحلهٔ ${index + 1}`} onClick={() => patch('stages', draft.stages.filter((_, i) => i !== index))}><Trash2 size={16} /></Button>}</div><div className="two-columns"><Field label="نوع مرحله"><select className="input" value={stage.type} onChange={e => patchStage(index, { type: e.target.value as StageType })}>{(['HR', 'Technical', 'LiveCoding', 'SystemDesign', 'Other'] as StageType[]).map(type => <option key={type} value={type}>{stageLabel[type]}</option>)}</select></Field><Field label="تاریخ"><Input type="date" max={new Date().toISOString().slice(0, 10)} value={stage.date} onChange={e => patchStage(index, { date: e.target.value })} /></Field></div><Field label="چه اتفاقی افتاد؟"><Textarea rows={3} maxLength={10000} value={stage.description} onChange={e => patchStage(index, { description: e.target.value })} /></Field></div>)}<Button variant="outline" disabled={draft.stages.length >= 20} onClick={() => patch('stages', [...draft.stages, newStage()])}><Plus size={17} /> افزودن مرحله</Button></div>}
    {step === 2 && <div className="form-stack"><h2>سؤال‌های هر مرحله</h2><p>سؤال‌ها را همان‌طور که به خاطر داری بنویس. سؤال مشابه در backend به نمونهٔ موجود وصل می‌شود.</p>{draft.stages.map((stage, stageIndex) => <div className="stage-editor" key={stageIndex}><h3>{stageLabel[stage.type]} · مرحلهٔ {stageIndex + 1}</h3>{stage.questions.map((question, questionIndex) => <div className="question-editor" key={questionIndex}><Field label={`سؤال ${questionIndex + 1}`}><Textarea rows={2} maxLength={2000} value={question.text} onChange={e => patchQuestion(stageIndex, questionIndex, { text: e.target.value })} /></Field><div className="two-columns"><Field label="نوع سؤال"><select className="input" value={question.type} onChange={e => patchQuestion(stageIndex, questionIndex, { type: e.target.value as QuestionType })}>{(['Algorithm', 'LiveCode', 'Conceptual', 'SystemDesign'] as QuestionType[]).map(type => <option key={type} value={type}>{typeLabel[type]}</option>)}</select></Field><Field label="سختی از نگاه شما"><select className="input" value={question.perceivedDifficulty || ''} onChange={e => patchQuestion(stageIndex, questionIndex, { perceivedDifficulty: e.target.value ? Number(e.target.value) : undefined })}><option value="">بدون امتیاز</option>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n} از ۵</option>)}</select></Field></div>{stage.questions.length > 1 && <Button variant="ghost" onClick={() => patchStage(stageIndex, { questions: stage.questions.filter((_, i) => i !== questionIndex) })}><Trash2 size={15} /> حذف سؤال</Button>}</div>)}<Button variant="outline" disabled={stage.questions.length >= 50} onClick={() => patchStage(stageIndex, { questions: [...stage.questions, newQuestion()] })}><Plus size={16} /> افزودن سؤال</Button></div>)}</div>}
    {step === 3 && <div className="form-stack"><h2>نتیجه و ارزیابی</h2><Field label="نتیجهٔ مصاحبه"><select className="input" value={draft.outcome} onChange={e => patch('outcome', e.target.value as Outcome)}>{(['Unknown', 'Passed', 'Rejected'] as Outcome[]).map(value => <option key={value} value={value}>{outcomeLabel[value]}</option>)}</select></Field><Field label="رفتار مصاحبه‌کننده"><select className="input" value={draft.interviewerConductRating || ''} onChange={e => patch('interviewerConductRating', e.target.value ? Number(e.target.value) : undefined)}><option value="">بدون امتیاز</option>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n} از ۵</option>)}</select></Field><Field label="تناسب سؤال‌ها با آگهی"><select className="input" value={draft.jobAdAlignmentRating || ''} onChange={e => patch('jobAdAlignmentRating', e.target.value ? Number(e.target.value) : undefined)}><option value="">بدون امتیاز</option>{[1, 2, 3, 4, 5].map(n => <option key={n} value={n}>{n} از ۵</option>)}</select></Field><Field label="دلیل امتیازها (اختیاری)"><Textarea rows={3} maxLength={2000} value={draft.ratingReason || ''} onChange={e => patch('ratingReason', e.target.value)} /></Field><Field label="توصیه یا توضیح تکمیلی (اختیاری)"><Textarea rows={4} maxLength={10000} value={draft.description || ''} onChange={e => patch('description', e.target.value)} /></Field></div>}
    {step === 4 && <div className="form-stack"><h2>بازبینی و ثبت</h2><p>تجربه پس از ثبت، تا زمان بررسی مدیر عمومی نمی‌شود.</p><div className="review-list"><div><span>شرکت</span><strong>{draft.companyName || 'ثبت نشده'}</strong></div><div><span>سطح</span><strong>{levelLabel[draft.level]}</strong></div><div><span>نمایش نام</span><strong>{draft.isAuthorAnonymous ? 'ناشناس' : 'با نام کاربری'}</strong></div><div><span>تعداد مراحل</span><strong>{draft.stages.length}</strong></div><div><span>تعداد سؤال‌ها</span><strong>{draft.stages.reduce((sum, s) => sum + s.questions.length, 0)}</strong></div><div><span>نتیجه</span><strong>{outcomeLabel[draft.outcome]}</strong></div></div></div>}
    {error && <div className="inline-error" role="alert">{error}</div>}<div className="wizard-actions">{step > 0 && <Button variant="outline" onClick={() => { setError(''); setStep(step - 1) }}><ArrowRight size={16} /> مرحلهٔ قبل</Button>}{step < 4 ? <Button onClick={next}>مرحلهٔ بعد <ArrowLeft size={16} /></Button> : <Button disabled={mutation.isPending} onClick={submit}>{mutation.isPending ? 'در حال ذخیره…' : id ? 'ذخیرهٔ ویرایش برای بررسی' : 'ثبت نهایی تجربه'}</Button>}</div>
  </Card></div>
}
