# Changelog

All notable changes to EousGate are documented here.

## [0.1.0-beta.2] - 2026-09-27

### Fixed

- Selecting Windows Photos now resolves its actual app identity and opens files through its Shell association handler. Localized names and icon resources are no longer treated as app identities.
- Removed the Explorer fallback that incorrectly reported successful file opening.
- Packaged apps receive a native Shell file selection; multi-file calls check whether the selected handler supports the complete selection.
- Added regression coverage for localized names, AppX registration, complete file delivery, and failure handling.

### Validation

- 49 Release tests passed. Single JPG and mixed JPG/PNG opened successfully in Photos 2026.11080.24002.0 on the development machine.
- Full mouse-drag replay, other Windows/Photos versions, clean-machine startup, DPI, and high-contrast scenarios still need broader field testing.

## [0.1.0-beta.1] - 2026-09-24

### Added

- Edge-triggered app picker for the left, right, and top edges of the primary display.
- Single-file and multi-file drag sessions, including mixed file types with common-app filtering.
- Windows desktop and packaged-app discovery, custom per-extension apps, ordering, and hiding.
- Light, dark, and high-contrast themes with configurable panel appearance and edge regions.
- Privacy-preserving local diagnostics that are disabled by default.

### Known limitations

- Windows 11 x64 and the primary display are the supported environment for this beta.
- Folders are not supported.
- The binaries are not digitally signed and may trigger a Microsoft Defender SmartScreen warning.
- Microsoft Photos multi-file activation, clean-machine startup, DPI, and high-contrast scenarios still need broader field testing.
