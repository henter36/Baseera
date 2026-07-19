import { NoteStatus } from './noteEnums'

// Mirrors Baseera.Application.Notes.NoteStateMachine / NoteAssignmentService transition rules,
// so the UI only ever offers actions the backend can actually perform. The backend remains the
// source of truth and re-validates every transition server-side.

export type NoteActionKind =
  | 'submit'
  | 'assign'
  | 'reassign'
  | 'startWork'
  | 'submitForVerification'
  | 'returnForRework'
  | 'verifyClosure'
  | 'reopen'
  | 'cancel'

export type NoteActionDef = {
  kind: NoteActionKind
  labelAr: string
  permission: string
  requiresReason: boolean
  requiresClosureSummary?: boolean
  isAssign?: boolean
}

export function getAllowedActions(status: number, hasCurrentAssignment: boolean): NoteActionDef[] {
  switch (status) {
    case NoteStatus.Draft:
      return [
        { kind: 'submit', labelAr: 'إرسال (فتح الملاحظة)', permission: 'Notes.Update', requiresReason: true },
        { kind: 'cancel', labelAr: 'إلغاء', permission: 'Notes.Cancel', requiresReason: true },
      ]
    case NoteStatus.Open:
      return [
        { kind: 'assign', labelAr: 'تكليف', permission: 'Notes.Assign', requiresReason: true, isAssign: true },
        { kind: 'cancel', labelAr: 'إلغاء', permission: 'Notes.Cancel', requiresReason: true },
      ]
    case NoteStatus.Assigned:
      return [
        { kind: 'startWork', labelAr: 'بدء العمل', permission: 'Notes.StartWork', requiresReason: false },
        { kind: 'reassign', labelAr: 'إعادة التكليف', permission: 'Notes.Assign', requiresReason: true, isAssign: true },
      ]
    case NoteStatus.InProgress:
      return [
        {
          kind: 'submitForVerification',
          labelAr: 'إرسال للتحقق',
          permission: 'Notes.SubmitForVerification',
          requiresReason: false,
        },
      ]
    case NoteStatus.PendingVerification:
      return [
        {
          kind: 'verifyClosure',
          labelAr: 'توثيق الإغلاق',
          permission: 'Notes.VerifyClosure',
          requiresReason: true,
          requiresClosureSummary: true,
        },
        {
          kind: 'returnForRework',
          labelAr: 'إرجاع لإعادة العمل',
          permission: 'Notes.ReturnForRework',
          requiresReason: true,
        },
      ]
    case NoteStatus.Closed:
      return [{ kind: 'reopen', labelAr: 'إعادة فتح', permission: 'Notes.Reopen', requiresReason: true }]
    case NoteStatus.Reopened: {
      const actions: NoteActionDef[] = [
        { kind: 'assign', labelAr: 'تكليف', permission: 'Notes.Assign', requiresReason: true, isAssign: true },
      ]
      if (hasCurrentAssignment) {
        actions.push({ kind: 'startWork', labelAr: 'بدء العمل', permission: 'Notes.StartWork', requiresReason: false })
      }
      return actions
    }
    default:
      return []
  }
}
