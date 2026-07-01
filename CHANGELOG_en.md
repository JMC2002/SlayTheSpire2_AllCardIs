# Changelog

All notable changes to this project will be documented in this file.

## [1.5.1] - 2026-7-1
### Added
- Test automated publishing workflow.

## [1.5.0] - 2026-6-19
### Changed
- Adapted to the official mod publishing format migration.

## [1.4.0] - 2026-5-26
### Added
- Target-card settings now support entering card names in the current game language, dynamically resolving names from the game's card database and localized titles instead of relying on a handwritten Chinese alias table.

### Changed
- Target-card parsing caches now invalidate when settings or language change; when duplicate card names are found, the mod no longer selects one automatically and instead prompts using `CARD.xxx` or a short ID.
- Naturally generated upgrade chances are now corrected based on the original generated card, so target cards inherit the natural upgrade chance the original card would have had.

### Fixed
- Fixed event cards created before their generic type was set not being replaced when added to the deck, such as `CARD.MAD_SCIENCE` from the Mad Science event.
- Fixed incomplete replacement coverage for generic cards, events, batch deck additions, transformations, clones, and similar paths.
- Fixed delayed replacement and startup deck cleanup not preserving upgrade level, floor added, and related state.

## [1.3.10] - 2026-5-8
### Added
- Integrated JML and officially released the general version.

### Fixed
- Fixed some events failing to transform cards.

## [1.2.2] - 2026-4-12
### Added
- Changed all cards into Claw.

## [1.2.1] - 2026-3-31
### Added
- Changed attacks and curses into Snake Bite to match the new video.

## [1.2.0] - 2026-3-28
### Added
- Changed cards into Jackpot to match the new video.

## [1.1.0] - 2026-3-18
### Fixed
- Updated the packaging logic to adapt to the 0.99.1 update.

## [1.0.0] - 2026-3-16
### Added
- Initial release: turns all cards, including starting cards and cards obtained later but excluding in-combat searches, into White Noise.
