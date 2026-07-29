import type { MutableRefObject } from 'react'
import { api, ApiError, type FormVersionDetail } from '../../../api/client'
import { formatApiError } from '../../../forms/designer/designerHelpers'
import { createHistory, type HistoryState } from '../../../forms/designer/historyStore'
import {
  createEmptySchema,
  FormFieldTypeLabelsAr,
  FormFieldTypes,
  type FormFieldSchema,
  type FormFieldType,
  type FormSchemaDocument,
} from '../../../forms/designer/schemaTypes'
import type { AutosaveStatus } from '../../../forms/designer/useFormDesignerAutosave'
import type { ClassifiedIssue } from '../../../forms/designer/ValidationPanel'

export type ReviewDecisionAction = 'RequestChanges' | 'Reject' | 'ApproveAndLock'

const RECENT_TYPES_STORAGE_KEY = 'baseera.studio.recentFieldTypes'

export function newField(type: FormFieldType): FormFieldSchema {
  const id = crypto.randomUUID()
  return {
    id,
    key: `field_${id.slice(0, 8)}`,
    type,
    labelAr: FormFieldTypeLabelsAr[type] ?? 'حقل',
    order: 0,
    layoutWidth: 0,
    isRequired: false,
    validationRules: [],
    isReadOnly: type === FormFieldTypes.CalculatedNumber || type === FormFieldTypes.CalculatedText,
    isCalculated: type === FormFieldTypes.CalculatedNumber || type === FormFieldTypes.CalculatedText,
    choice: type === FormFieldTypes.SingleChoice || type === FormFieldTypes.MultipleChoice
      ? { options: [{ value: 'a', labelAr: 'خيار أ', order: 0, isActive: true }], allowOther: false }
      : null,
  }
}

export function parseSchema(json: string | undefined): FormSchemaDocument {
  if (!json) return createEmptySchema()
  try {
    const parsed = JSON.parse(json) as FormSchemaDocument
    return parsed.pages?.length ? parsed : createEmptySchema()
  } catch {
    return createEmptySchema()
  }
}

export function loadRecentTypes(): FormFieldType[] {
  try {
    const raw = window.localStorage.getItem(RECENT_TYPES_STORAGE_KEY)
    return raw ? (JSON.parse(raw) as FormFieldType[]) : []
  } catch {
    return []
  }
}

export function saveRecentTypes(types: FormFieldType[]) {
  try {
    window.localStorage.setItem(RECENT_TYPES_STORAGE_KEY, JSON.stringify(types.slice(0, 6)))
  } catch {
    // Non-critical preference; ignore storage failures (private mode, quota, etc.).
  }
}

export function resolveErrorMessage(err: unknown, fallbackAr: string): string {
  return err instanceof ApiError ? formatApiError(err) : fallbackAr
}

export function serializeSchemaOrNull(schema: FormSchemaDocument | undefined): string | null {
  return schema ? JSON.stringify(schema) : null
}

export async function runVersionReviewDecision(
  formId: string,
  versionId: string,
  action: ReviewDecisionAction,
  reason: string,
  rowVersion: string,
): Promise<FormVersionDetail> {
  const body = { reason, rowVersion }
  if (action === 'RequestChanges') return api.forms.requestVersionChanges(formId, versionId, body)
  if (action === 'Reject') return api.forms.rejectVersion(formId, versionId, body)
  return api.forms.approveLockVersion(formId, versionId, body)
}

export async function saveCurrentSchemaAsNewVersion(
  formId: string,
  schema: FormSchemaDocument | undefined,
): Promise<FormVersionDetail> {
  const created = await api.forms.createVersion(formId, {})
  if (schema) {
    await api.forms.saveSchema(formId, created.id, { schemaJson: JSON.stringify(schema), rowVersion: created.rowVersion })
  }
  return created
}

export function resolveFieldIssueTone(
  errors: ClassifiedIssue[],
  warnings: ClassifiedIssue[],
  fieldId: string,
): { hasError: boolean; hasWarning: boolean } {
  return {
    hasError: errors.some((issue) => issue.location.fieldId === fieldId),
    hasWarning: warnings.some((issue) => issue.location.fieldId === fieldId),
  }
}

export function countPageErrors(errors: ClassifiedIssue[], pageId: string): number {
  return errors.filter((issue) => issue.location.pageId === pageId).length
}

type SyncDesignerStateParams = {
  versionData: FormVersionDetail | undefined
  versionId: string
  initializedVersionIdRef: MutableRefObject<string | null>
  forceReseedRef: MutableRefObject<boolean>
  setHistory: (history: HistoryState) => void
  setSelectedPageId: (id: string | null) => void
  setSelectedFieldId: (id: string | null) => void
  setRowVersion: (rowVersion: string) => void
  markSavedBaseline: (json: string) => void
}

/** Keeps the studio's local editing history in sync with the server version once per load/reload —
 * either seeds a fresh history from the version's schema, or (on later renders of the same version)
 * just tracks the latest rowVersion. Extracted so the sync `useEffect` body stays a single call. */
export function syncDesignerStateFromVersion(params: SyncDesignerStateParams): void {
  const { versionData, versionId, initializedVersionIdRef, forceReseedRef, setHistory, setSelectedPageId, setSelectedFieldId, setRowVersion, markSavedBaseline } = params
  if (!versionData) return

  const isNewVersion = initializedVersionIdRef.current !== versionId
  if (!isNewVersion && !forceReseedRef.current) {
    setRowVersion(versionData.rowVersion)
    return
  }

  const nextSchema = parseSchema(versionData.draftSchemaJson)
  setHistory(createHistory(nextSchema))
  setSelectedPageId(nextSchema.pages[0]?.id ?? null)
  setSelectedFieldId(null)
  setRowVersion(versionData.rowVersion)
  markSavedBaseline(JSON.stringify(nextSchema))
  initializedVersionIdRef.current = versionId
  forceReseedRef.current = false
}

type SyncConflictSchemaParams = {
  status: AutosaveStatus
  formId: string
  versionId: string
  setConflictServerSchema: (schema: FormSchemaDocument | null) => void
}

/** Fetches the server's current schema for side-by-side comparison whenever autosave reports a
 * 409 conflict, and clears it once the conflict is resolved. */
export function syncConflictServerSchema(params: SyncConflictSchemaParams): void {
  const { status, formId, versionId, setConflictServerSchema } = params
  if (status !== 'conflict') {
    setConflictServerSchema(null)
    return
  }

  void api.forms.getVersion(formId, versionId)
    .then((latest) => setConflictServerSchema(parseSchema(latest.draftSchemaJson)))
    .catch(() => undefined)
}
