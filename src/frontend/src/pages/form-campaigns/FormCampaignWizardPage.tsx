import { useMutation, useQuery } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, type CreateFormCampaignRequest, type FormCampaignScheduleRequest, type FormCampaignTargetRequest } from '../../api/client'
import { usePermission } from '../../auth/AuthProvider'
import { listQueryErrorMessage } from '../../shared/listPageUtils'

const STEPS = ['الإصدار', 'البيانات', 'الاستهداف', 'الاستثناءات', 'الجدولة', 'المعاينة', 'التأكيد'] as const

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
          <label>النموذج
            <select value={formDefinitionId} onChange={(e) => { setFormDefinitionId(e.target.value); setFormVersionId('') }}>
              <option value="">اختر…</option>
              {(forms.data?.items ?? []).map((f) => <option key={f.id} value={f.id}>{f.nameAr}</option>)}
            </select>
          </label>
          <label>إصدار مقفل
            <select value={formVersionId} onChange={(e) => setFormVersionId(e.target.value)}>
              <option value="">اختر…</option>
              {lockedVersions.map((v) => <option key={v.id} value={v.id}>v{v.versionNumber}</option>)}
            </select>
          </label>
        </div>
      )}

      {step === 1 && (
        <div className="form-grid">
          <label>الرمز<input value={code} onChange={(e) => setCode(e.target.value)} /></label>
          <label>الاسم العربي<input value={nameAr} onChange={(e) => setNameAr(e.target.value)} /></label>
        </div>
      )}

      {step === 2 && (
        <div className="form-grid">
          <label>نمط الاستهداف
            <select value={ruleType} onChange={(e) => setRuleType(Number(e.target.value))}>
              <option value={0}>جميع المواقع</option>
              <option value={1}>مناطق محددة</option>
              <option value={2}>مواقع محددة</option>
              <option value={3}>مجموعة ديناميكية</option>
            </select>
          </label>
          {ruleType === 1 && (
            <label>المناطق
              <select multiple value={regionIds} onChange={(e) => setRegionIds(Array.from(e.target.selectedOptions).map((o) => o.value))}>
                {(regions.data?.items ?? []).map((r) => <option key={r.facilityId} value={r.facilityId}>{r.nameAr}</option>)}
              </select>
            </label>
          )}
          {ruleType === 2 && (
            <label>المواقع
              <select multiple value={facilityIds} onChange={(e) => setFacilityIds(Array.from(e.target.selectedOptions).map((o) => o.value))}>
                {(facilities.data?.items ?? []).map((f) => <option key={f.facilityId} value={f.facilityId}>{f.nameAr}</option>)}
              </select>
            </label>
          )}
        </div>
      )}

      {step === 3 && (
        <div className="form-grid">
          <label>موقع مستثنى
            <select value={exclusionFacilityId} onChange={(e) => setExclusionFacilityId(e.target.value)}>
              <option value="">اختر…</option>
              {(facilities.data?.items ?? []).map((f) => <option key={f.facilityId} value={f.facilityId}>{f.nameAr}</option>)}
            </select>
          </label>
          <label>سبب الاستثناء<input value={exclusionReason} onChange={(e) => setExclusionReason(e.target.value)} /></label>
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
          <label>التكرار
            <select value={recurrenceKind} onChange={(e) => setRecurrenceKind(Number(e.target.value))}>
              <option value={0}>مرة واحدة</option>
              <option value={1}>يومي</option>
              <option value={2}>أسبوعي</option>
              <option value={3}>شهري</option>
            </select>
          </label>
          <label>أول فتح محلي<input type="datetime-local" value={firstOpenAtLocal} onChange={(e) => setFirstOpenAtLocal(e.target.value)} /></label>
          <label>نافذة الاستجابة (دقيقة)<input type="number" value={responseWindowMinutes} onChange={(e) => setResponseWindowMinutes(Number(e.target.value))} /></label>
          <label>مهلة السماح<input type="number" value={gracePeriodMinutes} onChange={(e) => setGracePeriodMinutes(Number(e.target.value))} /></label>
          <label>الإغلاق بعد<input type="number" value={closeAfterMinutes} onChange={(e) => setCloseAfterMinutes(Number(e.target.value))} /></label>
          {recurrenceKind === 3 && (
            <>
              <label>يوم الشهر<input type="number" min={1} max={31} value={dayOfMonth} onChange={(e) => setDayOfMonth(Number(e.target.value))} /></label>
              <label>سياسة اليوم المفقود
                <select value={missingDayPolicy} onChange={(e) => setMissingDayPolicy(Number(e.target.value))}>
                  <option value={0}>آخر يوم في الشهر</option>
                  <option value={1}>تخطي الاستحقاق</option>
                </select>
              </label>
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
