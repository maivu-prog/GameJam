# Game data ↔ Google Sheet sync

Sync `Assets/Resources/GameData/game-data.json` with a Google Sheet, one tab per feature
(`fish`, `ports`, `obstacles`, `zones`, `upgrades`, `tuning`, `economy`).

- **push** = local JSON → Sheet (designers edit in the sheet)
- **pull** = Sheet → local JSON (bring edits back into the repo/game)

## Auth: log in as yourself (recommended)

Bagelcode's Workspace **blocks sharing sheets with external service accounts**, so the
service-account method won't work. Instead authenticate **as your own Bagelcode account**
(`--auth oauth`, the default) — you already have access to your sheets, so nothing needs sharing.

### Setup (one time)

1. Install deps:
   ```bash
   pip install -r requirements.txt
   ```
2. Create an **OAuth client** (Desktop app) and download its JSON:
   - Google Cloud Console → create/select any project → **Enable** *Google Sheets API*.
   - *APIs & Services* → *OAuth consent screen* → set it up as **Internal** (Bagelcode) if available.
   - *Credentials* → *Create credentials* → *OAuth client ID* → **Desktop app**.
   - Download the JSON, save as `tools/oauth_client.json` (git-ignored — never commit).
3. Create a Google Sheet in your Bagelcode account, copy its id from the URL:
   `https://docs.google.com/spreadsheets/d/<SHEET_ID>/edit`.

First run opens a browser to log in; the token is cached in `tools/authorized_user.json`.

### Alternative: gcloud ADC (no client file)

If you have the gcloud CLI:
```bash
gcloud auth application-default login --scopes=https://www.googleapis.com/auth/spreadsheets,https://www.googleapis.com/auth/drive
python sheet_sync.py push --auth adc --sheet <SHEET_ID>
```

## Usage

```bash
cd tools

# push local data up to the sheet (creates/overwrites the tabs)
python sheet_sync.py push --sheet <SHEET_ID>

# pull the sheet back into game-data.json
python sheet_sync.py pull --sheet <SHEET_ID>
```

Default auth is `oauth` with client file `oauth_client.json`. Override with
`--auth adc|service` and `--creds <file>`. You can also set env vars once:

```bash
export GAME_SHEET_ID=<SHEET_ID>
export GAME_SHEET_AUTH=oauth
python sheet_sync.py push
```

(`--sheet` also accepts the full sheet URL.)

## Notes

- Numbers/booleans are auto-detected on pull (e.g. `12`, `1.5`, `true`).
- In the `ports` tab, `price_<fishId>` columns are the per-species sell multipliers.
- In `zones`, `allows` is a whitelist (`sardine,bream`) or blacklist (`!ghost_tuna,!bream`).
- First run: use **push** to populate the sheet from the current JSON, then edit there.
- The Unity side still reads these values from `GameCatalog.cs` today; loading
  `game-data.json` at runtime is the planned follow-up so the sheet drives the game directly.
```
