# -*- coding: utf-8 -*-
"""Value-for-money chart for the four upgrade branches.

Straight out of NOTE-game-balance-math.md 1.4-1.6: the player silently computes
    do dang mua = thu nhap them / gia mon tiep theo
and buys the highest. If one branch's line sits on top from start to finish, the other three are
decoration. The lines have to CROSS.

Income is modelled the way sim_session.py actually resolves a cycle, so the numbers here and the
90-minute arc cannot drift apart.
"""
import json, io, sys

d = json.load(io.open("Assets/Resources/GameData/game-data.json", encoding="utf-8"))
t, e = d["tuning"], d["economy"]
FISH = {f["id"]: f for f in d["fish"] if f.get("atk", 0) == 0}
PORTS, BANDS, ZONES = d["ports"], {b["id"]: b for b in d["bands"]}, {z["index"]: z for z in d["zones"]}
UP, MILE = {}, {}
for u in d["upgrades"]:
    UP.setdefault(u["branch"], []).append((u["level"], u["cost"]))
    if u.get("milestone"): MILE.setdefault(u["branch"], []).append(u["level"])
for k in UP: UP[k] = [c for _, c in sorted(UP[k])]
GAIN = e.get("MilestoneGain", 1.5)

def mile_mul(branch, level):
    """Milestones passed on this branch multiply its bonus -- the staggered lists in GameCatalog."""
    m = 1.0
    for lv in MILE.get(branch, []):
        if level >= lv: m *= GAIN
    return m

LP, MAXT = 4, 3
DAY = t["DaySeconds"]
GAP, JIT = t["DockGap"], (1 + t["DockGapVarMax"]) / 2
px = [6.0]
for z in d["zones"]: px.append(px[-1] + GAP * z["gapMul"] * JIT)
centre = lambda zi: (px[zi - 1] + px[zi]) / 2

def region_value(zi, bid):
    b = BANDS[bid]; port = PORTS[min(zi, len(PORTS) - 1)]
    tot = n = 0.0
    for f in FISH.values():
        if not (f["minDepth"] < b["bottom"] and f["maxDepth"] > b["top"]): continue
        s = max(0.0, f["rarity"] - 1)
        w = (0.45 ** s) * ((1 + (b["rarityBias"] + ZONES[zi]["rarityBias"]) * 0.55) ** s)
        tot += w * f["value"] * port.get("price_" + f["id"], 1.0); n += w
    return (tot / n * 1.15) if n else 0.0

def cast_seconds(bid, hook, tier):
    """Hook buys BOTH ends of the cycle: damage shortens the fight, hook speed the sink and reel."""
    b = BANDS[bid]; depth = (b["top"] + b["bottom"]) / 2
    hm = mile_mul("hook", hook)
    hs = 1 + hook * e["hookSpeedPerLevel"] * hm + tier * e["tierHookSpeedBonus"]
    dm = 1 + hook * e["hookDamagePerLevel"] * hm + tier * e["tierDamageBonus"]
    return depth / (t["HookSinkMax"] * hs) + 6.0 / dm + depth / (t["HookRetract"] * hs)

def repair_bill(zi, hull, tier):
    port = PORTS[min(zi, len(PORTS) - 1)]
    worst = sum(port.get("obs_" + o["id"], 0) * o["damage"] for o in d["obstacles"])
    hm = mile_mul("hull", hull)
    hp = e["startHullHp"] + hull * e["hullHpPerLevel"] * hm + tier * e["tierHullHpBonus"]
    armor = hull * e["hullArmorPerLevel"] * hm + tier * e["tierArmorBonus"]
    # Armour is what makes the hull branch pay: raw +HP never lowered the bill, because repairs are
    # charged per missing HP and the damage arriving was the same either way.
    return min(hp * .55, worst * .35 / (1 + armor)) * e["repairCostPerMissingHp"]

def wreck_odds(zi, hull, tier):
    """Wreck() clears the hold, so sinking costs a whole cycle's catch. This -- not the repair bill --
    is what the hull branch buys: repairs are charged per missing HP, so max HP never changed them."""
    port = PORTS[min(zi, len(PORTS) - 1)]
    worst = sum(port.get("obs_" + o["id"], 0) * o["damage"] for o in d["obstacles"])
    hm = mile_mul("hull", hull)
    hp = e["startHullHp"] + hull * e["hullHpPerLevel"] * hm + tier * e["tierHullHpBonus"]
    armor = hull * e["hullArmorPerLevel"] * hm + tier * e["tierArmorBonus"]
    return max(0.0, min(.85, (worst * .35 / (1 + armor) / max(hp, 1) - .45) * 1.6))


def cycle_income(lv, tier, zi, bid, travel_zones=2):
    speed = t["MaxSpeed"] * (1 + lv["engine"] * e["engineSpeedPerLevel"] * mile_mul("engine", lv["engine"])
                             + tier * e["tierBoatSpeedBonus"])
    travel = abs(centre(min(zi + travel_zones, 9)) - centre(zi)) / speed
    # Fishing is mid-zone, selling is at the port on the zone edge: every cycle pays a round trip.
    # Without it the engine branch was worth exactly nothing once the player settled in zone 9.
    hop = (GAP * ZONES[zi]["gapMul"] * JIT) / speed
    fishing = max(0.0, DAY - travel - hop)
    cap = (e["basketBaseCapacity"] + lv["hold"] * e["holdCapacityPerLevel"] * mile_mul("hold", lv["hold"])
           + tier * e["tierCapacityBonus"])
    catches = min(fishing / cast_seconds(bid, lv["hook"], tier), cap)
    gross = catches * region_value(zi, bid) * (1.0 - wreck_odds(zi, lv["hull"], tier))
    return gross - repair_bill(zi, lv["hull"], tier)

def context(level):
    """Where the player realistically is when this level is on offer."""
    tier = min(level // LP, MAXT - 1)
    return tier, [3, 6, 9][tier], ["A", "B", "C"][tier]

def table():
    print(f"{'bac':>3} {'tier':>4}  " + "  ".join(f"{k:>22}" for k in ["hook", "hold", "engine", "hull"]))
    print(f"{'':>3} {'':>4}  " + "  ".join(f"{'gia   d.thu   dang mua':>22}" for _ in range(4)))
    best_count = {k: 0 for k in UP}
    rows = []
    for level in range(12):
        tier, zi, bid = context(level)
        base = {"hook": level, "hold": level, "engine": level, "hull": level}
        cells, vfm = [], {}
        for k in ["hook", "hold", "engine", "hull"]:
            if level >= len(UP[k]): cells.append(f"{'-':>22}"); continue
            cost = UP[k][level]
            up = dict(base); up[k] += 1
            gain = cycle_income(up, tier, zi, bid) - cycle_income(base, tier, zi, bid)
            v = gain / cost
            vfm[k] = v
            cells.append(f"{cost:>5} {gain:>7.1f} {v:>8.4f}")
        win = max(vfm, key=vfm.get); best_count[win] += 1
        rows.append((level, win, vfm))
        print(f"{level+1:>3} {tier:>4}  " + "  ".join(cells) + f"   <- {win}")
    print("\nso lan dan dau:", ", ".join(f"{k}={v}" for k, v in best_count.items()))
    flips = sum(1 for i in range(1, len(rows)) if rows[i][1] != rows[i-1][1])
    print(f"so lan doi ngoi vuong: {flips}  ->", "DUNG BAI (cac duong cat nhau)" if flips >= 3
          else "HONG (mot nhanh thong tri, 3 nhanh kia la do trang tri)")
    return rows

def growth():
    print("\nti le tang gia moi bac (note yeu cau 1.07 - 1.15):")
    for k in ["hook", "hold", "engine", "hull"]:
        c = UP[k]
        r = [c[i+1] / c[i] for i in range(len(c) - 1)]
        geo = (c[-1] / c[0]) ** (1 / (len(c) - 1))
        flag = "ok" if 1.07 <= geo <= 1.15 else "NGOAI KHOANG"
        print(f"  {k:7} r trung binh = {geo:.3f}  ({flag})   dau {min(r):.2f} .. cuoi {max(r):.2f}")

if __name__ == "__main__":
    table(); growth()
