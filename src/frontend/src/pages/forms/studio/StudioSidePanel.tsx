import type { ReactNode } from 'react'
import { StudioFieldLibrary } from './StudioFieldLibrary'
import { StudioOutline } from './StudioOutline'
import type { FormFieldType, FormSchemaDocument } from '../../../forms/designer/schemaTypes'

type StudioSidePanelTab = 'library' | 'outline'

function resolveSidePanelContent(params: {
  rightTab: StudioSidePanelTab
  canEdit: boolean
  onAddField: (type: FormFieldType) => void
  recentTypes: FormFieldType[]
  schema: FormSchemaDocument
  selectedFieldId: string | null
  errorCountByPageId: (pageId: string) => number
  onSelectField: (pageId: string, fieldId: string) => void
}): ReactNode {
  const { rightTab, canEdit, onAddField, recentTypes, schema, selectedFieldId, errorCountByPageId, onSelectField } = params

  if (rightTab === 'outline') {
    return <StudioOutline schema={schema} selectedFieldId={selectedFieldId} errorCountByPageId={errorCountByPageId} onSelectField={onSelectField} />
  }

  if (!canEdit) {
    return <div className="muted">هذا الإصدار للقراءة فقط ولا يمكن إضافة حقول جديدة إليه.</div>
  }

  return <StudioFieldLibrary onAddField={onAddField} recentTypes={recentTypes} />
}

type StudioSidePanelProps = {
  rightTab: StudioSidePanelTab
  onChangeTab: (tab: StudioSidePanelTab) => void
  canEdit: boolean
  onAddField: (type: FormFieldType) => void
  recentTypes: FormFieldType[]
  schema: FormSchemaDocument
  selectedFieldId: string | null
  errorCountByPageId: (pageId: string) => number
  onSelectField: (pageId: string, fieldId: string) => void
  panelOpen?: boolean
}

export function StudioSidePanel({
  rightTab,
  onChangeTab,
  canEdit,
  onAddField,
  recentTypes,
  schema,
  selectedFieldId,
  errorCountByPageId,
  onSelectField,
  panelOpen,
}: Readonly<StudioSidePanelProps>) {
  return (
    <div className="studio-side" data-panel-open={panelOpen}>
      <div className="studio-side-tabs" role="tablist">
        <button type="button" role="tab" aria-selected={rightTab === 'library'} className={rightTab === 'library' ? undefined : 'secondary'} onClick={() => onChangeTab('library')}>
          مكتبة الحقول
        </button>
        <button type="button" role="tab" aria-selected={rightTab === 'outline'} className={rightTab === 'outline' ? undefined : 'secondary'} onClick={() => onChangeTab('outline')}>
          مخطط النموذج
        </button>
      </div>
      {resolveSidePanelContent({ rightTab, canEdit, onAddField, recentTypes, schema, selectedFieldId, errorCountByPageId, onSelectField })}
    </div>
  )
}
