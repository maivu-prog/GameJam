# -*- coding: utf-8 -*-
"""How much should a night fish be worth?

Nothing in the game points at the night table -- that is deliberate, it is there to be found. But a
discovery that ENDS the run is not a reward, it is an exit: at today's prices a player who works it
out finishes all twelve levels in 16 minutes instead of 90.

So this sweeps the night fish values down and asks, for each: does finding the night still pay
enough to be worth the risk, and does the run still fill its 90 minutes either way?
"""
import sys, io, json, subprocess, shutil, os

SRC = "Assets/Resources/GameData/game-data.json"
BAK = SRC + ".night"


def arc(eff, night):
    args = [sys.executable, "tools/sim_session.py", str(eff), "r", "d"] + (["n"] if night else [])
    out = subprocess.run(args, capture_output=True, text=True).stdout
    ships = [int(float(l.split()[0])) for l in out.splitlines() if "SHIP-" in l]
    end = next((l for l in out.splitlines() if l.startswith("ket thuc")), "")
    last = ships[-1] if len(ships) == 3 else None
    return last, ("12/12/12/12" in end)


shutil.copy(SRC, BAK)
base = {f["id"]: f["value"] for f in json.load(io.open(BAK, encoding="utf-8"))["fish"] if f.get("atk", 0)}
print("gia goc ban dem:", base)
print(f"\n{'x gia':>7} {'gia moi':>22} {'ngu: xong tau D':>17} {'cau dem: xong tau D':>21} {'loi the':>9}")
for m in [1.0, .7, .5, .35, .25, .18, .12]:
    d = json.load(io.open(BAK, encoding="utf-8"))
    vals = []
    for f in d["fish"]:
        if f.get("atk", 0):
            f["value"] = max(5, int(round(f["value"] * m)))
            vals.append(f["value"])
    io.open(SRC, "w", encoding="utf-8", newline="\n").write(json.dumps(d, ensure_ascii=False, indent=2))
    sleep_end, sleep_ok = arc(0.75, False)
    night_end, night_ok = arc(0.75, True)
    gain = f"{sleep_end/night_end:.1f}x nhanh hon" if (sleep_end and night_end) else "?"
    tag = ""
    if night_end and sleep_end and 1.15 <= sleep_end / night_end <= 1.5 and night_ok and sleep_ok:
        tag = "  <- thuong xung dang, van du 90 phut"
    print(f"{m:>7.2f} {str(vals):>22} {str(sleep_end)+' phut':>17} {str(night_end)+' phut':>21} {gain:>9}{tag}")

shutil.copy(BAK, SRC); os.remove(BAK)
print("\n(da khoi phuc game-data.json - day chi la do thu)")
