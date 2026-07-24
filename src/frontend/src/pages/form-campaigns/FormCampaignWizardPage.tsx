import { useMutation, useQuery } from '@tanstack/react-query'
import { useMemo, useState, type ReactNode } from 'react'
import { Link, useNavigate } from 'react-router'
import { api, type CreateFormCampaignRequest, type FormCampaignScheduleRequest, type FormCampaignTargetRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { listQueryErrorMessage } from '../../shared/listPageUtils'

const STEPS = ['الإصدار', 'البيانات', 'الاستهداف', 'الاستثناءات', 'الجدولة', 'المعاينة', 'التأكيد'] as const

type FieldLabelProps = Readonly<{
  htmlFor: string
  children: ReactNode
}>

function FieldLabel({ htmlFor, children }: FieldLabelProps) {
  return (
    <label htmlFor={htmlFor}>
      <span>{children}</span>
    </label>
  )
}

export function FormCampaignWizardPage() {
  const canManage = usePermission('Forms.ManageCampaigns')
  const navigate = useNavigate()
  const [step, setStep] = useState(0)
  const [formDefinitionId, setFormDefinitionId] = useState('')
  const [formVersionId, setFormVersionId] = useState('')
  const [code, setCode] = useState(`CMP-${Date.now()}`)
  const [nameAr, setNameAr] = useState('')
  const [ruleType, setRuleType] = useState(0)
  const [regionIds, setRegionIds] = useState<string[]>([])
  const [facilityIds, setFacilityIds] = useState<string[]>([])
  const [exclusionFacilityId, setExclusionFacilityId] = useState('')
  const [exclusionReason, setExclusionReason] = useState('')
  const [exclusions, setExclusions] = useState<Array<{ facilityId: string; reason: string }>>([])
  const [recurrenceKind, setRecurrenceKind] = useState(0)
  const [firstOpenAtLocal, setFirstOpenAtLocal] = useState(() => {
    const d = new Date()
    d.setMinutes(0, 0, 0)
    return d.toISOString().slice(0, 16)
  })
  const [responseWindowMinutes, setResponseWindowMinutes] = useState(1440)
  const [gracePeriodMinutes, setGracePeriodMinutes] = useState(60)
  const [closeAfterMinutes, setCloseAfterMinutes] = useState(0)
  const [dayOfMonth, setDayOfMonth] = useState(1)
  const [missingDayPolicy, setMissingDayPolicy] = useState(0)
  const [errorSummary, setErrorSummary] = useState<string | null>(null)

  const forms = useQuery({
    queryKey: ['forms-for-campaign'],
    queryFn: () => api.forms.list({ page: 1, pageSize: 100 }),
    enabled: canManage,
  })
  const versions = useQuery({
    queryKey: ['locked-versions', formDefinitionId],
    queryFn: () => api.forms.listVersions(formDefinitionId),
    enabled: canManage && !!formDefinitionId,
  })
  const regions = useQuery({
    queryKey: ['campaign-regions'],
    queryFn: () => api.formCampaigns.targetRegions({ pageSize: 100 }),
    enabled: canManage && step >= 2,
  })
  const facilities = useQuery({
    queryKey: ['campaign-facilities'],
    queryFn: () => api.formCampaigns.targetFacilities({ pageSize: 100 }),
    enabled: canManage && step >= 2,
  })

  const schedule: FormCampaignScheduleRequest = useMemo(() => ({
    recurrenceKind,
    firstOpenAtLocal: new Date(firstOpenAtLocal).toISOString(),
    responseWindowMinutes,
    gracePeriodMinutes,
    closeAfterMinutes,
    businessDayAdjustment: 0,
    intervalDays: recurrenceKind === 1 ? 1 : null,
    intervalWeeks: recurrenceKind === 2 ? 1 : null,
    weekDays: recurrenceKind === 2 ? [1] : null,
    dayOfMonth: recurrenceKind === 3 ? dayOfMonth : null,
    missingDayPolicy: recurrenceKind === 3 ? missingDayPolicy : null,
    untilLocal: null,
    maxOccurrences: null,
    customDatesLocal: null,
  }), [recurrenceKind, firstOpenAtLocal, responseWindowMinutes, gracePeriodMinutes, closeAfterMinutes, dayOfMonth, missingDayPolicy])

  const targets: FormCampaignTargetRequest[] = useMemo(() => [{
    ruleType,
    regionIds: ruleType === 1 ? regionIds : null,
    facilityIds: ruleType === 2 ? facilityIds : null,
    dynamicCriteria: ruleType === 3 ? { isActive: true } : null,
  }], [ruleType, regionIds, facilityIds])

  const upcoming = useQuery({
    queryKey: ['schedule-preview', schedule],
    queryFn: () => api.formCampaigns.schedulePreview(schedule, 'Asia/Riyadh'),
    enabled: canManage && step >= 5,
  })

  const create = useMutation({
    mutationFn: async () => {
      const body: CreateFormCampaignRequest = {
        formDefinitionId,
        formVersionId,
        code,
        nameAr,
        priority: 1,
        timeZoneId: 'Asia/Riyadh',
        schedule,
        targets,
        exclusions,
      }
      return api.formCampaigns.create(body)
    },
    onSuccess: (created) => navigate(`/form-campaigns/${created.id}`),
    onError: (err) => setErrorSummary(listQueryErrorMessage(err, 'ليست لديك صلاحية.', 'تعذر إكمال العملية.')),
  })

  if (!canManage) {
    return <div className="error" role="alert">ليست لديك صلاحية إدارة الحملات.</div>
  }

  const lockedVersions = (versions.data ?? []).filter((v) => v.status === 4)

  return (
    <div className="panel" dir="rtl">
      <div className="page-header">
        <div>
          <h1>معالج إنشاء حملة نشر</h1>
          <p className="muted">الخطوة {step + 1} من {STEPS.length}: {STEPS[step]}</p>
        </div>
        <Link to="/form-campaigns">رجوع</Link>
      </div>

      <ol className="toolbar" aria-label="خطوات المعالج">
        {STEPS.map((label, index) => (
          <li key={label}><button type="button" className={index === step ? 'active' : undefined} onClick={() => setStep(index)}>{label}</button></li>
        ))}
      </ol>

      {errorSummary && <div className="error" role="alert">{errorSummary}</div>}

      {step === 0 && (
        <div className="form-grid">
          <div>
            <FieldLabel htmlFor="campaign-form-definition">النموذج</FieldLabel>
            <select
              id="campaign-form-definition"
              value={formDefinitionId}
              onChange={(e) => { setFormDefinitionId(e.target.value); setFormVersionId('') }}
            >
              <option value="">اختر…</option>
              {(forms.data?.items ?? []).map((f) => <option key={f.id} value={f.id}>{f.nameAr}</option>)}
            </select>
          </div>
          <div>
            <FieldLabel htmlFor="campaign-form-version">إصدار مقفل</FieldLabel>
            <select
              id="campaign-form-version"
              value={formVersionId}
              onChange={(e) => setFormVersionId(e.target.value)}
            >
              <option value="">اختر…</option>
              {lockedVersions.map((v) => <option key={v.id} value={v.id}>v{v.versionNumber}</option>)}
            </select>
          </div>
        </div>
      )}

      {step === 1 && (
        <div className="form-grid">
          <div>
            <FieldLabel htmlFor="campaign-code">الرمز</FieldLabel>
            <input id="campaign-code" value={code} onChange={(e) => setCode(e.target.value)} />
          </div>
          <div>
            <FieldLabel htmlFor="campaign-name-ar">الاسم العربي</FieldLabel>
            <input id="campaign-name-ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} />
          </div>
        </div>
      )}

      {step === 2 && (
        <div className="form-grid">
          <div>
            <FieldLabel htmlFor="campaign-rule-type">نمط الاستهداف</FieldLabel>
            <select
              id="campaign-rule-type"
              value={ruleType}
              onChange={(e) => setRuleType(Number(e.target.value))}
            >
              <option value={0}>جميع المواقع</option>
              <option value={1}>مناطق محددة</option>
              <option value={2}>مواقع محددة</option>
              <option value={3}>مجموعة ديناميكية</option>
            </select>
          </div>
          {ruleType === 1 && (
            <div>
              <FieldLabel htmlFor="campaign-regions">المناطق</FieldLabel>
              <select
                id="campaign-regions"
                multiple
                value={regionIds}
                onChange={(e) => setRegionIds(Array.from(e.target.selectedOptions).map((o) => o.value))}
              >
                {(regions.data?.items ?? []).map((r) => <option key={r.facilityId} value={r.facilityId}>{r.nameAr}</option>)}
              </select>
            </div>
          )}
          {ruleType === 2 && (
            <div>
              <FieldLabel htmlFor="campaign-facilities">المواقع</FieldLabel>
              <select
                id="campaign-facilities"
                multiple
                value={facilityIds}
                onChange={(e) => setFacilityIds(Array.from(e.target.selectedOptions).map((o) => o.value))}
              >
                {(facilities.data?.items ?? []).map((f) => <option key={f.facilityId} value={f.facilityId}>{f.nameAr}</option>)}
              </select>
            </div>
          )}
        </div>
      )}

      {step === 3 && (
        <div className="form-grid">
          <div>
            <FieldLabel htmlFor="campaign-exclusion-facility">موقع مستثنى</FieldLabel>
            <select
              id="campaign-exclusion-facility"
              value={exclusionFacilityId}
              onChange={(e) => setExclusionFacilityId(e.target.value)}
            >
              <option value="">اختر…</option>
              {(facilities.data?.items ?? []).map((f) => <option key={f.facilityId} value={f.facilityId}>{f.nameAr}</option>)}
            </select>
          </div>
          <div>
            <FieldLabel htmlFor="campaign-exclusion-reason">سبب الاستثناء</FieldLabel>
            <input
              id="campaign-exclusion-reason"
              value={exclusionReason}
              onChange={(e) => setExclusionReason(e.target.value)}
            />
          </div>
          <button type="button" onClick={() => {
            if (!exclusionFacilityId || !exclusionReason.trim()) return
            setExclusions((prev) => [...prev, { facilityId: exclusionFacilityId, reason: exclusionReason.trim() }])
            setExclusionFacilityId('')
            setExclusionReason('')
          }}>إضافة استثناء</button>
          <ul>{exclusions.map((e) => <li key={e.facilityId}>{e.facilityId}: {e.reason}</li>)}</ul>
        </div>
      )}

      {step === 4 && (
        <div className="form-grid">
          <div>
            <FieldLabel htmlFor="campaign-recurrence">التكرار</FieldLabel>
            <select
              id="campaign-recurrence"
              value={recurrenceKind}
              onChange={(e) => setRecurrenceKind(Number(e.target.value))}
            >
              <option value={0}>مرة واحدة</option>
              <option value={1}>يومي</option>
              <option value={2}>أسبوعي</option>
              <option value={3}>شهري</option>
            </select>
          </div>
          <div>
            <FieldLabel htmlFor="campaign-first-open">أول فتح محلي</FieldLabel>
            <input
              id="campaign-first-open"
              type="datetime-local"
              value={firstOpenAtLocal}
              onChange={(e) => setFirstOpenAtLocal(e.target.value)}
            />
          </div>
          <div>
            <FieldLabel htmlFor="campaign-response-window">نافذة الاستجابة (دقيقة)</FieldLabel>
            <input
              id="campaign-response-window"
              type="number"
              value={responseWindowMinutes}
              onChange={(e) => setResponseWindowMinutes(Number(e.target.value))}
            />
          </div>
          <div>
            <FieldLabel htmlFor="campaign-grace">مهلة السماح</FieldLabel>
            <input
              id="campaign-grace"
              type="number"
              value={gracePeriodMinutes}
              onChange={(e) => setGracePeriodMinutes(Number(e.target.value))}
            />
          </div>
          <div>
            <FieldLabel htmlFor="campaign-close-after">الإغلاق بعد</FieldLabel>
            <input
              id="campaign-close-after"
              type="number"
              value={closeAfterMinutes}
              onChange={(e) => setCloseAfterMinutes(Number(e.target.value))}
            />
          </div>
          {recurrenceKind === 3 && (
            <>
              <div>
                <FieldLabel htmlFor="campaign-day-of-month">يوم الشهر</FieldLabel>
                <input
                  id="campaign-day-of-month"
                  type="number"
                  min={1}
                  max={31}
                  value={dayOfMonth}
                  onChange={(e) => setDayOfMonth(Number(e.target.value))}
                />
              </div>
              <div>
                <FieldLabel htmlFor="campaign-missing-day-policy">سياسة اليوم المفقود</FieldLabel>
                <select
                  id="campaign-missing-day-policy"
                  value={missingDayPolicy}
                  onChange={(e) => setMissingDayPolicy(Number(e.target.value))}
                >
                  <option value={0}>آخر يوم في الشهر</option>
                  <option value={1}>تخطي الاستحقاق</option>
                </select>
              </div>
            </>
          )}
        </div>
      )}

      {step === 5 && (
        <div>
          <p>المواعيد القادمة (Asia/Riyadh):</p>
          <ul>{(upcoming.data ?? []).map((d) => <li key={d}>{new Date(d).toLocaleString('ar-SA', { timeZone: 'Asia/Riyadh' })}</li>)}</ul>
        </div>
      )}

      {step === 6 && (
        <div className="detail-grid">
          <div><strong>النموذج</strong><div>{formDefinitionId}</div></div>
          <div><strong>الإصدار</strong><div>{formVersionId}</div></div>
          <div><strong>الاستهداف</strong><div>{ruleType}</div></div>
          <div><strong>الاستثناءات</strong><div>{exclusions.length}</div></div>
          <div><strong>التكرار</strong><div>{recurrenceKind}</div></div>
          <button type="button" disabled={create.isPending} onClick={() => {
            if (!formDefinitionId || !formVersionId || !nameAr.trim()) {
              setErrorSummary('أكمل الحقول المطلوبة قبل الحفظ.')
              return
            }
            create.mutate()
          }}>حفظ المسودة</button>
        </div>
      )}

      <div className="form-actions">
        <button type="button" disabled={step === 0} onClick={() => setStep((s) => s - 1)}>السابق</button>
        <button type="button" disabled={step >= STEPS.length - 1} onClick={() => setStep((s) => s + 1)}>التالي</button>
      </div>
    </div>
  )
}
