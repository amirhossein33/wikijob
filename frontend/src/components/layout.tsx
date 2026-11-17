import { useState } from 'react'
import { NavLink, Outlet, Link } from 'react-router-dom'
import { Menu, X, ArrowLeft, LogOut } from 'lucide-react'
import { useAuth } from '../auth-context'
import { ButtonLink } from './ui'

const nav = [
  { to: '/experiences', label: 'تجربه‌ها' },
  { to: '/questions', label: 'سؤال‌ها' },
  { to: '/companies', label: 'شرکت‌ها' },
  { to: '/reports', label: 'گزارش‌ها' },
]

export function Layout() {
  const [menuOpen, setMenuOpen] = useState(false)
  const { account, logout } = useAuth()
  return <div className="app-shell">
    <a className="skip-link" href="#main">رفتن به محتوای اصلی</a>
    <header className="site-header"><div className="container header-inner">
      <Link className="brand" to="/" onClick={() => setMenuOpen(false)}><span className="brand-mark">⌘</span><span>تجربه <span className="brand-light">مصاحبه</span></span></Link>
      <nav className="desktop-nav" aria-label="ناوبری اصلی">{nav.map(item => <NavLink key={item.to} to={item.to} className={({ isActive }) => isActive ? 'nav-active' : ''}>{item.label}</NavLink>)}</nav>
      <div className="header-actions">{account ? <><Link className="account-link" to="/dashboard">{account.userName}</Link><button className="icon-button" title="خروج" aria-label="خروج" onClick={logout}><LogOut size={18} /></button></> : <Link className="account-link" to="/login">ورود / ثبت‌نام</Link>}<ButtonLink to="/experiences/new" className="header-cta">ثبت تجربه <ArrowLeft size={16} /></ButtonLink></div>
      <button className="mobile-menu-button" aria-label={menuOpen ? 'بستن منو' : 'باز کردن منو'} aria-expanded={menuOpen} onClick={() => setMenuOpen(!menuOpen)}>{menuOpen ? <X /> : <Menu />}</button>
    </div></header>
    {menuOpen && <nav className="mobile-nav" aria-label="ناوبری موبایل">{nav.map(item => <NavLink key={item.to} to={item.to} onClick={() => setMenuOpen(false)}>{item.label}</NavLink>)}<Link to={account ? '/dashboard' : '/login'} onClick={() => setMenuOpen(false)}>{account ? 'حساب من' : 'ورود / ثبت‌نام'}</Link><Link to="/experiences/new" onClick={() => setMenuOpen(false)}>ثبت تجربه</Link></nav>}
    <main id="main"><Outlet /></main>
    <footer className="site-footer"><div className="container footer-inner"><div><Link className="brand" to="/">تجربه <span className="brand-light">مصاحبه</span></Link><p>از تجربهٔ هم یاد می‌گیریم، برای مصاحبهٔ بعدی آماده‌تر می‌شویم.</p></div><div className="footer-links"><Link to="/experiences">تجربه‌ها</Link><Link to="/questions">کتابخانهٔ سؤال‌ها</Link><Link to="/privacy">حریم خصوصی</Link><Link to="/terms">شرایط استفاده</Link></div></div><div className="container footer-bottom">ساخته‌شده برای جامعهٔ توسعه‌دهندگان فارسی‌زبان</div></footer>
  </div>
}
