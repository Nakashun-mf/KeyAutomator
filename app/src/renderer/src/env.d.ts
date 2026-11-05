import type { KeyAutomatorApi } from '../../shared/api'

declare global {
  interface Window {
    api: KeyAutomatorApi
  }
}
