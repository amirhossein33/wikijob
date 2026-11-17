import { createContext, useContext } from 'react'
import type { Account, Role, TokenResponse } from './lib/types'

export interface AuthContextValue {
  account: Account | null; loading: boolean; hasRole: (role: Role) => boolean;
  acceptToken: (result: TokenResponse) => Promise<void>; refresh: () => Promise<void>; logout: () => void
}
export const AuthContext = createContext<AuthContextValue | null>(null)
export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) throw new Error('AuthProvider is missing')
  return value
}
