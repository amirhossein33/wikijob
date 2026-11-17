import { Component, type ErrorInfo, type ReactNode } from 'react'

// Render a useful fallback if a page throws outside a handled API state.
export class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false }
  static getDerivedStateFromError() { return { failed: true } }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error('Unhandled page error', error, info.componentStack) }
  render() {
    if (this.state.failed) return <main className="container page-space center-state" dir="rtl"><h1>نمایش این صفحه ممکن نشد</h1><p>صفحه را دوباره بارگذاری کنید. اگر مشکل ادامه داشت، بعداً دوباره تلاش کنید.</p><button className="button button-primary" onClick={() => window.location.reload()}>بارگذاری دوباره</button></main>
    return this.props.children
  }
}
