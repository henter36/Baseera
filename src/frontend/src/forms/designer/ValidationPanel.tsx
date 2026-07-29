import type { FormSchemaValidationIssue } from '../../api/client'
import { flattenFields } from './fieldDependencies'
import type { FormSchemaDocument } from './schemaTypes'

export type IssueLocation = {
  pageId: string | null
  pageTitleAr: string | null
  sectionId: string | null
  sectionTitleAr: string | null
  fieldId: string | null
  fieldLabelAr: string | null
}

export function locateIssue(schema: FormSchemaDocument, issue: FormSchemaValidationIssue): IssueLocation {
  if (issue.entityId) {
    for (const page of schema.pages) {
      if (page.id === issue.entityId) {
        return { pageId: page.id, pageTitleAr: page.titleAr, sectionId: null, sectionTitleAr: null, fieldId: null, fieldLabelAr: null }
      }
      for (const section of page.sections) {
        if (section.id === issue.entityId) {
          return { pageId: page.id, pageTitleAr: page.titleAr, sectionId: section.id, sectionTitleAr: section.titleAr, fieldId: null, fieldLabelAr: null }
        }
      }
    }
  }

  for (const { field, pageId, sectionId } of flattenFields(schema)) {
    if (field.id === issue.entityId || (issue.fieldKey && field.key.toLowerCase() === issue.fieldKey.toLowerCase())) {
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
  }

  return { pageId: null, pageTitleAr: null, sectionId: null, sectionTitleAr: null, fieldId: null, fieldLabelAr: null }
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
          {errors.map((issue, index) => (
            <IssueRow key={`error-${index}`} issue={issue} tone="error" onNavigateToElement={onNavigateToElement} />
          ))}
        </ul>
      )}

      <h3 className="section-title">تحذيرات ({warnings.length})</h3>
      {warnings.length === 0 ? (
        <div className="muted">لا توجد تحذيرات.</div>
      ) : (
        <ul>
          {warnings.map((issue, index) => (
            <IssueRow key={`warn-${index}`} issue={issue} tone="warn" onNavigateToElement={onNavigateToElement} />
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
