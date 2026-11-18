import React, { lazy, Suspense } from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'sonner'
import { AuthProvider } from './auth'
import { Layout } from './components/layout'
import { RequireAuth } from './components/require-auth'
import { Status } from './components/ui'
import { ErrorBoundary } from './components/error-boundary'
import '@fontsource/vazirmatn/400.css'
import '@fontsource/vazirmatn/700.css'
import '@fontsource/vazirmatn/800.css'
import './styles.css'

const queryClient = new QueryClient({ defaultOptions: { queries: { staleTime: 30_000, retry: 1, refetchOnWindowFocus: false } } })
const Home = lazy(() => import('./pages/public').then(m => ({ default: m.Home })))
const Experiences = lazy(() => import('./pages/public').then(m => ({ default: m.Experiences })))
const ExperienceDetail = lazy(() => import('./pages/public').then(m => ({ default: m.ExperienceDetail })))
const Companies = lazy(() => import('./pages/public').then(m => ({ default: m.Companies })))
const CompanyDetail = lazy(() => import('./pages/public').then(m => ({ default: m.CompanyDetail })))
const Questions = lazy(() => import('./pages/public').then(m => ({ default: m.Questions })))
const QuestionDetail = lazy(() => import('./pages/public').then(m => ({ default: m.QuestionDetail })))
const Reports = lazy(() => import('./pages/public').then(m => ({ default: m.Reports })))
const NotFound = lazy(() => import('./pages/public').then(m => ({ default: m.NotFound })))
const PrivacyDraft = lazy(() => import('./pages/public').then(m => ({ default: m.PrivacyDraft })))
const TermsDraft = lazy(() => import('./pages/public').then(m => ({ default: m.TermsDraft })))
const Login = lazy(() => import('./pages/auth').then(m => ({ default: m.Login })))
const Register = lazy(() => import('./pages/auth').then(m => ({ default: m.Register })))
const Dashboard = lazy(() => import('./pages/account').then(m => ({ default: m.Dashboard })))
const SubmissionReceipt = lazy(() => import('./pages/account').then(m => ({ default: m.SubmissionReceipt })))
const SubmitInterviewer = lazy(() => import('./pages/account').then(m => ({ default: m.SubmitInterviewer })))
const InterviewerProfileEditor = lazy(() => import('./pages/account').then(m => ({ default: m.InterviewerProfileEditor })))
const AdminDashboard = lazy(() => import('./pages/account').then(m => ({ default: m.AdminDashboard })))
const SubmitExperience = lazy(() => import('./pages/submit').then(m => ({ default: m.SubmitExperience })))

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode><ErrorBoundary><QueryClientProvider client={queryClient}><AuthProvider><BrowserRouter><Suspense fallback={<div className="container page-space"><Status kind="loading">در حال آماده‌سازی صفحه…</Status></div>}><Routes><Route element={<Layout />}>
    <Route index element={<Home />} />
    <Route path="experiences" element={<Experiences />} />
    <Route path="experiences/new" element={<RequireAuth><SubmitExperience /></RequireAuth>} />
    <Route path="dashboard/submission/candidate/:id/edit" element={<RequireAuth role="Candidate"><SubmitExperience /></RequireAuth>} />
    <Route path="experiences/:kind/:id" element={<ExperienceDetail />} />
    <Route path="companies" element={<Companies />} />
    <Route path="companies/:id" element={<CompanyDetail />} />
    <Route path="questions" element={<Questions />} />
    <Route path="questions/:id" element={<QuestionDetail />} />
    <Route path="reports" element={<Reports />} />
    <Route path="login" element={<Login />} />
    <Route path="register" element={<Register />} />
    <Route path="dashboard" element={<RequireAuth><Dashboard /></RequireAuth>} />
    <Route path="dashboard/submission/:kind/:id" element={<RequireAuth><SubmissionReceipt /></RequireAuth>} />
    <Route path="interviewer/new" element={<RequireAuth role="Interviewer"><SubmitInterviewer /></RequireAuth>} />
    <Route path="interviewer/:id/edit" element={<RequireAuth role="Interviewer"><SubmitInterviewer /></RequireAuth>} />
    <Route path="interviewer/profile" element={<RequireAuth role="Interviewer"><InterviewerProfileEditor /></RequireAuth>} />
    <Route path="admin" element={<RequireAuth role="Admin"><AdminDashboard /></RequireAuth>} />
    <Route path="privacy" element={<PrivacyDraft />} />
    <Route path="terms" element={<TermsDraft />} />
    <Route path="*" element={<NotFound />} />
  </Route></Routes></Suspense></BrowserRouter><Toaster position="bottom-center" richColors dir="rtl" /></AuthProvider></QueryClientProvider></ErrorBoundary></React.StrictMode>,
)
