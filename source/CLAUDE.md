# Kodiak Cleaner: Project Rules

Kodiak Cleaner is Ted Tabaka's Windows cleaner: C# .NET 8 WPF app (`src/KodiakCleaner.App`), logic in
`src/KodiakCleaner.Core`, xunit tests in `tests/`, WiX v5 MSI in `installer/`. It follows Ted's app standard:
privacy first, local first, security paramount, convenient, practical and useful to everyone.

## Privacy and Safety
- No telemetry, analytics, crash reporting, ads, upsells or background services. Nothing is sent automatically.
- The only network use is the update check on the website (`UpdateManifest`, https://www.kodiakcleaner.app/updates/<channel>.json, since 0.8.40
  because the GitHub repo went private; Ted), off by default and only on request. Any new online feature
  must be off until turned on, explained where it's turned on, and added to PRIVACY.md and store-assets/privacy-policy.html.
- No remote resources (CDNs, web fonts). Fonts are bundled (`Assets/Fonts`) or come from Windows.
- Settings go in `KodiakCleaner.ini` next to the exe. Backups and logs go in `Settings.Data` (`DataFolders`, Ted 0.8.36):
  `Documents\Kodiak Cleaner` for an installed copy (AppData\Local\Kodiak Cleaner only if Documents can't be written), beside
  the exe for a portable copy, with `Backups\{Drivers, Event Logs, Mass Rename, Programs History, Registry, Scheduled Tasks, Settings, Startup History}`, `Logs\` and `Reports\` (System Information exports, Ted 0.8.46).
  Nothing else in AppData. Never write user data into Program Files (only KodiakCleaner.ini is writable there).
- `ActivityLog` writes one plain-text file per month in Logs (12 kept): counts, sizes and item names only, never file
  contents; Settings > Application > Keep an Activity Log turns it off. Log every user-visible change it makes.
- Uninstall leaves nothing behind (Ted, 0.8.36): the MSI runs `KodiakCleaner.exe --uninstall-user` (as the user: HKCU
  entries, update temp folder, asks whether to delete Documents\Kodiak Cleaner) and `--uninstall-system` (elevated: the
  Task Scheduler folder), only on REMOVE=ALL and never on upgrade (0.8.47: the exe also skips it while two products with our
  upgrade code are registered, `UninstallCleanup.IsUpgrading`, since Wine runs it mid-upgrade). Anything new the app registers with Windows must be
  removed there too. Cleanup must never show an error or block the uninstall.
- Every registry change is backed up to a `.reg` file first. Removed files go to the Recycle Bin where possible.

## Interface
- Colors only from the Honey (default), Classic, Glacier and Lagoon theme dictionaries in `Themes/` and the accent shades
  (`Core/Appearance/AccentShades.cs`, used by `AccentColors.cs`); no colors anywhere else (a test enforces this). Only colors, fonts and corner
  radii differ between themes. The theme colors match the standard's `tools/design/theme.css` exactly (a test checks; run
  `python3 tools/design/check_contrast.py tools/design/theme.css` after any color change: WCAG 2.2 AA). Honey is free; Classic, Glacier and
  Lagoon are Supporter themes (standard, 0.8.31; Lagoon 0.8.74) and fall back to Honey without a code. Accent colors (theme's own, Windows, Blue, Teal, Green,
  Purple, Pink, Orange, Slate) are free and adjusted per theme and mode; swatches are 28px with a ring and a checkmark when chosen.
  The scheme's own accent is always the first swatch (Ted, 0.8.32); a built-in swatch of the same color (`AccentShades.SchemeSwatch`:
  Classic = Blue) is hidden in that scheme and a saved choice of it means the scheme's own accent.
- About is its own sidebar entry under Settings (Ted's choice), not a page inside the Settings sidebar.
- Settings sidebar (2026-10-23 review): pages A to Z under each heading ("", Personalize, Protect, Help, Developer); Settings opens on General.
  Help has FAQ (`faq.md`, questions A to Z): keep its answers true to the code whenever a feature changes. The Developer page is titled
  Developer Tools. A test (`StaysCurrentTests`) checks the order, that Getting Started lists every sidebar page, and that every
  Backups folder is a `DataFolders.BackupKinds` entry named in both privacy policies (never `Path.Combine(Settings.Data.Backups, "...")`).
  Check for Updates is a large button on About right below the version card (Ted, 0.8.41; on the right with the text beside it, 0.8.42), and also stays under Settings > Updates;
  the automatic-updates switch is only in Settings.
- Slogan (`AppInfo.Slogan`, Ted's exact words): "Not a Crap Cleaner. A Cleaner App, Built Around What Matters."
- `why-it-was-made.md` is Ted's own story. Change its wording only when Ted asks (0.8.4: light edit, no em dashes, "Kodiak Cleaner"
  wherever it said Cleaner; 0.8.13: light wording edit at Ted's invitation, his voice and first person kept; 0.8.25: opens "For well over two decades", Ted's words; 0.8.57: replaced word for word with Ted's new text, heading "Built Around What Matters.", ending with a donation ask); it shows under Settings > Help > Why It Was Made, in larger text (17, Ted, 0.8.24).
- Tools must stay local and reversible: read-only, or removals go to the Recycle Bin after a confirm. Browser
  Extensions only lists; removal happens in the browser. Empty Folders never searches Windows, program folders or any user's AppData (0.8.48), as a starting point or inside one (`EmptyFolderFinder.Find`/`IsStillEmpty` take the protected list), and never lists Desktop, Documents and the other Windows user folders (the kept list; searched inside). Large Files skips Windows, program folders and hidden/system files.
  Scheduled Tasks turns tasks on or off and hides Windows tasks unless asked. Delete (Ted, 2026-10-23) always saves the task's XML to
  Backups\Scheduled Tasks first (`ScheduledTaskBackups`, index.json keeps each file's task path) and skips a task whose backup fails;
  Restore re-registers it in its old folder (backing up and replacing a task of the same name). Password-logon tasks can't be restored
  from here (the password isn't in the XML); the message points to Task Scheduler's Import Task. Back Up, Delete and Restore warn
  first when a picked task is password-protected (`ScheduledTaskTools.PasswordCaution`, Ted 2026-10-23).
- Drive Wiper (2026-10-12): `DriveMedia` reads each disk's type from the storage stack (NVMe bus, seek penalty, USB/SD bus; no WMI).
  Entire Drive works on whole physical disks (`DriveWiper.ListDisks`/`EraseAsync`): a disk is refused when any letter on it is protected
  (`ProtectedReason`: Windows, Kodiak Cleaner's drive, page file, user folder, programs), when it holds the start-up partition
  (HKLM\SYSTEM\Setup SystemPartition) or a volume spanning disks. Every volume on it is locked and dismounted, then hard drives and flash
  drives get `Overwriter` passes (1 Pass (Zeros), DoD 5220.22-M 3, ECE 7; last pass read back) and SSD/NVMe get IOCTL_STORAGE_REINITIALIZE_MEDIA
  (crypto, then block, then default; random overwrite if refused). Wipe Type (Ted, 2026-10-12): Quick = partition data only, Thorough =
  zeros (firmware erase on SSD/NVMe), Secure = the Security choice incl. 30 Passes (DoD pattern x10, labelled "not a standard").
  diskpart clean always removes MBR/GPT and boot sectors; Leave It Uninitialized Like a New Drive stops there, otherwise
  `DiskFormat` (GPT/MBR, NTFS/exFAT/FAT32, label; `FormatProblem` refuses MBR over 2 TB and FAT32 over 32 GB). Three checks:
  summary Warn, type each drive's disk number, type ERASE.
  Never test it against a real drive (Wine included). Free space wiping uses the same DoD patterns.
- Defrag and TRIM (2026-10-12, `DriveOptimizer`): Windows' own defrag.exe only; hard drives /D (NTFS/FAT), SSD and NVMe /L, unknown /O, flash
  drives Not Needed. Analyze (/A) only for hard drives. One drive at a time; needs administrator rights.
- Every setting is a toggle switch; checkmarks only for batch selections (pick items, then act). One-of-several
  choices (color scheme, accent color, light or dark) use tiles, swatches or segmented buttons.
- Title Case (Ted's rule, stricter than the standard) for page names, card titles, headings, field labels, column
  headers, buttons, switches and option labels. Sentences (help text, captions, hints) stay sentence case. Use the
  `PageTitle`, `SectionTitle` and `SettingTitle` styles; the test also checks every Button/CheckBox/RadioButton label and (0.8.24)
  labels set in code (prompts, folder pickers, Sort By, table values like All Users). Getting Started's Features list stays A to Z and current.
- Main sidebar: a Cleaning heading over Cleaner/Registry/Startup/Programs, and Tools open by default (Ted, 0.8.11; folding is remembered as `ToolsOpen`).
  Bottom right of the sidebar: the paperclip (on its side = locked at the default 310 width, upright = drag the edge, 240 to 480)
  above the collapse button (icons only, 84 wide, bigger centered icons; every nav entry has its name as a tooltip).
  A divider with space above it separates Settings and About from the scrolling list (Ted, 0.8.29). The standard-user bar uses AccentTint, Line and Text, not Warn colors.
  Width, lock and collapse are saved in the ini. Settings > General > Sidebar: Adjustable Sidebar Width and Show the Paperclip (0.8.12).
- Refer to the app by name ("Kodiak Cleaner", `AppInfo.Name`), never "the app". Since 0.8.7 the shown name never
  includes the channel (Ted's choice); the channel appears in the version ("0.8.7 Dev") and the About chip. Plain hyphens in names.
- Never mention Ted's other apps. Logo (0.8.5, designed at Ted's request, approved by Ted 2026-10-04): a computer with bear ears,
  a bear face on the screen and a sparkle. Source `Assets/logo.svg`; `app.ico`, `logo.png` and the installer's
  `WixUIDialog.bmp`/`WixUIBanner.bmp` are rendered from it. Don't redesign it without Ted. Named PCSweep until 0.8.2.
- The MSI installs every channel to `Program Files\Kodiak Cleaner` (Ted's choice) with one shared upgrade code, so
  channels replace each other rather than sitting side by side.
- Ted's name in the app and docs is "Ted Tabaka" (copyright holder too), never his full name.
- Tables: alternating rows (`RowAlt`), `RowHover`, `RowLine` and a draggable `ColumnDivider` in the header only, all theme
  tokens. Batch actions use a fixed 48px checkbox column (`CanUserResize=False`, `CheckColumnHeader`, no divider; header box
  selects all) plus buttons above the table and a right-click menu; switches go at the right end.
- Settings pages are titled cards of related switches. The logo dances on hover/click (sidebar and Getting Started, `LogoDance`) unless animations are off: the Animations switch (Settings > Appearance > Motion) and Windows animation effects both gate `Motion.Enabled`.
- Uninstall (Ted, 0.8.14, `UninstallFlow`): scan, back up the uninstall entry and found keys (`Before_Uninstall`), run the uninstaller, and only
  when the entry is gone rescan and show `LeftoversWindow` (all ticked; folders/shortcuts to the Recycle Bin, keys and startup entries backed up).
  `LeftoverNames`/`LeftoverFinder` match exact names only, never generic names, protected folders, roots or other programs' folders.
- Programs (0.8.15): program icons (`ProgramIcons`: DisplayIcon, Installer ProductIcon, main exe), tick boxes + Select All; batch uninstall
  runs the full flow per program strictly one at a time, waiting for helper uninstallers (Temp, Un/Au_/_iu/unins) to close.
- Multi-select (Ted, 0.8.49; every list 0.8.90): the shared DataGrid style is `SelectionMode="Extended"` and switches on `Views/ListSelect`
  for every table and ItemsControl (0.8.92: a drag starts anywhere on the list's Card border and always shows an accent outline;
  tables highlight the covered rows by item index, so it scrolls and survives virtualization; tick-box lists tick the covered rows,
  highlight whole rows on hover and when ticked via an adorner, and a click anywhere on a row ticks it; Space ticks highlighted rows). Only `ListSelectTests.SingleRowTables` may say `SelectionMode="Single"`; a table or list never gets
  its own Style (it would lose ListSelect). Tables with a right-click menu pass their highlighted rows to the view model's `Highlighted`. Right-click actions act on every highlighted row (`GridRightClick` keeps a
  selection when the clicked row is in it); the buttons act on ticked rows, else the highlighted ones. Repair and Rename need exactly one row.
- Icons (0.8.16): only from files already on the PC (`ProgramIcons`, `FileIcons`: exe icons via ExtractIconEx, others via SHGetFileInfo), never bundled brand logos;
  a plain Segoe glyph when nothing is found. Cleaner groups take the icon from `CleanTarget.AppFiles`, then App Paths.
- Cleaner Advanced Cleaning (Ted, 0.8.16): `CleanTarget.Advanced` items sit in one group, hidden behind the Advanced Cleaning button, never ticked
  by default, unticked again when hidden. After an analysis they are measured quietly and suggested only per `AdvancedAdvice` (1 GB, or 200 MB on a nearly full drive).
- Startup (Ted, 0.8.17): Add makes a .lnk in the Startup folder (`ShellLink`; a picked shortcut is moved unless Move the Shortcut is off).
  `StartupHistory` (Backups\Startup History\history.json + shortcut copies) is updated whenever the list is read, never in the background;
  every removal (Startup page or Uninstall leftovers) is backed up and marked; History restores removed entries. Back Up Settings copies it too.
- Programs History (Ted, 0.8.22): `ProgramHistoryStore` (Backups\Programs History\history.json) records every Delete Entry, Rename, uninstall
  and removed leftover. Restore uses the .reg backup (reg.exe import), the Recycle Bin, or the Startup history; uninstalls are record only.
  Every new removal on the Programs page must be recorded there. Back Up Settings copies it.
- Advanced Cleaning shows a warning (Yes/No) every time it is opened (Ted, 0.8.17).
- Driver Cleaner (Ted, 2026-10-12, `DriverCleaner`/`InfReader` in Core, `DriverTools` in the app): picks a maker (AMD, Intel, NVIDIA,
  Realtek Audio) and lists its driver store packages (read from C:\Windows\INF\oem*.inf, only the Display, Sound, Software Component,
  Extension and, for Realtek, Audio Effects classes), leftover folders and registry keys. Display drivers and parts whose device names
  belong to the driver are ticked; other parts from the same maker (chipset, storage, wireless...) are listed unticked. Removal: restore
  point (not in Safe Mode), `pnputil /export-driver` to Backups\Drivers (a package whose backup fails is kept), `pnputil /delete-driver
  /uninstall /force` (done only when its oemNN.inf is gone or pnputil says 3010), keys backed up then deleted, folders to the Recycle Bin,
  then Later / Shut Down / Restart and Get the Latest Driver (the maker's own page, opened only on click). Restart in Safe Mode uses
  `bcdedit /set {current} safeboot minimal` plus a `*` RunOnce entry that opens `--page=drivers`; every restart from Safe Mode, and
  Later, clears safeboot (and safebootalternateshell) from {current} and {default} and reads it back with `bcdedit /enum`; if it's still set or
  can't be read (no admin), it doesn't restart and shows the msconfig steps with an Open System Configuration button (Ted, 2026-10-23); `--uninstall-system` drops the RunOnce entry. Let Windows Update Install Drivers is the live `DriverSearching\SearchOrderConfig` value, backed up first.
  BitLocker (Ted, 2026-10-23, `BitLockerTools`): state of the Windows drive from WMI Win32_EncryptableVolume (admin only; Unknown
  hides the card). Restart in Safe Mode with BitLocker on asks Cancel / Restart Without Suspending / Suspend and Restart; suspend is
  DisableKeyProtectors with DisableCount 2 (into Safe Mode and back), never full decryption: turning it off is left to Windows' own page
  (control /name Microsoft.BitLockerDriveEncryption, or ms-settings:deviceencryption on Home). Show Recovery Key reads the numerical
  passwords and only shows, copies or saves them (warns when saving to the Windows drive); never log or store a key.
  Never name or reference any third-party driver removal tool in the app, docs or replies.
- Advanced Cleaning tasks (0.8.18, `CleanTargetKind.Task`, `SystemTasks`): only Windows' own tools from System32 (DISM, powercfg, compact,
  vssadmin, wevtutil, netsh, cleanmgr, DnsFlushResolverCache); registry changes backed up first; event logs exported and checked before clearing;
  shadow copies keep the newest. `CleanTarget.Warning` items: caution text, confirm on tick, skipped by Select All and by AutoClean.
  Never add Windows Installer cache, Downloads or the Security event log, and never delete Defender or File History files directly.
- Disk Cleanup items (Ted, 0.8.61, `task.dc.*`): items whose files only Windows may judge (Defender, Diagnostic Data Viewer, Feedback Hub,
  Language Resource Files, RetailDemo, Temporary Setup Files, Temporary Windows Installation Files, User File History) run
  `cleanmgr /sagerun:617` with a temporary StateFlags0617 tick on that one VolumeCaches handler (found by its shown name, then key
  name), removed again afterwards. Disk Cleanup items Kodiak Cleaner already had are merged into the existing items, not duplicated.
- More cleaning items (Ted, 2026-10-12, from his two screenshot lists): browser Metrics Temp Files (ticked), Session, Bookmarks Backup,
  Firefox Crash Reports/Site Preferences/Saved Form Information; passwords and Chromium's Web Data (it also holds cards and search engines)
  only under Advanced Cleaning with a warning. Programs (AMD, AnyDesk, Discord, FileZilla, OneDrive, Node.js, NVIDIA, Steam, Telegram,
  VLC, WhatsApp, Zoom) are listed only when their folder exists and only offer caches, logs and installer leftovers. Broken Start Menu/
  Desktop shortcuts use `ShellLink.IsBroken` (fixed, ready drives only). Downloads items (by kind, top level only, `CleanRule.Filter`)
  and Jump Lists, Folder views (shellbags), Environment Path and Wipe Free Space (cipher /w) ask first. Registry history lists
  (`SystemTasks.RegistryLists`) are backed up to .reg before clearing. Left out on purpose: databases compaction, last download
  location, saved credentials, tray icon cache (Explorer rewrites it), network data usage (locked by Windows), Sticky Notes (would
  delete notes), Notepad++ backups (unsaved work), store-app items (already in Temporary Files (Store Apps)), Custom Files and Folders.
- Browser extension control (Ted, 0.8.65): Core/Tools/ExtensionControl.cs. Chrome/Edge/Brave use HKCU\Software\Policies\<vendor>
  ExtensionInstallBlocklist (off) and ExtensionSettings installation_mode "removed" (remove); never edit Preferences/Secure
  Preferences (MAC-protected). Settings [Extensions] Removed lists our remove marks; Tidy clears them once the browser uninstalled
  the extension and deletes empty policy keys. Firefox family: delete profile\extensions\id.xpi with the browser closed.
- Registry keys owned by TrustedInstaller are filtered from scans and never deleted (RegistryScanner.ProtectedByWindows); we never
  take ownership. Empty Folders searches drives via SearchPlaces/PlacesPicker (reusable drive + folder picker); hidden folders
  and drive-root Windows folders are skipped. Scheduled Tasks read each task's first action (exec path, COM handler).
- Launch count (0.8.64): Donation.CountLaunch is called once in App startup; LaunchCounter.CountsAsStart skips self-reopened
  copies (--restarted, --updated). The donation pop-up shows after Getting Started/What's New, from start 1 (a test checks).
- Hash Checker (Ted, 2026-10-23, `HashChecker` in Core, `HashCheckerViewModel`): its own tool, not part of Integrity Check (that one checks
  Windows itself). Every file is read once for all five kinds (MD5, SHA-1, SHA-256, SHA-384, SHA-512, one core each), shared read so open
  files work; a kind Windows refuses (FIPS) is left out. `ParseList` reads sha256sum/md5sum, BSD "SHA256 (name) = hash", certutil,
  PowerShell Get-FileHash (table and list) and bare hashes; the hash length decides the kind. A file the list names must match that entry
  (else Doesn't Match); otherwise any equal hash verifies it. Dropped checksum files (`IsListFile`) become the list and add the files they
  name from the list's folder; folders add at most 5000 files. Read-only: only Save Checksum File writes (where the person picks). Ctrl+F
  (MainWindow `FindShortcut`) focuses the first search box shown on any page.
- Integrity Check (Ted, 2026-10-12, `IntegrityCheck` in Core, `IntegrityCheckViewModel`): cards A to Z, each with Check (read-only)
  and, where Windows can fix it, Repair. Only Windows' own tools from System32: sfc /verifyonly and /scannow (UTF-16 output, English
  messages only, else "Finished, see Details"), DISM /Online /Cleanup-Image /ScanHealth and /RestoreHealth with /English, chkdsk (NTFS /scan
  to check, FAT/exFAT read-only; repair /f or /r, plus /x except on the Windows drive, which is scheduled: answered Y on stdin and
  confirmed from Session Manager BootExecute), mdsched.exe, wevtutil for the System log (drive providers, Level 1-3, 30 days; memory
  results 1201/1202) and WMI MSFT_PhysicalDisk/MSFT_StorageReliabilityCounter for drive health. Repairs ask first and can't be stopped
  (Stop only during checks). Each run's output goes to Reports\Integrity Check and the activity log. DISM repair may use Windows Update
  (stated in PRIVACY.md); nothing else goes online.
- Boot Timer (Ted, 2026-10-12, `BootTimer` in Core, `BootProbe`, `BootTimerViewModel`): Restart and Time saves the click time in
  `Reports\Boot Timer\boot-timer.json`, adds a one-time HKCU RunOnce value ("Kodiak Cleaner Boot Timer", `--page=boottimer`; removed again
  after the report, on Cancel Timing and by `--uninstall-user`) and runs `shutdown /r /t 0`. After sign-in it waits for the processor to stay
  under 15% for 5 s (at most 4 min after sign-in), then records startup programs' processor time and disk use (Task Manager's impact
  thresholds) and, as administrator, Windows' Diagnostics-Performance events 100 to 110 via wevtutil. Only Windows' own information, nothing
  installed; the report table is slowest first (a stated priority, not A to Z). Turn Off at Startup only disables (reversible).
- Settings cards A to Z (Ted, 0.8.63): every Settings card, and every setting inside a card, is A to Z (a test checks); Reset to
  Default stays the last card in Advanced. Row hover details (0.8.63): Registry, Startup and Programs rows use the DetailsRow style,
  whose tooltip is the item's Details (DetailText.Lines).
- A to Z rule (Ted, 0.8.21): anything added to any list (Cleaner items, tools, menus, options, browsers) goes in A to Z order, unless a
  list has a stated priority (Windows first in the Cleaner; Advanced Cleaning last).
- Cleaner order (Ted, 0.8.20): `CleanCatalog.Order` sorts Windows first, then programs A to Z, Advanced Cleaning last, items A to Z
  in every group (a test checks it). Add new entries anywhere; never hand-order them. Reset to Default ticks only `EnabledByDefault` basic items.
  Sort By: Default (Ted, 0.8.23; the standing order, selected out of the box; 0.8.26: ticked items first in each group, A to Z,
  re-sorted on load, Reset to Default and every click on Default, never on each tick), A to Z (plain, Windows not pinned), Last Cleaned, Most Space Freed, Largest Found (`CleanSort`, saved as `[Cleaner] SortBy`); `CleanerHistory` in
  `[Cleaner History]` records every real clean (Cleaner page, AutoClean, schedules).
- Scheduled Cleaning (Ted, 0.8.21): `CleaningSchedule` in `[Schedule id]` sections, each with a Task Scheduler task `<AppInfo.Id>\Cleaning <id>`
  that runs `KodiakCleaner.exe /schedule:<id>` (no window; an open copy runs it instead). No background service. Caution items can't be
  scheduled; Run as Administrator only for an administrator on the installed copy. Settings > Reset keeps schedules and history.
- Startup speed (Ted, 0.8.33): only the Cleaner page loads at startup. Other pages read their lists on first visit through
  `ILoadOnFirstVisit.EnsureLoaded` (MainViewModel.OnNavigated); never call Refresh in a page view model's constructor. Grouped tables
  keep `VirtualizingPanel.IsVirtualizingWhenGrouping="True"`. Uncompressed or ReadyToRun single-file builds don't start under Wine.
  First paint comes first (0.8.47, Ted's "not responding after an update" report): pop-ups (Beta notice, What's New, Getting Started)
  and the update check run on the main window's first ContentRendered; `Integration.SyncAll` runs at ApplicationIdle. Starts of 5 s
  or more are logged. The copy reopened after an update gets `--updated` and brings itself to the front (Topmost flip).
- System Information (Ted, 0.8.46) is in the bottom sidebar group (Settings, System Information, About), not Tools. Summary | Advanced segments;
  Advanced (`SystemDetails`, read in the background on first open) adds drive/memory/PC serials, per-adapter MAC, IP, gateway, DNS and DHCP,
  memory type/speed/slots, CPU cores/threads/cache, BIOS/UEFI/Secure Boot/TPM, GPU memory/driver, audio, a few Windows settings and temperatures
  where Windows shares them; groups A to Z. Local only (no public IP lookup). Left out on purpose: the product key, nearby Wi-Fi networks,
  per-core and SMART tables; CAS/cache latency needs a driver, so it isn't shown. Export Report saves Summary + Advanced to `Settings.Data.Reports`
  as Text, HTML (self-contained, current theme colors passed in as `ReportStyle`, no scripts) or CSV (formula cells made text), chosen in a
  dropdown remembered as `[System Information] ReportFormat` (default HTML).
  Loading (Ted, 0.8.51): nothing is read on the main thread. Summary reads the basics in the background and every drive at once,
  each given 5 s (`DriveWait`) before it's listed as not answering (a sleeping disk or lost network drive used to freeze the page);
  Advanced reports progress per part (`SystemDetails.Collect(progress, ct)`) and logs parts taking 5 s or more. The list area shows a
  progress panel with Cancel while reading and a plain "Reading Stopped" panel after Cancel; the page can be left and returned to.
- Single instance (0.8.48): the hand-off pipe allows this person's account (`WindowsIdentity.User`), never `PipeOptions.CurrentUserOnly`, which only lets in the token owner (Administrators for an elevated copy) and so dropped every normal launch's hand-off.
- Disk Analyzer (Ted, 2026-10-12): `DiskAnalyzerPickWindow` picks All Drives (local only), Individual Drives (ticked, network too) or Folder from a
  dropdown (Ted, 2026-10-22; fixed 760x660 window, the drive cards scroll, Select All/Clear); drives are read in the background with 5 s each. `DiskAnalyzer.Scan` keeps folders only (paths worked out from the parent, per-kind byte totals), reads
  drives and top folders side by side, and the open folder's files are read again on opening (`FilesIn`). `TreemapView` (squarified,
  `Treemap.Layout`, up to 3 levels) and `KindBarView` color by `FileKind` with `DiskColors`, which follow the scheme and accent (Ted, 2026-10-12):
  the current Accent's hue turned per kind (`AccentShades.ChartShade`), lightness fitted so OnPrimary reads on it (4.5:1). Removing a file updates every folder above it (`DiskNode.FileRemoved`).
  Fast scan (Ted, 2026-10-13, `NtfsFileTable`): a whole local NTFS drive, as administrator, is read from its master file table through a
  read-only `\\.\X:` handle (whole-cluster reads); records 0-15, reparse points and the short 8.3 names are left out, hard links count once.
  Anything unexpected returns false and `ScanTop` walks the folders instead, so the fast scan must never fail a scan. Tests use the two
  mkntfs/ntfs-3g images in tests/.../Fixtures.
- System Restore page (Ted, Point-in-Time Restore): System Restore | Point-in-Time Restore segments. Create Restore Point uses
  `RestorePoints.CreateNow`, which sets SystemRestorePointCreationFrequency to 0 for that one call (backed up, put back) so it never
  skips quietly. Point-in-Time Restore (`PointInTimeRestore`, Windows 11 24H2/25H2 build x.8737+) reads and writes only the `_UX`
  values Settings uses under `PITR\Settings` (Active, SnapshotInterval, MaxTimespan in minutes, MaxGlobalSize in MB), backed up
  first; `_GPO` values win and lock the controls. Snapshots come from `PITR\Snapshots` (shadow copy IDs) with times from
  Win32_ShadowCopy. Snapshot Now starts Windows' own `\Microsoft\Windows\Setup\PITRTask` with TASK_RUN_IGNORE_CONSTRAINTS
  (undocumented by Microsoft; the task isn't changed). Restoring only happens in WinRE (Restart to Recovery = `shutdown /r /o`).
  Never delete Point-in-Time snapshots directly; Windows manages them.
- Background tasks (Ted, 2026-10-23, `BackgroundTasks`): every page method that sets IsBusy (or IsScanning/IsMeasuring) starts
  `using var task = BackgroundTasks.Start(this, "Title Case Verb Phrase")` and sets `task.Percent` (or `task.Step(done, total)`) when it can
  tell; null means a moving bar (a test checks every busy page). MainWindow shows the newest task of a page that isn't open as a card in the
  bottom right corner (`MainViewModel.CornerTask`, after 1 s so quick reads never flash it; click opens the page, X hides it until the next
  task). Closing, tray Exit, Restart as Administrator and Install Now ask with `MainWindow.ConfirmExit` (Close Anyway | Keep Open, Keep Open
  the default and what Esc means); Close to System Tray and Windows signing out never ask.
  Time (Ted, 2026-10-23): `ProgressClock` (Core) gives "1:05 elapsed · about 3 min left"; time left only after 5 s and 2% since the
  percentage last started over (a drop or a first value), counted down between updates. `RunningTask.TimeText` ticks once a second from one
  timer that runs only while a task does; `Rough` marks a guessed percentage (~42%, "rough guess"). Pages show it with `Views/TaskClock`
  under their own bar (follows the page's newest task, hides itself).
- Search boxes above tables use `Views/SearchBox`; Select All ticks only what the search shows.
- Duplicate Finder (Ted, 2026-10-12): searches folders and whole drives (Fixed and Removable, each given 5 s to answer). It never searches Windows,
  program folders, ProgramData, AppData (any user's), Temp\.net, its own folder, or a drive's Recycle Bin/System Volume Information/Recovery,
  as a starting point or inside one (`DuplicateSearchOptions.Protected` + `DuplicateFinder.AlwaysSkipped`). In-Depth (`SimilarFinder`) lists
  similar files with a likeness score (capped at 99%; identical files stay in Identical): content-defined chunks of the first 8 MB, names
  without copy marks, sizes, and pictures by a 64-bit difference hash (`PictureHasher`, Windows' decoders). Only files whose SHA-256
  matches another can be ticked (Ted, 2026-10-23, `DuplicateFileItem.CanTick`): identical sets until Verify Hashes finds a copy changed,
  and in similar sets only files Verify Hashes found identical; one copy of each hash always stays. Verify Hashes re-reads the shown
  files in full and compares SHA-256; changed copies are unticked and locked.
  Check All ticks every copy but `DuplicateFinder.SuggestKeep` (no copy mark, outside Downloads/Temp, oldest, shortest path); Keep
  Newest/Oldest only tick too, nothing is removed before Remove Selected. Remove To: Recycle Bin, or A Folder (`DuplicateMover`): a dated
  "Duplicates yyyy-MM-dd HHmmss" folder per move with files under their old drive and folders, Duplicate Report.csv (kept copy, old place,
  new place) and moves.json for Restore (`DuplicateRestoreWindow`, never overwrites a file at the old place). The folder is never searched.
  The page is three cards: Summary, Actions, Results. The faster-as-administrator note lives in the standard-user banner (MainWindow).
  Fast scan (Ted, 2026-10-23): a whole NTFS drive (Fixed or Removable) is listed from its file table when Kodiak Cleaner runs as
  administrator (`NtfsFileTable.TryListDrive`, the Disk Analyzer's reader with `keepFiles`), with the same folders left out through
  `keepFolder`; anything else (folders, FAT32/exFAT, no admin) falls back to the folder walk. Hard-linked files are listed once.
  Matching stays size first, then a 64 KB SHA-256, then full SHA-256, so only same-size files are ever read.
  Name Matching (Ted, 2026-10-12, In-Depth only, `NameRules`): Match Similar Names lists files whose names are alike (edit distance,
  words in any order; Name Similarity %, default 80) even when contents differ; Leave Out of Names (Copy Marks and Version Words on,
  Dates and Times and Numbers off) plus the person's own words; Any File Type compares names across types. Name Patterns (* any text,
  ? one character, # a number) make one set per pattern, shown first with a Name Pattern badge and no percent. Options aren't saved yet.
- Mass Rename (Ted, 2026-10-12, `MassRenamer` in Core, `MassRenameViewModel`): renames files in one folder (subfolders optional,
  `--page=rename`). Rules apply in order: pattern tokens ({created} {ext} {folder} {modified} {name} {num} {taken}, with :format,
  :1-5 slices and :3 padding), Find and Replace (plain or regular expression, 1 s timeout), Letter Case (Title Case follows Ted's rule:
  short joining words stay lower inside a name), Tidy Spaces, Extension. The preview flags names Windows won't take, two files
  getting one name and names already in the folder (from a listing read at load, nothing read per keystroke); those keep their
  names. Only ticked files are numbered. Renames go through a temporary name so swaps and case-only changes work; a file that
  fails keeps its name. The journal (`Backups\Mass Rename\last-rename.json`) is written before renaming and Undo Last Rename
  reverses it, skipping files changed since. Never offers Windows, program folders, ProgramData, AppData, Kodiak Cleaner's own
  folders, a drive root, hidden/system files or linked folders. Rules are remembered in `[Mass Rename]` (values quoted, so spaces survive).
- Settings search (Ted, 0.8.45): a `SearchBox` above General filters the Settings sidebar (`SettingsSearch`: every word must appear in the page's title,
  description or labels). Labels are read from the page templates themselves on the first search, so new settings are found automatically.
  The Check for Updates interval is a dropdown (Ted, 0.8.45) so more choices fit.
- Registry checks (Ted, 0.8.30): 15 categories in `RegistryScanner.Categories` (A to Z, Look For grid of 3 columns, Select All). Only report what is
  definitely gone (`PathChecks.IsMissing`); never files in the Windows folder (`MissingOutsideWindows`), drivers, boot/system-start services,
  or HKLM file extensions. COM classes and type libraries only when every listed file is gone.
- Registry backups keep rare value types (hex(N) via RegQueryValueEx) so restores bring back the same type.
- Command line (Ted, 0.8.53, `Cli` in CommandLine.cs): `/clean [/items:]`, `/list-items`, `/registry [/scan-only] [/categories:]`, `/uninstall:<name|{GUID}> [/quiet]`,
  `/list-programs`, `/help`; `-` and `=` work too. Handled in `App.Start` before single-instance and the Always Admin/Skip UAC relaunch, so it never
  opens a window. Same rules as the GUI: caution items never run, registry fixes always back up, uninstall backs up first, leftovers only from
  Programs. Output attaches to the parent console; exit codes 0 done, 1 some failures, 2 bad switch/not found, 3 uninstall didn't finish.
  Document any new switch in `Cli.Help` and the README's Command Line section.
- Kodiak Cleaner never uninstalls itself (Ted, 0.8.54, `SelfGuard`): its own entry (upgrade code's product codes, or "Kodiak Cleaner" by Ted Tabaka,
  or this copy's folder) is left out of Uninstall, batches, Delete Entry and `/uninstall`, with a message pointing to Control Panel.
- Reset to Default for all settings is the last card in Settings > Advanced (Ted, 0.8.59; was on the Developer page in 0.8.55, which goes
  away at public beta, and replaces the old Reset Settings row in Application). `Settings.Reset` keeps the supporter code but starts LaunchCount again
  (Ted, 0.8.60), then `Donation.SupporterTurnsPromptOff` keeps Supporters' reminder off.
- Programs: Product Code column (`ProgramItem.ProductCode`, the uninstall key's GUID) with right-click Copy Product Code and Copy Uninstall
  Command (`msiexec /x {GUID} /qn`, Windows Installer programs only) (Ted, 0.8.53).
- New startup programs (Ted, 0.8.53, `NewStartupWatch`): at start (after `Integration.SyncAll`) and every 10 minutes while running, entries not in
  the Startup History (`StartupHistory.NotYetSeen`) pop up in `NewStartupWindow` with Keep/Disable/Delete each; Kodiak Cleaner's own entries are
  skipped and the first look only records. `[General] NotifyNewStartup` (default on) turns it off.
- Programs actions (Uninstall, Repair, Rename, Delete Entry) are buttons and a right-click menu. Rename and Delete Entry
  back up the uninstall entry first; Delete Entry never touches program files. Tools are listed alphabetically.
- Settings footer, two lines: `Copyright © 2026 TED TABAKA.` then `Free and open source under the MIT License.`
  No Terms of Use link there (Ted removed it); Terms of Use and Privacy Policy are pages under Help.
- Dev and Beta builds show the Beta Software notice at every start until it closes with Don't Show This Again at Startup
  on (`ShowBetaNotice`, also a switch under Settings > General). Stable builds never show it.
- Automatic updates (0.8.40): read `updates/<channel>.json` ({channel, version, msi: {url, sha256}}) from the website, accept only an
  .msi on https://www.kodiakcleaner.app (no redirects followed), verify its SHA-256 and start msiexec on exit without waiting (so the exe isn't
  locked); a hidden cmd.exe reopens Kodiak Cleaner when msiexec exits with 0 (Ted, 0.8.43). Two switches (Ted, 0.8.51): Check for Updates Automatically (`[Updates] AutoUpdate`) only checks and shows a notice; Download and Install Updates Automatically (`AutoInstall`, greyed out while checking is off; defaults to the old AutoUpdate value so earlier opt-ins keep installing) also downloads and installs. With checking on, the check follows `UpdateSchedule` ([Updates] Interval, Ted 0.8.44: On Startup (default), Every Hour, Every 4 Hours, Daily, Every 3 Days, Weekly, Twice a Month (15 days), Monthly; listed in that order, not A to Z), checked at start and by a 5-minute timer while open. Every release must refresh the website's update
  file with the new version and the MSI's SHA-256 (release-<ver>/updates/dev.json).
- Pop-ups (0.8.19, Ted): always `Dialogs` (Confirm, Warn with a caution box, YesNoCancel, Info, Error), which shows the themed `MessageWindow`;
  never `MessageBox` directly. Yes/No pop-ups show No on the left and Yes on the right as the default (Enter) button (Ted, 0.8.50,
  replacing No as the default); Esc or closing means No. On a warning, Yes is a `DangerButton`.
- Every window uses `UseLayoutRounding="True" TextOptions.TextFormattingMode="Display"` for crisp text (0.8.27).
- README screenshots live in `docs/images/` (never shipped in packages); retake them when pages change.
- Clipboard copies go through `ClipboardHelper` (retries, then a plain message), never `Clipboard.SetText`.

## Feedback and Support
- Never put Ted's email address anywhere: app, docs, installer, tests, website (Ted, 2026-10-10, 0.8.56; a test checks).
  Contact & Feedback buttons open the website's Contact Us form (`AppInfo.ContactUrl`) in the browser with `?topic=` and
  `&version=`. Nothing is sent automatically, no hidden data in links, and Copy Details never includes files, folders or user names.
- Security issues are reported privately, never in public issues. Contact & Feedback has four accent buttons (Ted, 0.8.24):
  Send Feedback, Report a Bug, Request a Feature and Security (the security form); no separate security link.
- Website: https://www.kodiakcleaner.app/ (Ted, 0.8.28; `AppInfo.HomepageUrl`): About > Learn More Website, HOMEPAGE.url and the MSI support/help links. Updates come from the website since 0.8.40; release downloads stay on GitHub releases; source and Star on GitHub stay on the repo.
- The repository (ttabakagroup/kodiakcleaner) is private for now (2026-10-06). Rate Kodiak Cleaner points to starring it on GitHub until there's a store listing.
- Never offer anything in return for a rating. Support link: https://ko-fi.com/s/aabd61b988 (the Kodiak Cleaner supporter item in Ted's Ko-fi Shop; every donate button uses AppInfo.KoFiUrl). The only donation asks are the ones Ted asked for in 0.8.55
  (`Donation`): the startup pop-up (`DonationWindow`, `[General] ShowDonationPrompt`, can't be turned off before 10 launches counted in
  `[General] LaunchCount`, never stacked on Getting Started/What's New) and the banner on every 10th launch. Supporters see neither.
- Supporter tier only (Ted removed Premium). Codes are verified offline; never run `make-key` for Ted or touch his private key.

## Versions and Channels
- `version.json` is the single source: `{ "channel": "dev", "version": "0.8.0", "targetStable": "1.0.0" }`.
- Dev 0.8.x, Beta 0.9.x, Stable 1.x. Never change channel unless Ted says so ("move to beta" = 0.9.0,
  "move to stable" = 1.0.0, "new minor release" raises the stable minor).
- Show `AppInfo.VersionName` ("0.8.0 Dev") wherever a version is shown or sent.
- Release builds (`build.ps1 -Release`, tagged CI builds) strip the Developer page and refuse to build with the
  test supporter key or without a CHANGELOG entry.

## Commit Routine
After each completed, tested change:
1. Run `dotnet test tests/KodiakCleaner.Core.Tests` (and build the app). Don't commit if anything fails.
2. Bump the patch number in `version.json` (+0.0.1) unless Ted gave a channel or minor instruction.
3. Add the entry to `CHANGELOG.md` under `## [version_name] - YYYY-MM-DD`, grouped by area.
4. Update README.md and this file if behavior or rules changed.
5. Commit as `[version_name]: short summary` with a body listing changes by area.
6. Tag `v[version]-[channel]` (e.g. `v0.8.1-dev`; stable as `v1.0.2`).
7. Every GitHub release carries `KodiakCleaner-<Branch>-Setup.msi` (bare, so Download.url can link it; Ted, 0.8.31) and
   `KodiakCleaner-<Branch>-Portable.zip`, Branch = Dev, Beta or Stable, the same name every release (standard, 0.8.31; replaces
   Ted's 0.8.10 versioned names). No Setup ZIP since 0.8.37 (Ted): the bare MSI is the installer deliverable everywhere,
   including release folders (Ted gets builds through GitHub and the C:\Projects\KodiakCleaner copy). On Windows `build.ps1`
   makes both; on Linux publish the exe, then run `installer/package-linux.sh publish dist` (WiX under Wine; the same files as
   build.ps1, a test checks it). Test the MSI installs before shipping.
8. Docs (standard): `tools/MakeDocs` writes Changelog.txt, Download.url, Homepage.url, License.txt, Privacy.txt, ReadMe.txt
   (from `dist-files/ReadMe.md`, the user-facing read me) and TermsOfUse.txt as plain text with CRLF. Both ZIPs have a `Docs\` folder and the
   MSI installs it to `Program Files\Kodiak Cleaner\Docs`. Download.url: Dev = the website's /download/ page (the repository is private
   until the first Beta, `DocsFolder.RepositoryIsPublic`); Beta: `releases/download/latest-beta/KodiakCleaner-Beta-Setup.msi`; Stable:
   `releases/latest/download/KodiakCleaner-Stable-Setup.msi`. Project Page (About) opens the website's /open-source/ page and Star on GitHub
   is hidden while `AppInfo.RepositoryIsPublic` is false; both switch to GitHub on their own with the first Beta build.
9. Deliver the project-folder layout (`C:\Projects\Kodiak Cleaner`, standard's project-folders.md) as one `Kodiak Cleaner.zip` when
   C:\Projects can't be reached: Source (with .git), Website (+ReadMe.txt, no .dev.vars/.wrangler/node_modules), Assets\Design,
   Assets\Logos, Assets\StoreAssets, Assets\Git (mirror clone), Application\Windows, Application\Linux, Docs, ARCHIVE\ChangelogComplete.txt.
   Every release package also carries the newest Linux app ZIPs (Ted, 2026-10-05): KodiakCleaner-Dev-AppImage.zip and
   KodiakCleaner-Dev-Deb.zip from /mnt/project-files/kodiak-linux/dist (built by the Linux thread), listed in FILE-LIST.md.
   The changelog resets only when Ted moves to Beta or Stable; ChangelogComplete.txt is the running history.
Push each valid build to the private repo without asking, then have the PC copy synced (Ted, 2026-10-09).

## Testing on Linux
The app builds on Linux (WindowsDesktop SDK copied in) and runs under Wine for screenshots: `sudo tools/container/setup.sh` sets up
the SDK in a fresh container, and `--wine` adds Wine, Xvfb (:97) and stand-in fonts with real icons. Never set TMPDIR for Wine (it crashes). Wine can't check
supporter codes (no ECDSA in its CNG), so use the Developer page preview there.
