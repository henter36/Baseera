import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api, ApiError, type CreateFormRequest, type FormTemplateListItem } from '../../../api/client'
import { formatApiError } from '../../../forms/designer/designerHelpers'
import { FormPreviewPanel } from '../../../forms/designer/FormPreviewPanel'
import { ScopeType, ScopeTypeLabelsAr } from '../../../forms/formEnums'
import { parseSchema } from './studioWorkspaceHelpers'

type StartFlowMode = 'choose' | 'blank' | 'template' | 'copy'

const USAGE_TYPES = [
  { value: 'once', labelAr: 'مرة واحدة' },
  { value: 'daily', labelAr: 'يومي' },
  { value: 'weekly', labelAr: 'أسبوعي' },
  { value: 'monthly', labelAr: 'شهري' },
  { value: 'custom', labelAr: 'مخصص' },
]

const TEMPLATE_VISIBILITY_LABELS_AR: Record<number, string> = {
  0: 'عام (على مستوى المنظمة)',
  1: 'خاص بإدارة',
  2: 'خاص',
}

function generateFormCode(): string {
  return `FORM-${Date.now().toString(36).toUpperCase()}`
}

type IdentityDraft = {
  nameAr: string
  description: string
  scopeType: number
  regionId: string
  facilityId: string
  facilityUnitId: string
}

function IdentityFields({ draft, onChange }: Readonly<{ draft: IdentityDraft; onChange: (next: IdentityDraft) => void }>) {
  const regionsQuery = useQuery({ queryKey: ['form-regions'], queryFn: () => api.regions() })
  const facilitiesQuery = useQuery({
    queryKey: ['form-facilities', draft.regionId],
    queryFn: () => api.facilities(draft.regionId || undefined),
    enabled: !!draft.regionId,
  })
  const unitsQuery = useQuery({
    queryKey: ['form-facility-units', draft.facilityId],
    queryFn: () => api.facilityUnits(draft.facilityId),
    enabled: !!draft.facilityId && draft.scopeType === ScopeType.FacilityUnit,
  })

  const showRegion = draft.scopeType === ScopeType.Region || draft.scopeType === ScopeType.Facility || draft.scopeType === ScopeType.FacilityUnit
  const showFacility = draft.scopeType === ScopeType.Facility || draft.scopeType === ScopeType.FacilityUnit
  const showUnit = draft.scopeType === ScopeType.FacilityUnit

  return (
    <div className="form-grid">
      <label className="field field-wide">
        <span>اسم النموذج *</span>
        <input value={draft.nameAr} onChange={(e) => onChange({ ...draft, nameAr: e.target.value })} />
      </label>
      <label className="field field-wide">
        <span>الغرض أو الوصف *</span>
        <textarea rows={2} value={draft.description} onChange={(e) => onChange({ ...draft, description: e.target.value })} />
      </label>
      <label className="field">
        <span>نطاق النموذج *</span>
        <select value={draft.scopeType} onChange={(e) => onChange({ ...draft, scopeType: Number(e.target.value), regionId: '', facilityId: '', facilityUnitId: '' })}>
          {[ScopeType.Global, ScopeType.Headquarters, ScopeType.Region, ScopeType.Facility, ScopeType.FacilityUnit].map((s) => (
            <option key={s} value={s}>{ScopeTypeLabelsAr[s]}</option>
          ))}
        </select>
      </label>
      {showRegion && (
        <label className="field">
          <span>المنطقة *</span>
          <select value={draft.regionId} onChange={(e) => onChange({ ...draft, regionId: e.target.value, facilityId: '', facilityUnitId: '' })}>
            <option value="">اختر المنطقة</option>
            {regionsQuery.data?.items.map((r) => <option key={r.id} value={r.id}>{r.nameAr}</option>)}
          </select>
        </label>
      )}
      {showFacility && (
        <label className="field">
          <span>السجن *</span>
          <select value={draft.facilityId} onChange={(e) => onChange({ ...draft, facilityId: e.target.value, facilityUnitId: '' })}>
            <option value="">اختر السجن</option>
            {facilitiesQuery.data?.items.map((f) => <option key={f.id} value={f.id}>{f.nameAr}</option>)}
          </select>
        </label>
      )}
      {showUnit && (
        <label className="field">
          <span>الوحدة *</span>
          <select value={draft.facilityUnitId} onChange={(e) => onChange({ ...draft, facilityUnitId: e.target.value })}>
            <option value="">اختر الوحدة</option>
            {unitsQuery.data?.items.map((u) => <option key={u.id} value={u.id}>{u.nameAr}</option>)}
          </select>
        </label>
      )}
    </div>
  )
}

function isIdentityValid(draft: IdentityDraft): boolean {
  if (!draft.nameAr.trim() || !draft.description.trim()) return false
  if (draft.scopeType === ScopeType.Region && !draft.regionId) return false
  if ((draft.scopeType === ScopeType.Facility || draft.scopeType === ScopeType.FacilityUnit) && (!draft.regionId || !draft.facilityId)) return false
  if (draft.scopeType === ScopeType.FacilityUnit && !draft.facilityUnitId) return false
  return true
}

function toCreateRequest(draft: IdentityDraft): CreateFormRequest {
  return {
    code: generateFormCode(),
    nameAr: draft.nameAr.trim(),
    nameEn: null,
    description: draft.description.trim(),
    classification: 0,
    scopeType: draft.scopeType,
    regionId: draft.regionId || null,
    facilityId: draft.facilityId || null,
    facilityUnitId: draft.scopeType === ScopeType.FacilityUnit ? draft.facilityUnitId || null : null,
    ownerDepartmentId: null,
  }
}

const EMPTY_DRAFT: IdentityDraft = { nameAr: '', description: '', scopeType: ScopeType.Facility, regionId: '', facilityId: '', facilityUnitId: '' }

function BlankFormFlow({ onCreated, onBack }: Readonly<{ onCreated: (formId: string, versionId: string) => void; onBack: () => void }>) {
  const [draft, setDraft] = useState<IdentityDraft>(EMPTY_DRAFT)
  const [usageType, setUsageType] = useState('once')
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: async () => {
      const form = await api.forms.create(toCreateRequest(draft))
      const version = await api.forms.createVersion(form.id)
      return { formId: form.id, versionId: version.id }
    },
    onSuccess: ({ formId, versionId }) => onCreated(formId, versionId),
    onError: (err) => setError(err instanceof ApiError ? formatApiError(err) : 'تعذر إنشاء النموذج.'),
  })

  return (
    <div className="panel-section">
      <div className="page-header">
        <h2 className="section-title">نموذج فارغ</h2>
        <button type="button" className="secondary" onClick={onBack}>عودة</button>
      </div>
      <IdentityFields draft={draft} onChange={setDraft} />
      <label className="field">
        <span>نوع الاستخدام</span>
        <select value={usageType} onChange={(e) => setUsageType(e.target.value)}>
          {USAGE_TYPES.map((u) => <option key={u.value} value={u.value}>{u.labelAr}</option>)}
        </select>
      </label>
      {error && <div className="error" role="alert">{error}</div>}
      <button type="button" disabled={!isIdentityValid(draft) || mutation.isPending} onClick={() => mutation.mutate()}>
        {mutation.isPending ? 'جارٍ الإنشاء…' : 'إنشاء وفتح الاستوديو'}
      </button>
    </div>
  )
}

function TemplateFlow({ onCreated, onBack }: Readonly<{ onCreated: (formId: string, versionId: string) => void; onBack: () => void }>) {
  const [selected, setSelected] = useState<FormTemplateListItem | null>(null)
  const [previewing, setPreviewing] = useState(false)
  const [draft, setDraft] = useState<IdentityDraft>(EMPTY_DRAFT)
  const [error, setError] = useState<string | null>(null)

  const listQuery = useQuery({ queryKey: ['form-templates'], queryFn: () => api.formTemplates.list() })
  const previewQuery = useQuery({
    queryKey: ['form-template-schema', selected?.id],
    queryFn: () => api.formTemplates.getSchema(selected!.id),
    enabled: previewing && !!selected,
  })

  const mutation = useMutation({
    mutationFn: async () => {
      if (!selected) throw new Error('لم يتم اختيار قالب.')
      const form = await api.formTemplates.createForm(selected.id, toCreateRequest(draft))
      const versions = await api.forms.listVersions(form.id)
      const version = versions[0]
      if (!version) throw new Error('تعذر تحديد إصدار النموذج الجديد.')
      return { formId: form.id, versionId: version.id }
    },
    onSuccess: ({ formId, versionId }) => onCreated(formId, versionId),
    onError: (err) => setError(err instanceof ApiError ? formatApiError(err) : 'تعذر إنشاء النموذج من القالب.'),
  })

  if (listQuery.isLoading) return <div className="loading">جاري تحميل القوالب…</div>
  if (listQuery.isError) return <div className="error" role="alert">{formatApiError(listQuery.error as ApiError)}</div>

  const templates = listQuery.data ?? []

  return (
    <div className="panel-section">
      <div className="page-header">
        <h2 className="section-title">استخدام قالب</h2>
        <button type="button" className="secondary" onClick={onBack}>عودة</button>
      </div>

      {templates.length === 0 && <div className="empty">لا توجد قوالب متاحة ضمن نطاقك.</div>}

      <ul>
        {templates.map((t) => (
          <li key={t.id}>
            <strong>{t.nameAr}</strong> — {t.category} — {t.pageCount} صفحات، {t.fieldCount} حقول — {TEMPLATE_VISIBILITY_LABELS_AR[t.visibility] ?? '—'}
            <p className="muted">{t.description}</p>
            <div className="toolbar">
              <button type="button" className="secondary" onClick={() => { setSelected(t); setPreviewing(true) }}>معاينة</button>
              <button type="button" onClick={() => { setSelected(t); setPreviewing(false) }}>استخدام هذا القالب</button>
            </div>
          </li>
        ))}
      </ul>

      {previewing && selected && previewQuery.data && (
        <FormPreviewPanel
          schema={parseSchema(previewQuery.data.canonicalSchemaJson)}
          mode="desktop"
          onModeChange={() => undefined}
          onClose={() => setPreviewing(false)}
        />
      )}

      {selected && !previewing && (
        <div className="panel-section">
          <h3 className="section-title">بيانات النموذج الجديد من القالب «{selected.nameAr}»</h3>
          <IdentityFields draft={draft} onChange={setDraft} />
          {error && <div className="error" role="alert">{error}</div>}
          <button type="button" disabled={!isIdentityValid(draft) || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'جارٍ الإنشاء…' : 'إنشاء وفتح الاستوديو'}
          </button>
        </div>
      )}
    </div>
  )
}

function CopyExistingFlow({ onCreated, onBack }: Readonly<{ onCreated: (formId: string, versionId: string) => void; onBack: () => void }>) {
  const [search, setSearch] = useState('')
  const [sourceFormId, setSourceFormId] = useState('')
  const [sourceVersionId, setSourceVersionId] = useState('')
  const [draft, setDraft] = useState<IdentityDraft>(EMPTY_DRAFT)
  const [error, setError] = useState<string | null>(null)

  const searchQuery = useQuery({
    queryKey: ['forms-search', search],
    queryFn: () => api.forms.list({ search, pageSize: 10 }),
    enabled: search.trim().length >= 2,
  })

  const versionsQuery = useQuery({
    queryKey: ['form-versions', sourceFormId],
    queryFn: () => api.forms.listVersions(sourceFormId),
    enabled: !!sourceFormId,
  })

  const mutation = useMutation({
    mutationFn: async () => {
      const created = await api.forms.copyFromExistingForm(sourceFormId, sourceVersionId, toCreateRequest(draft))
      return { formId: created.formDefinitionId, versionId: created.id }
    },
    onSuccess: ({ formId, versionId }) => onCreated(formId, versionId),
    onError: (err) => setError(err instanceof ApiError ? formatApiError(err) : 'تعذر نسخ النموذج.'),
  })

  return (
    <div className="panel-section">
      <div className="page-header">
        <h2 className="section-title">نسخ نموذج موجود</h2>
        <button type="button" className="secondary" onClick={onBack}>عودة</button>
      </div>

      <label className="field field-wide">
        <span>ابحث ضمن النماذج المصرح لك بعرضها</span>
        <input value={search} onChange={(e) => { setSearch(e.target.value); setSourceFormId(''); setSourceVersionId('') }} placeholder="اسم أو رمز النموذج…" />
      </label>

      {searchQuery.data && searchQuery.data.items.length > 0 && (
        <ul>
          {searchQuery.data.items.map((f) => (
            <li key={f.id}>
              <button type="button" className={sourceFormId === f.id ? undefined : 'secondary'} onClick={() => { setSourceFormId(f.id); setSourceVersionId('') }}>
                {f.nameAr} ({f.code})
              </button>
            </li>
          ))}
        </ul>
      )}

      {sourceFormId && (
        <label className="field">
          <span>الإصدار المصدر *</span>
          <select value={sourceVersionId} onChange={(e) => setSourceVersionId(e.target.value)}>
            <option value="">اختر إصدارًا</option>
            {(versionsQuery.data ?? []).map((v) => (
              <option key={v.id} value={v.id}>v{v.versionNumber} — {v.statusAr}</option>
            ))}
          </select>
        </label>
      )}

      {sourceVersionId && (
        <div className="panel-section">
          <h3 className="section-title">بيانات النموذج الجديد</h3>
          <p className="muted">سيُنسخ Schema الإصدار المحدد إلى مسودة جديدة فقط؛ لن تُنسخ الاستجابات أو حملات النشر.</p>
          <IdentityFields draft={draft} onChange={setDraft} />
          {error && <div className="error" role="alert">{error}</div>}
          <button type="button" disabled={!isIdentityValid(draft) || mutation.isPending} onClick={() => mutation.mutate()}>
            {mutation.isPending ? 'جارٍ النسخ…' : 'نسخ وفتح الاستوديو'}
          </button>
        </div>
      )}
    </div>
  )
}

export function StudioStartFlow({ onCreated }: Readonly<{ onCreated: (formId: string, versionId: string) => void }>) {
  const [mode, setMode] = useState<StartFlowMode>('choose')

  if (mode === 'blank') return <BlankFormFlow onCreated={onCreated} onBack={() => setMode('choose')} />
  if (mode === 'template') return <TemplateFlow onCreated={onCreated} onBack={() => setMode('choose')} />
  if (mode === 'copy') return <CopyExistingFlow onCreated={onCreated} onBack={() => setMode('choose')} />

  return (
    <div className="studio-start-flow">
      <button type="button" className="studio-start-option" onClick={() => setMode('blank')}>
        <h2 className="section-title">نموذج فارغ</h2>
        <p className="muted">ابدأ من الصفر باسم وغرض ونوع استخدام فقط.</p>
      </button>
      <button type="button" className="studio-start-option" onClick={() => setMode('template')}>
        <h2 className="section-title">استخدام قالب</h2>
        <p className="muted">ابدأ من قالب جاهز مع معاينة قبل الاستخدام.</p>
      </button>
      <button type="button" className="studio-start-option" onClick={() => setMode('copy')}>
        <h2 className="section-title">نسخ نموذج موجود</h2>
        <p className="muted">انسخ مخطط نموذج مصرح لك بعرضه إلى مسودة جديدة.</p>
      </button>
    </div>
  )
}
