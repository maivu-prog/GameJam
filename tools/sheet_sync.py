#!/usr/bin/env python3
"""
Sync the game's data file (Assets/Resources/GameData/game-data.json) with a Google Sheet.

Each feature section becomes its own worksheet (tab):
  - Table sections (fish, ports, obstacles, zones, upgrades): header row + one row per entry.
  - Key/value sections (tuning, economy): two columns, `key` and `value`.

Usage:
  python sheet_sync.py push [--sheet <id_or_url>] [--creds service_account.json] [--data <path>]
  python sheet_sync.py pull [--sheet <id_or_url>] [--creds service_account.json] [--data <path>]

`push` = local JSON  -> Google Sheet   (overwrites the tabs)
`pull` = Google Sheet -> local JSON     (overwrites game-data.json, keeping _meta)

Sheet id / creds may also come from env vars GAME_SHEET_ID and GAME_SHEET_CREDS.
See README.md for one-time Google setup.
"""
import argparse
import json
import os
import re
import sys
from pathlib import Path

try:
    import gspread
except ImportError:
    sys.exit("Missing deps. Run:  pip install -r requirements.txt")

SCOPES = ["https://www.googleapis.com/auth/spreadsheets"]
TABLE_SECTIONS = ["fish", "ports", "obstacles", "zones", "upgrades"]
KV_SECTIONS = ["tuning", "economy"]
DEFAULT_DATA = Path(__file__).resolve().parent.parent / "Assets" / "Resources" / "GameData" / "game-data.json"

_INT_RE = re.compile(r"^-?\d+$")
_FLOAT_RE = re.compile(r"^-?\d*\.\d+$")


def coerce(value: str):
    """Turn a sheet cell string back into int/float/bool/str."""
    if value is None:
        return ""
    s = str(value).strip()
    if s == "":
        return ""
    low = s.lower()
    if low in ("true", "false"):
        return low == "true"
    if _INT_RE.match(s):
        return int(s)
    if _FLOAT_RE.match(s):
        return float(s)
    return s


def cell(value):
    """Turn a JSON value into something the Sheets API accepts."""
    if isinstance(value, bool):
        return "TRUE" if value else "FALSE"
    return value


def make_client(auth: str, creds_path: str):
    """auth = 'oauth' (log in as yourself — works around domain sharing limits),
             'adc'   (reuse `gcloud auth application-default login`),
             'service' (service-account key file)."""
    if auth == "oauth":
        # Logs in as your own Google account via the browser; token cached next to the client secret.
        token = os.path.join(os.path.dirname(os.path.abspath(creds_path)) or ".", "authorized_user.json")
        return gspread.oauth(credentials_filename=creds_path, authorized_user_filename=token, scopes=SCOPES)
    if auth == "adc":
        import google.auth
        creds, _ = google.auth.default(scopes=SCOPES)
        return gspread.authorize(creds)
    from google.oauth2.service_account import Credentials
    creds = Credentials.from_service_account_file(creds_path, scopes=SCOPES)
    return gspread.authorize(creds)


def open_sheet(client, sheet_ref: str):
    if sheet_ref.startswith("http"):
        return client.open_by_url(sheet_ref)
    return client.open_by_key(sheet_ref)


def get_or_create(spreadsheet, title: str, rows: int, cols: int):
    try:
        ws = spreadsheet.worksheet(title)
        ws.resize(rows=max(rows, 1), cols=max(cols, 1))
        return ws
    except gspread.WorksheetNotFound:
        return spreadsheet.add_worksheet(title=title, rows=max(rows, 1), cols=max(cols, 1))


def push(data: dict, spreadsheet):
    for name in TABLE_SECTIONS:
        rows = data.get(name, [])
        if not rows:
            continue
        header = list(rows[0].keys())
        values = [header] + [[cell(row.get(h, "")) for h in header] for row in rows]
        ws = get_or_create(spreadsheet, name, len(values) + 2, len(header) + 2)
        ws.clear()
        ws.update(range_name="A1", values=values)
        print(f"  pushed '{name}': {len(rows)} rows x {len(header)} cols")
    for name in KV_SECTIONS:
        section = data.get(name, {})
        if not section:
            continue
        values = [["key", "value"]] + [[k, cell(v)] for k, v in section.items()]
        ws = get_or_create(spreadsheet, name, len(values) + 2, 4)
        ws.clear()
        ws.update(range_name="A1", values=values)
        print(f"  pushed '{name}': {len(section)} keys")


def pull(spreadsheet, existing: dict) -> dict:
    out = dict(existing)  # keep _meta and section order
    for name in TABLE_SECTIONS:
        try:
            grid = spreadsheet.worksheet(name).get_all_values()
        except gspread.WorksheetNotFound:
            print(f"  (skip '{name}': no tab)")
            continue
        grid = [r for r in grid if any(c.strip() for c in r)]
        if len(grid) < 2:
            out[name] = []
            continue
        header = grid[0]
        out[name] = [{h: coerce(v) for h, v in zip(header, row)} for row in grid[1:]]
        print(f"  pulled '{name}': {len(out[name])} rows")
    for name in KV_SECTIONS:
        try:
            grid = spreadsheet.worksheet(name).get_all_values()
        except gspread.WorksheetNotFound:
            print(f"  (skip '{name}': no tab)")
            continue
        out[name] = {row[0]: coerce(row[1]) for row in grid[1:] if len(row) >= 2 and row[0].strip()}
        print(f"  pulled '{name}': {len(out[name])} keys")
    return out


def main():
    ap = argparse.ArgumentParser(description="Sync game-data.json with a Google Sheet.")
    ap.add_argument("action", choices=["push", "pull"])
    ap.add_argument("--sheet", default=os.environ.get("GAME_SHEET_ID", ""), help="Sheet id or full URL")
    ap.add_argument("--auth", default=os.environ.get("GAME_SHEET_AUTH", "oauth"), choices=["oauth", "adc", "service"],
                    help="oauth = log in as yourself (default; avoids domain sharing limits); adc = gcloud ADC; service = service-account key")
    ap.add_argument("--creds", default=os.environ.get("GAME_SHEET_CREDS", "oauth_client.json"),
                    help="OAuth client-secret JSON (oauth) or service-account JSON (service). Ignored for adc.")
    ap.add_argument("--data", default=str(DEFAULT_DATA), help="Path to game-data.json")
    args = ap.parse_args()

    if not args.sheet:
        sys.exit("No sheet given. Pass --sheet <id_or_url> or set GAME_SHEET_ID.")
    if args.auth != "adc" and not os.path.exists(args.creds):
        sys.exit(f"Credentials not found: {args.creds}  (see README.md, auth mode '{args.auth}')")

    data_path = Path(args.data)
    with open(data_path, encoding="utf-8") as f:
        data = json.load(f)

    client = make_client(args.auth, args.creds)
    spreadsheet = open_sheet(client, args.sheet)

    if args.action == "push":
        print(f"Pushing {data_path.name} -> '{spreadsheet.title}'")
        push(data, spreadsheet)
        print("Done. Edit the sheet, then `pull` to bring changes back.")
    else:
        print(f"Pulling '{spreadsheet.title}' -> {data_path.name}")
        merged = pull(spreadsheet, data)
        with open(data_path, "w", encoding="utf-8") as f:
            json.dump(merged, f, indent=2, ensure_ascii=False)
            f.write("\n")
        print(f"Done. Wrote {data_path}")


if __name__ == "__main__":
    main()
