# Portable Files usability implementation plan

Approved scope: flatten context menu overflow; per-pane selected/total size, file and folder counts; configurable bottom command buttons and shortcuts (F3 view, F4 edit, F5 copy, F6 move, F7 folder, F8 delete, Alt+F4 close); split-pane copy/move from active pane to opposite pane; retain Korean and English only; persistent pinned/colored tabs; favorites after toolbar properties separator.

Constraints: no commits or pushes; preserve existing portable Data; registration-free x64 EXE; CRLF; existing storage/file operations and localization patterns; no parallel builds; build checks and focused UI verification per AGENTS.

- [x] Bottom command bar, per-pane statistics and editable settings, shortcut conflicts.
- [x] Persistent pinned tabs and colors, close/reorder behavior.
- [x] Toolbar favorites menu using existing folder favorites storage where possible.
- [x] Flatten overflow and keep only ko-KR/en-US app resources and supported runtime language payloads.
- [x] Merge localization additions; build and independent review completed. Runtime verification delegated to user.
- [x] Final Native AOT publish and clean deployment completed; Data hashes and every deployed file verified. User will launch EXE.

Rulings: F6 uses move in split pane and destination dialog in single pane; expose rename as separate configurable action. Size is selected/total immediate files (no expensive recursive folder scan). Localization edits coordinated by root from agent JSON dictionaries to avoid shared file writes. Implementers do not build or commit. Existing workspace has no git; preserve changes and use scoped backups rather than new checkout.

User update: runtime/UI verification will be performed by the user. Complete final build and clean deployment, preserving Data. No automated UI launch or smoke checks required for delivery.
