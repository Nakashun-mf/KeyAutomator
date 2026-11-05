import type { ReactElement } from 'react'
import { useEffect, useRef } from 'react'
import { useModel } from '../store'

/** 確認ダイアログ。Enter で既定ボタン、Esc でキャンセル。 */
export function Modal(): ReactElement | null {
  const { modal } = useModel()
  const defaultRef = useRef<HTMLButtonElement>(null)

  useEffect(() => {
    if (!modal) return
    defaultRef.current?.focus()
    const onKey = (e: KeyboardEvent): void => {
      if (e.key === 'Escape') {
        e.preventDefault()
        e.stopPropagation()
        modal.resolve(modal.cancelId)
      }
    }
    window.addEventListener('keydown', onKey, true)
    return () => window.removeEventListener('keydown', onKey, true)
  }, [modal])

  if (!modal) return null
  return (
    <div className="overlay" role="presentation" onMouseDown={(e) => e.target === e.currentTarget && modal.resolve(modal.cancelId)}>
      <div className="dialog" role="alertdialog" aria-modal="true" aria-labelledby="dlg-title" aria-describedby="dlg-body">
        <h2 id="dlg-title" className="dialog-title">{modal.title}</h2>
        <p id="dlg-body" className="dialog-body">{modal.body}</p>
        <div className="dialog-actions">
          {modal.buttons.map((b) => (
            <button
              key={b.id}
              ref={b.id === modal.defaultId ? defaultRef : undefined}
              className={`btn ${b.kind === 'primary' ? 'btn-primary' : b.kind === 'danger' ? 'btn-danger' : ''}`}
              onClick={() => modal.resolve(b.id)}
            >
              {b.label}
            </button>
          ))}
        </div>
      </div>
    </div>
  )
}
