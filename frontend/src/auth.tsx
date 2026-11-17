import { useEffect, useState, type ReactNode } from 'react'
import { api, tokenStore } from './lib/api'
import type { Account, TokenResponse } from './lib/types'
import { AuthContext } from './auth-context'

// Bearer tokens remain in memory; a page reload requires login until the API supports secure refresh cookies.
export function AuthProvider({ children }: { children: ReactNode }) {
  const [account, setAccount] = useState<Account | null>(null)
  const [loading, setLoading] = useState(false)
  useEffect(() => { tokenStore.set(null) }, [])
  async function refresh() { setAccount(await api.me()) }
  async function acceptToken(result: TokenResponse) {
    tokenStore.set(result.accessToken)
    setLoading(true)
    try { await refresh() } catch (error) { tokenStore.set(null); throw error } finally { setLoading(false) }
  }
  function logout() { tokenStore.set(null); setAccount(null) }
  return <AuthContext.Provider value={{ account, loading, hasRole: role => !!account?.roles.includes(role), acceptToken, refresh, logout }}>{children}</AuthContext.Provider>
}
