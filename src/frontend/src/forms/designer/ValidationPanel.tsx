import type { FormSchemaValidationIssue } from '../../api/client'
import { flattenFields } from './fieldDependencies'
import type { FormFieldSchema, FormSchemaDocument } from './schemaTypes'

export type IssueLocation = {
  pageId: string | null
  pageTitleAr: string | null
  sectionId: string | null
  sectionTitleAr: string | null
  fieldId: string | null
  fieldLabelAr: string | null
}

const EMPTY_LOCATION: IssueLocation = {
  pageId: null,
  pageTitleAr: null,
  sectionId: null,
  sectionTitleAr: null,
  fieldId: null,
  fieldLabelAr: null,
}

function findPageOrSectionLocation(schema: FormSchemaDocument, entityId: string): IssueLocation | null {
  for (const page of schema.pages) {
    if (page.id === entityId) {
      return { ...EMPTY_LOCATION, pageId: page.id, pageTitleAr: page.titleAr }
    }
    const section = page.sections.find((s) => s.id === entityId)
    if (section) {
      return { ...EMPTY_LOCATION, pageId: page.id, pageTitleAr: page.titleAr, sectionId: section.id, sectionTitleAr: section.titleAr }
    }
  }
  return null
}

function matchesIssueField(field: FormFieldSchema, issue: FormSchemaValidationIssue): boolean {
  return field.id === issue.entityId || field.key.toLowerCase() === issue.fieldKey?.toLowerCase()
}

function findFieldLocation(schema: FormSchemaDocument, issue: FormSchemaValidationIssue): IssueLocation | null {
  const match = flattenFields(schema).find(({ field }) => matchesIssueField(field, issue))
  if (!match) return null

  const { field, pageId, sectionId } = match
  const page = schema.pages.find((p) => p.id === pageId)
  const section = page?.sections.find((s) => s.id === sectionId)
  return {
    pageId,
    pageTitleAr: page?.titleAr ?? null,
    sectionId,
    sectionTitleAr: section?.titleAr ?? null,
    fieldId: field.id,
    fieldLabelAr: field.labelAr,
  }
}

export function locateIssue(schema: FormSchemaDocument, issue: FormSchemaValidationIssue): IssueLocation {
  const entityLocation = issue.entityId ? findPageOrSectionLocation(schema, issue.entityId) : null
  return entityLocation ?? findFieldLocation(schema, issue) ?? EMPTY_LOCATION
}

const ACTION_HINTS_AR: Record<string, string> = {
  DuplicateFieldKey: 'غيّر مفتاح الحقل إلى قيمة فريدة.',
  DuplicateOptionKey: 'غيّر مفتاح الخيار المكرر إلى قيمة فريدة.',
  EmptyPage: 'أضف حقلًا واحدًا على الأقل أو احذف الصفحة الفارغة.',
  ConditionReferencesDeletedField: 'أعد ربط الشرط بحقل موجود أو احذف الشرط.',
  ConditionCycle: 'أزل أحد الشروط لكسر الدورة بين الحقول.',
  FormulaCycle: 'أزل أحد مراجع الصيغة لكسر الدورة بين الحقول.',
  FormulaIncompatibleType: 'استخدم حقلًا رقميًا في هذه العملية الحسابية.',
  FieldWithoutLabel: 'أضف عنوانًا للحقل.',
  RequiredFieldAlwaysHidden: 'عدّل شرط الظهور أو ألغِ إلزامية هذا الحقل.',
}

export type ClassifiedIssue = FormSchemaValidationIssue & {
  location: IssueLocation
  actionAr: string
}

/** A stable, order-independent identity for a validation issue — the server does not assign
 * issues a dedicated id, so this composes the fields that together identify a specific issue
 * instance (rule, path, and the element it points at) instead of falling back to array index. */
export function validationIssueKey(issue: ClassifiedIssue): string {
  const target = issue.location.fieldId ?? issue.location.sectionId ?? issue.location.pageId ?? 'form'
  return `${issue.code}:${issue.path}:${target}`
}

export function classifyIssues(
  schema: FormSchemaDocument,
  issues: FormSchemaValidationIssue[],
): { errors: ClassifiedIssue[]; warnings: ClassifiedIssue[] } {
  const decorated = issues.map((issue) => ({
    ...issue,
    location: locateIssue(schema, issue),
    actionAr: ACTION_HINTS_AR[issue.code] ?? 'راجع هذا العنصر وعدّل الإعداد المرتبط بالرسالة أعلاه.',
  }))
  return {
    errors: decorated.filter((i) => i.severity === 0),
    warnings: decorated.filter((i) => i.severity !== 0),
  }
}

export type Suggestion = { id: string; messageAr: string; actionAr: string }

/** Purely local, non-blocking authoring hints. Never sent to or validated by the server. */
export function computeSuggestions(schema: FormSchemaDocument): Suggestion[] {
  const suggestions: Suggestion[] = []
  const fields = flattenFields(schema)

  if (fields.length === 0) {
    suggestions.push({ id: 'no-fields', messageAr: 'النموذج لا يحتوي على أي حقل بعد.', actionAr: 'أضف حقلًا واحدًا على الأقل من مكتبة الحقول.' })
  }

  if (fields.length > 0 && !fields.some(({ field }) => !field.isReadOnly)) {
    suggestions.push({
      id: 'no-answerable-fields',
      messageAr: 'كل الحقول للقراءة فقط أو محسوبة؛ لا يوجد سؤال يحتاج إجابة فعلية من المستخدم.',
      actionAr: 'أضف حقلًا واحدًا على الأقل قابلًا للتعبئة.',
    })
  }

  for (const { field } of fields) {
    if (field.choice && field.choice.options.length < 2) {
      suggestions.push({
        id: `few-options-${field.id}`,
        messageAr: `الحقل «${field.labelAr}» يحتوي على أقل من خيارين.`,
        actionAr: 'أضف خيارًا ثانيًا على الأقل حتى يكون الاختيار ذا معنى.',
      })
    }
  }

  return suggestions
}

type ValidationPanelProps = {
  schema: FormSchemaDocument
  issues: FormSchemaValidationIssue[]
  onNavigateToElement: (location: IssueLocation) => void
}

function IssueRow({ issue, tone, onNavigateToElement }: Readonly<{
  issue: ClassifiedIssue
  tone: 'error' | 'warn'
  onNavigateToElement: (location: IssueLocation) => void
}>) {
  const canNavigate = issue.location.pageId !== null
  return (
    <li className={tone === 'error' ? 'error' : 'warn'}>
      <div>{issue.messageAr}</div>
      <div className="muted">
        {issue.location.pageTitleAr ? `الصفحة: ${issue.location.pageTitleAr}` : null}
        {issue.location.sectionTitleAr ? ` — القسم: ${issue.location.sectionTitleAr}` : null}
        {issue.location.fieldLabelAr ? ` — الحقل: ${issue.location.fieldLabelAr}` : null}
      </div>
      <div className="muted">{issue.actionAr}</div>
      {canNavigate && (
        <button type="button" className="secondary" onClick={() => onNavigateToElement(issue.location)}>
          الانتقال إلى العنصر
        </button>
      )}
    </li>
  )
}

export function ValidationPanel({ schema, issues, onNavigateToElement }: Readonly<ValidationPanelProps>) {
  const { errors, warnings } = classifyIssues(schema, issues)
  const suggestions = computeSuggestions(schema)

  return (
    <div className="validation-panel" aria-label="لوحة التحقق">
      <div aria-live="polite" className="sr-only">
        {errors.length} أخطاء تمنع المراجعة، {warnings.length} تحذيرات، {suggestions.length} اقتراحات.
      </div>

      <h3 className="section-title">أخطاء تمنع المراجعة ({errors.length})</h3>
      {errors.length === 0 ? (
        <div className="muted">لا توجد أخطاء مانعة.</div>
      ) : (
        <ul>
          {errors.map((issue) => (
            <IssueRow key={validationIssueKey(issue)} issue={issue} tone="error" onNavigateToElement={onNavigateToElement} />
          ))}
        </ul>
      )}

      <h3 className="section-title">تحذيرات ({warnings.length})</h3>
      {warnings.length === 0 ? (
        <div className="muted">لا توجد تحذيرات.</div>
      ) : (
        <ul>
          {warnings.map((issue) => (
            <IssueRow key={validationIssueKey(issue)} issue={issue} tone="warn" onNavigateToElement={onNavigateToElement} />
          ))}
        </ul>
      )}

      <h3 className="section-title">اقتراحات ({suggestions.length})</h3>
      {suggestions.length === 0 ? (
        <div className="muted">لا توجد اقتراحات إضافية.</div>
      ) : (
        <ul>
          {suggestions.map((s) => (
            <li key={s.id} className="muted">
              <div>{s.messageAr}</div>
              <div className="muted">{s.actionAr}</div>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
