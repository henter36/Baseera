import { useQuery } from '@tanstack/react-query'
import { Link, useParams, useSearchParams } from 'react-router'
import { api, ApiError } from '../../../api/client'
import { usePermission } from '../../../auth/AuthProvider'
import { formatApiError } from '../../../forms/designer/designerHelpers'
import { VersionCompare } from '../../../forms/designer/VersionCompare'
import { createEmptySchema, type FormSchemaDocument } from '../../../forms/designer/schemaTypes'

function parseSchema(json: string | undefined): FormSchemaDocument {
  if (!json) return createEmptySchema()
  try {
    const parsed = JSON.parse(json) as FormSchemaDocument
    return parsed.pages?.length ? parsed : createEmptySchema()
  } catch {
    return createEmptySchema()
  }
}

export function FormVersionComparePage() {
  const canViewHistory = usePermission('Forms.ViewVersionHistory')
  const { formId } = useParams<{ formId: string }>()
  const [searchParams, setSearchParams] = useSearchParams()
  const fromId = searchParams.get('from') ?? ''
  const toId = searchParams.get('to') ?? ''

  const versionsQuery = useQuery({
    queryKey: ['form-versions', formId],
    queryFn: () => api.forms.listVersions(formId!),
    enabled: canViewHistory && !!formId,
  })

  const fromQuery = useQuery({
    queryKey: ['form-version', formId, fromId],
    queryFn: () => api.forms.getVersion(formId!, fromId),
    enabled: canViewHistory && !!formId && !!fromId,
  })

  const toQuery = useQuery({
    queryKey: ['form-version', formId, toId],
    queryFn: () => api.forms.getVersion(formId!, toId),
    enabled: canViewHistory && !!formId && !!toId,
  })

  if (!canViewHistory) {
    return <div className="error" role="alert">ليست لديك صلاحية عرض إصدارات النموذج.</div>
  }

  if (versionsQuery.isLoading) {
    return <div className="loading">جاري تحميل الإصدارات…</div>
  }

  if (versionsQuery.isError) {
    return <div className="error" role="alert">{formatApiError(versionsQuery.error as ApiError)}</div>
  }

  const versions = versionsQuery.data ?? []

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <h1 className="page-title">مقارنة الإصدارات</h1>
        <Link to={`/forms/${formId}/versions`} className="secondary">عودة للإصدارات</Link>
      </div>

      <div className="form-grid">
        <label className="field">
          <span>من الإصدار</span>
          <select
            value={fromId}
            onChange={(e) => setSearchParams((prev) => { prev.set('from', e.target.value); return prev })}
          >
            <option value="">اختر إصدارًا</option>
            {versions.map((v) => (
              <option key={v.id} value={v.id}>v{v.versionNumber} — {v.statusAr}</option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>إلى الإصدار</span>
          <select
            value={toId}
            onChange={(e) => setSearchParams((prev) => { prev.set('to', e.target.value); return prev })}
          >
            <option value="">اختر إصدارًا</option>
            {versions.map((v) => (
              <option key={v.id} value={v.id}>v{v.versionNumber} — {v.statusAr}</option>
            ))}
          </select>
        </label>
      </div>

      {fromQuery.isError && <div className="error" role="alert">{formatApiError(fromQuery.error as ApiError)}</div>}
      {toQuery.isError && <div className="error" role="alert">{formatApiError(toQuery.error as ApiError)}</div>}

      {fromQuery.data && toQuery.data && (
        <VersionCompare
          beforeSchema={parseSchema(fromQuery.data.draftSchemaJson)}
          afterSchema={parseSchema(toQuery.data.draftSchemaJson)}
          beforeLabelAr={`v${fromQuery.data.versionNumber}`}
          afterLabelAr={`v${toQuery.data.versionNumber}`}
        />
      )}
    </div>
  )
}
