# Privacy policy (KeyAutomator)

KeyAutomator does **not** send personal data or macro contents to the internet.  
Everything it stores or reads stays on this PC.

> For Microsoft Store submission, register a **public HTTPS URL** for this page in Partner Center.  
> Example: `https://github.com/Nakashun-mf/KeyAutomator/blob/main/PRIVACY.en.md`  
> Japanese: [PRIVACY.md](PRIVACY.md)

## What this app stores

| File | Contents |
|---|---|
| `config.json` | Macros (names, steps, strings to type, and so on) |
| `settings.json` | App settings (step gap, confirm-before-delete, display language, and so on) |
| `error.log` | Technical details after an error (optional; may fall back to a temp folder) |

The files live in one of these places:

- The same folder as the exe (when that folder is writable)
- `%LocalAppData%\KeyAutomator` (protected folders, packaged / Store installs, and similar)

Use the **Settings folder** button at the bottom of the window to open the actual location.

## What we do not send

- Macro contents
- Typed text or passwords
- Periodic device telemetry or usage analytics
- Keystroke capture or recording (this app **sends** input only; it is not a keylogger)

(There is no “phone home” on launch.)

## Permissions (MSIX / Store)

The packaged build declares the restricted capability `runFullTrust` so WinUI 3 can run as a desktop app and so Win32 `SendInput` can automate typing. Unused capabilities are not added.

## Please note

- If you put passwords or personal data in a macro, they remain **in plain text** in `config.json`
- Treat copies, shares, and backups of that file with care
- Delete the files in the settings folder when you no longer need the data

## Contact

Bugs and questions: GitHub Issues.

https://github.com/Nakashun-mf/KeyAutomator/issues

If you installed from the Microsoft Store, the same Issues page is the support channel.  
(If the publisher also lists an email address, the Store support contact will point there too.)
