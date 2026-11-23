import Lean

-- Int represents the widened intermediates. A checked operation either returns
-- an integer in the Int32 range or observes OverflowException as none.
abbrev FitsInt32 (x : Int) : Prop :=
  -2147483648 ≤ x ∧ x ≤ 2147483647

def checkedInt32 (x : Int) : Option Int :=
  if FitsInt32 x then some x else none

def checkedTransfer (a b k : Int) : Option Int :=
  match checkedInt32 (a + k) with
  | none => none
  | some left =>
    match checkedInt32 (b - k) with
    | none => none
    | some right => checkedInt32 (left + right)

theorem checked_transfer_under_guard (a b k : Int)
    (leftFits : FitsInt32 (a + k)) (rightFits : FitsInt32 (b - k)) :
    checkedTransfer a b k = checkedInt32 (a + b) := by
  have left : checkedInt32 (a + k) = some (a + k) := by
    simp only [checkedInt32, if_pos leftFits]
  have right : checkedInt32 (b - k) = some (b - k) := by
    simp only [checkedInt32, if_pos rightFits]
  have sum : (a + k) + (b - k) = a + b := by omega
  simp only [checkedTransfer, left, right, sum]

def guardedTransfer (a b k : Int) : Option Int :=
  if FitsInt32 (a + k) ∧ FitsInt32 (b - k) then checkedTransfer a b k else checkedInt32 (a + b)

theorem guarded_transfer_sound (a b k : Int) :
    guardedTransfer a b k = checkedInt32 (a + b) := by
  unfold guardedTransfer
  split
  · rename_i guard
    exact checked_transfer_under_guard a b k guard.1 guard.2
  · rfl

theorem widened_intermediates_fit_int64 (a b k : Int)
    (aFits : FitsInt32 a) (bFits : FitsInt32 b) (kFits : FitsInt32 k) :
    (-9223372036854775808 ≤ a + k ∧ a + k ≤ 9223372036854775807) ∧
    (-9223372036854775808 ≤ b - k ∧ b - k ≤ 9223372036854775807) := by
  unfold FitsInt32 at aFits bFits kFits
  omega

#check checked_transfer_under_guard
#check guarded_transfer_sound
#check widened_intermediates_fit_int64
#print axioms guarded_transfer_sound
#print axioms widened_intermediates_fit_int64
