# Security

Please report security problems privately through the contact form with the Security topic: https://www.kodiakcleaner.app/contact/?topic=Security (or Settings > Contact & Feedback > Security in the app). Don't open a public issue for them.

Include the version (from About), your macOS version, and the steps to repeat the problem. Please leave out personal data.

Kodiak Cleaner for Mac has no network features. Cleaning only removes files inside your home folder and your own temporary folder and never follows links. Administrator work (turning launch daemons off or on) goes through macOS's own password prompt with fixed `launchctl` commands only, after the name and path are checked (`Sources/KodiakCore/Triggers.swift`).
