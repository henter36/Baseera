import { appendNewPage } from '../../../forms/designer/studioSchemaOps'
import type { FormFieldType, FormPageSchema, FormSchemaDocument } from '../../../forms/designer/schemaTypes'
import { newField } from './studioWorkspaceHelpers'

type UseStudioFieldCommandsParams = {
  canEdit: boolean
  schema: FormSchemaDocument | undefined
  page: FormPageSchema | undefined
  selectedFieldId: string | null
  addField: (field: ReturnType<typeof newField>, page: FormPageSchema, section: FormPageSchema['sections'][number]) => string | undefined
  applySchema: (next: FormSchemaDocument) => void
  setSelectedFieldId: (id: string) => void
  setSelectedPageId: (id: string) => void
  recordRecentType: (type: FormFieldType) => void
}

/** Adding a field or a page each start with the same "not editable / nothing to act on" guard
 * clauses; extracted out of the studio page component so their branching doesn't count toward
 * its cognitive complexity. */
export function useStudioFieldCommands({
  canEdit,
  schema,
  page,
  selectedFieldId,
  addField,
  applySchema,
  setSelectedFieldId,
  setSelectedPageId,
  recordRecentType,
}: UseStudioFieldCommandsParams) {
  const handleAddField = (type: FormFieldType) => {
    if (!canEdit || !page) return
    const targetSection = page.sections.find((s) => s.fields.some((f) => f.id === selectedFieldId)) ?? page.sections[0]
    if (!targetSection) return
    const fieldId = addField(newField(type), page, targetSection)
    if (fieldId) setSelectedFieldId(fieldId)
    recordRecentType(type)
  }

  const handleAddPage = () => {
    if (!canEdit || !schema) return
    const { schema: next, pageId } = appendNewPage(schema)
    applySchema(next)
    setSelectedPageId(pageId)
  }

  return { handleAddField, handleAddPage }
}
