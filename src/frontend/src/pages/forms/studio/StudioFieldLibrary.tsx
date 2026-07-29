import { useState } from 'react'
import { FIELD_LIBRARY_CATEGORIES, searchFieldLibrary } from '../../../forms/designer/fieldLibrary'
import { FormFieldTypeLabelsAr, type FormFieldType } from '../../../forms/designer/schemaTypes'

type StudioFieldLibraryProps = {
  onAddField: (type: FormFieldType) => void
  recentTypes: FormFieldType[]
}

export function StudioFieldLibrary({ onAddField, recentTypes }: Readonly<StudioFieldLibraryProps>) {
  const [query, setQuery] = useState('')
  const results = searchFieldLibrary(query)
  const showCategories = query.trim().length === 0

  return (
    <div className="studio-field-library" aria-label="مكتبة الحقول">
      <input
        type="search"
        className="studio-field-library-search"
        aria-label="بحث في مكتبة الحقول"
        placeholder="ابحث عن نوع حقل…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />

      {showCategories && recentTypes.length > 0 && (
        <div className="studio-field-library-category">
          <h4>مستخدمة حديثًا</h4>
          <div className="studio-field-library-grid">
            {recentTypes.map((type, index) => (
              <button key={`recent-${type}-${index}`} type="button" className="secondary" onClick={() => onAddField(type)}>
                + {FormFieldTypeLabelsAr[type]}
              </button>
            ))}
          </div>
        </div>
      )}

      {showCategories ? (
        FIELD_LIBRARY_CATEGORIES.map((category) => (
          <div className="studio-field-library-category" key={category.key}>
            <h4>{category.labelAr}</h4>
            <div className="studio-field-library-grid">
              {category.types.map((type) => (
                <button key={type} type="button" className="secondary" onClick={() => onAddField(type)}>
                  + {FormFieldTypeLabelsAr[type]}
                </button>
              ))}
            </div>
          </div>
        ))
      ) : (
        <div className="studio-field-library-grid">
          {results.length === 0 ? (
            <div className="empty">لا توجد أنواع حقول مطابقة.</div>
          ) : (
            results.map((entry) => (
              <button key={entry.type} type="button" className="secondary" onClick={() => onAddField(entry.type)}>
                + {entry.labelAr}
              </button>
            ))
          )}
        </div>
      )}
    </div>
  )
}
