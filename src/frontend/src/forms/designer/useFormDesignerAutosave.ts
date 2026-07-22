import { useCallback, useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api, ApiError } from '../../api/client'
import type { FormSchemaDocument } from './schemaTypes'

export type AutosaveStatus = 'idle' | 'dirty' | 'saving' | 'saved' | 'error' | 'conflict'

type UseFormDesignerAutosaveArgs = {
  formId: string | undefined
  versionId: string | undefined
  schema: FormSchemaDocument | undefined
  rowVersion: string
  onRowVersionChange: (rowVersion: string) => void
  enabled: boolean
}

export function useFormDesignerAutosave({
  formId,
  versionId,
  schema,
  rowVersion,
  onRowVersionChange,
  enabled,
}: UseFormDesignerAutosaveArgs) {
  const qc = useQueryClient()
  const [status, setStatus] = useState<AutosaveStatus>('idle')
  const [error, setError] = useState<string | null>(null)
  const lastSavedJson = useRef('')
  const abortRef = useRef<AbortController | null>(null)
  const debounceRef = useRef<number | null>(null)
  const inFlightRef = useRef<Promise<string> | null>(null)

  const markSavedBaseline = useCallback((json: string) => {
    lastSavedJson.current = json
    setStatus('idle')
  }, [])

  const saveNow = useCallback(
    async (json: string, currentRowVersion: string): Promise<string> => {
      if (!formId || !versionId) {
        return currentRowVersion
      }

      abortRef.current?.abort()
      const controller = new AbortController()
      abortRef.current = controller
      setStatus('saving')
      setError(null)

      const promise = api.forms
        .autosaveSchema(formId, versionId, { schemaJson: json, rowVersion: currentRowVersion })
        .then((v) => {
          if (controller.signal.aborted) {
            return currentRowVersion
          }

          onRowVersionChange(v.rowVersion)
          lastSavedJson.current = json
          setStatus('saved')
          void qc.invalidateQueries({ queryKey: ['form-version', formId, versionId] })
          return v.rowVersion
        })
        .catch((err: ApiError) => {
          if (controller.signal.aborted) {
            return currentRowVersion
          }

          if (err.status === 409) {
            setStatus('conflict')
            setError('تعارض تحديث. أعد تحميل أحدث نسخة.')
          } else {
            setStatus('error')
            setError(err.message || 'فشل الحفظ التلقائي.')
          }
          throw err
        })
        .finally(() => {
          if (inFlightRef.current === promise) {
            inFlightRef.current = null
          }
        })

      inFlightRef.current = promise
      return promise
    },
    [formId, versionId, onRowVersionChange, qc],
  )

  const flush = useCallback(async (): Promise<string> => {
    if (!schema || !formId || !versionId) {
      return rowVersion
    }

    if (debounceRef.current) {
      window.clearTimeout(debounceRef.current)
      debounceRef.current = null
    }

    const json = JSON.stringify(schema)
    if (json === lastSavedJson.current) {
      if (inFlightRef.current) {
        return inFlightRef.current
      }

      return rowVersion
    }

    let currentRowVersion = rowVersion
    if (inFlightRef.current) {
      try {
        currentRowVersion = await inFlightRef.current
      } catch {
        // Continue with latest known rowVersion and attempt a fresh save.
      }
    }

    if (json === lastSavedJson.current) {
      return currentRowVersion
    }

    return saveNow(json, currentRowVersion)
  }, [formId, versionId, rowVersion, saveNow, schema])

  useEffect(() => {
    if (!enabled || !schema || !formId || !versionId || !rowVersion) {
      return
    }

    const json = JSON.stringify(schema)
    if (json === lastSavedJson.current) {
      return
    }

    setStatus('dirty')
    if (debounceRef.current) {
      window.clearTimeout(debounceRef.current)
    }

    debounceRef.current = window.setTimeout(() => {
      void saveNow(json, rowVersion).catch(() => undefined)
    }, 800)

    return () => {
      if (debounceRef.current) {
        window.clearTimeout(debounceRef.current)
      }
    }
  }, [enabled, formId, versionId, rowVersion, saveNow, schema])

  useEffect(
    () => () => {
      abortRef.current?.abort()
      if (debounceRef.current) {
        window.clearTimeout(debounceRef.current)
      }
    },
    [],
  )

  return { status, error, flush, markSavedBaseline }
}
