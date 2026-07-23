import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useEffect, useMemo, useRef, useState, useCallback } from 'react'
import { api } from '../../api/client'
import { AutosaveUiLabelsAr, FormResponseStatusLabelsAr, type AutosaveUiState } from './responseLabels'

type SchemaField = {
  key: string
  type: number
  labelAr: string
  isRequired?: boolean
  isReadOnly?: boolean
  isCalculated?: boolean
}

type SchemaDoc = {
  pages: Array<{ key: string; titleAr: string; sections: Array<{ key: string; titleAr: string; fields: SchemaField[] }> }>
}

function parseAnswers(json?: string | null): Record<string, unknown> {
  if (!json) return {}
  try { return JSON.parse(json) as Record<string, unknown> } catch { return {} }
}

export function RespondPage() {
  const { assignmentId = '' } = useParams()
  const queryClient = useQueryClient()
  const detail = useQuery({
    queryKey: ['assignment-response', assignmentId],
    queryFn: () => api.formResponses.getAssignmentResponse(assignmentId),
    enabled: Boolean(assignmentId),
    refetchOnWindowFocus: false,
    staleTime: 60_000,
  })

  const [answers, setAnswers] = useState<Record<string, unknown>>({})
  const [draftVersion, setDraftVersion] = useState(0)
  const [rowVersion, setRowVersion] = useState<string | null>(null)
  const [saveState, setSaveState] = useState<AutosaveUiState>('saved')
  const [ack, setAck] = useState(false)
  const [pageIndex, setPageIndex] = useState(0)
  const debounceRef = useRef<number | null>(null)
  const conflictRef = useRef(false)

  const initializedKey = useRef<string | null>(null)
  const pendingSaveRef = useRef(false)
  const saveStateRef = useRef(saveState)

  useEffect(() => {
    saveStateRef.current = saveState
  }, [saveState])

  useEffect(() => {
    if (!detail.data) return
    const key = `${detail.data.responseId ?? 'new'}:${detail.data.schemaHash}`
    const state = saveStateRef.current
    const dirty = state === 'dirty' || state === 'saving' || state === 'error'
      || state === 'offline' || state === 'conflict' || pendingSaveRef.current
    if (initializedKey.current === key && dirty) {
      return
    }
    // Only (re)hydrate from server when the response/schema key changes or an
    // explicit reload refreshed detail.data — never on saveState transitions.
    if (initializedKey.current === key) {
      return
    }
    initializedKey.current = key
    setAnswers(parseAnswers(detail.data.draftAnswersJson))
    setDraftVersion(detail.data.draftVersion ?? 0)
    setRowVersion(detail.data.rowVersion ?? null)
  }, [detail.data])

  const schema = useMemo(() => {
    if (!detail.data?.schemaJson) return null
    try { return JSON.parse(detail.data.schemaJson) as SchemaDoc } catch { return null }
  }, [detail.data?.schemaJson])

  const saveMutation = useMutation({
    mutationFn: (payload: { answers: Record<string, unknown>; clientMutationId: string }) =>
      api.formResponses.saveDraft(assignmentId, {
        answers: payload.answers,
        clientMutationId: payload.clientMutationId,
        expectedDraftVersion: draftVersion,
        rowVersion,
      }),
    onMutate: () => setSaveState('saving'),
    onSuccess: (result) => {
      pendingSaveRef.current = false
      setDraftVersion(result.draftVersion)
      setRowVersion(result.rowVersion)
      setSaveState('saved')
      if (result.calculatedValues) {
        setAnswers((prev) => ({ ...prev, ...result.calculatedValues }))
      }
    },
    onError: (err: unknown) => {
      const status = (err as { status?: number })?.status
      if (status === 409) {
        conflictRef.current = true
        setSaveState('conflict')
        return
      }
      setSaveState(navigator.onLine ? 'error' : 'offline')
    },
  })

  const queueSave = useCallback((next: Record<string, unknown>) => {
    if (conflictRef.current) return
    if (debounceRef.current) window.clearTimeout(debounceRef.current)
    pendingSaveRef.current = true
    setSaveState('dirty')
    debounceRef.current = window.setTimeout(() => {
      if (!navigator.onLine) {
        setSaveState('offline')
        return
      }
      saveMutation.mutate({ answers: next, clientMutationId: crypto.randomUUID() })
    }, 800)
  }, [saveMutation])

  const submitMutation = useMutation({
    mutationFn: () => api.formResponses.submit(assignmentId, {
      answers,
      clientMutationId: crypto.randomUUID(),
      expectedDraftVersion: draftVersion,
      rowVersion: rowVersion ?? '',
      acknowledged: ack,
      acknowledgementText: ack ? 'أقر بصحة البيانات المدخلة' : null,
    }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['assignment-response', assignmentId] })
      await queryClient.invalidateQueries({ queryKey: ['form-response-workspace'] })
    },
  })

  if (detail.isLoading) return <p>جاري التحميل…</p>
  if (detail.isError || !detail.data) return <p className="error" role="alert">تعذر فتح الاستحقاق.</p>

  const d = detail.data
  const pages = schema?.pages ?? []
  const currentPage = pages[pageIndex]
  const canEdit = d.allowedActions.includes('SaveDraft')

  return (
    <div className="page respond-page" dir="rtl">
      <header className="page-header">
        <div>
          <h1>{d.campaignNameAr}</h1>
          <p className="muted">{d.facilityNameAr} — {d.occurrenceKey}</p>
          <p>الحالة: {d.responseStatus == null ? 'لم يبدأ' : FormResponseStatusLabelsAr[d.responseStatus]}</p>
          <p>الموعد الفعلي: {new Date(d.effectiveDueAtUtc).toLocaleString('ar-SA')}</p>
        </div>
        <div>
          <span aria-live="polite">{AutosaveUiLabelsAr[saveState]}</span>
          <Link to="/my-form-responses">رجوع</Link>
        </div>
      </header>

      {d.visibleComments.length > 0 && (
        <section aria-label="تعليقات المراجعة">
          <h2>تعليقات الإعادة</h2>
          <ul>
            {d.visibleComments.map((c) => (
              <li key={c.id}>{c.fieldKey ? `[${c.fieldKey}] ` : ''}{c.body}</li>
            ))}
          </ul>
        </section>
      )}

      {saveState === 'conflict' && (
        <div className="error" role="alert">
          تعارض في النسخة. أعد تحميل نسخة الخادم.
          <button
            type="button"
            onClick={() => {
              initializedKey.current = null
              void detail.refetch().then((res) => {
                const data = res.data
                if (data) {
                  const key = `${data.responseId ?? 'new'}:${data.schemaHash}`
                  initializedKey.current = key
                  setAnswers(parseAnswers(data.draftAnswersJson))
                  setDraftVersion(data.draftVersion ?? 0)
                  setRowVersion(data.rowVersion ?? null)
                }
                conflictRef.current = false
                pendingSaveRef.current = false
                setSaveState('saved')
              })
            }}
          >
            تحميل نسخة الخادم
          </button>
        </div>
      )}

      <nav aria-label="صفحات النموذج" className="page-nav">
        {pages.map((p, idx) => (
          <button key={p.key} type="button" aria-current={idx === pageIndex} onClick={() => setPageIndex(idx)}>
            {p.titleAr}
          </button>
        ))}
      </nav>

      {currentPage && currentPage.sections.map((section) => (
        <section key={section.key}>
          <h2>{section.titleAr}</h2>
          {section.fields.map((field) => {
            const redacted = d.fieldRedacted?.[field.key]
            const value = answers[field.key]
            const readOnly = !canEdit || field.isReadOnly || field.isCalculated || redacted
            return (
              <label key={field.key} className="field">
                <span>{field.labelAr}{field.isRequired ? ' *' : ''}</span>
                {redacted ? (
                  <input value="***" readOnly aria-label={`${field.labelAr} محجوب`} />
                ) : field.type === 9 ? (
                  <select
                    aria-label={field.labelAr}
                    disabled={readOnly}
                    value={value === true ? 'true' : value === false ? 'false' : ''}
                    onChange={(e) => {
                      const next = { ...answers, [field.key]: e.target.value === 'true' }
                      setAnswers(next)
                      queueSave(next)
                    }}
                  >
                    <option value="">—</option>
                    <option value="true">نعم</option>
                    <option value="false">لا</option>
                  </select>
                ) : (
                  <input
                    aria-label={field.labelAr}
                    disabled={readOnly}
                    value={value == null ? '' : String(value)}
                    onChange={(e) => {
                      const next = { ...answers, [field.key]: e.target.value }
                      setAnswers(next)
                      queueSave(next)
                    }}
                  />
                )}
              </label>
            )
          })}
        </section>
      ))}

      {d.policy.requireSubmissionAcknowledgement && (
        <label className="field">
          <input type="checkbox" checked={ack} onChange={(e) => setAck(e.target.checked)} />
          <span>أقر بصحة البيانات المدخلة</span>
        </label>
      )}

      <div className="actions">
        {d.allowedActions.includes('Submit') && (
          <button
            type="button"
            className="button"
            disabled={submitMutation.isPending || saveState === 'conflict' || (d.policy.requireSubmissionAcknowledgement && !ack)}
            onClick={() => submitMutation.mutate()}
          >
            إرسال
          </button>
        )}
      </div>
      {submitMutation.isError && <p className="error" role="alert">فشل الإرسال.</p>}
    </div>
  )
}
