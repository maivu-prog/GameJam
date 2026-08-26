"""Simulate a 90-minute session against game-data.json and print the progression arc.

Deliberately crude but honest: a greedy player who sails to the best water their hull allows,
fishes the day phase, sleeps through the night, sells at the local port, and buys the cheapest
upgrade they can afford. Travel time comes out of the fishing time, which is the whole point —
it is what makes map size a balance number rather than a cosmetic one.
"""
import json, sys, io

d = json.load(io.open("Assets/Resources/GameData/game-data.json", encoding="utf-8"))
t, e = d["tuning"], d["economy"]
FISH = {f["id"]: f for f in d["fish"] if f.get("atk", 0) == 0}
EVIL = [f for f in d["fish"] if f.get("atk", 0)]
PORTS = d["ports"]
BANDS = {b["id"]: b for b in d["bands"]}
ZONES = {z["index"]: z for z in d["zones"]}
UP = {}
for u in d["upgrades"]:
    UP.setdefault(u["branch"], []).append(u["cost"])
for k in UP: UP[k].sort()

DAY = t["DaySeconds"]
GAP, JIT, PPU = t["DockGap"], (1 + t["DockGapVarMax"]) / 2, t["WorldScrollPpu"]
SESSION = 90 * 60

# Port x positions, and the zone each port terminates.
px = [6.0]
for z in d["zones"]:
    px.append(px[-1] + GAP * z["gapMul"] * JIT)


def zone_centre(zi):
    return (px[zi - 1] + px[zi]) / 2


MILE = {}
for u in d["upgrades"]:
    if u.get("milestone"): MILE.setdefault(u["branch"], []).append(u["level"])
GAIN = e.get("MilestoneGain", 1.5)


def mile_mul(branch, level):
    m = 1.0
    for lv in MILE.get(branch, []):
        if level >= lv: m *= GAIN
    return m


def speed(engine, tier):
    return t["MaxSpeed"] * (1 + engine * e["engineSpeedPerLevel"] * mile_mul("engine", engine)
                            + tier * e["tierBoatSpeedBonus"])


def port_hop(zi, engine, tier):
    """Fishing happens mid-zone but selling happens at the port on its edge, so every cycle pays a
    round trip out and back. Without this the engine branch was worth literally nothing once the
    player settled in zone 9 and stopped travelling between zones."""
    return (GAP * ZONES[zi]["gapMul"] * JIT) / speed(engine, tier)


def wreck_odds(zi, hull, tier):
    """Chance the cycle ends on the rocks. Wreck() clears the hold, so this is the real reason to buy
    hull -- the repair bill never was: repairs are billed per missing HP, so more max HP changed
    nothing at all about what you paid."""
    port = PORTS[min(zi, len(PORTS) - 1)]
    worst = sum(port.get("obs_" + o["id"], 0) * o["damage"] for o in d["obstacles"])
    hm = mile_mul("hull", hull)
    hp = e["startHullHp"] + hull * e["hullHpPerLevel"] * hm + tier * e["tierHullHpBonus"]
    armor = hull * e["hullArmorPerLevel"] * hm + tier * e["tierArmorBonus"]
    taken = worst * .35 / (1 + armor)
    return max(0.0, min(.85, (taken / max(hp, 1) - .45) * 1.6))


def bands_for(tier):
    """Depth is gated by the SHIP now, not by the hull branch: New Ship is what opens the next band."""
    return ["A", "B", "C"][: min(tier + 1, 3)]


LEVELS_PER_TIER, MAX_TIER = 4, 3
NEW_SHIP = t.get("NewShipCosts", [900, 3000, 6000])


def level_cap(tier):
    return min(tier + 1, MAX_TIER) * LEVELS_PER_TIER


def region_value(zi, bid):
    """Average coin value of one catch in this region, at this zone's port."""
    b = BANDS[bid]
    port = PORTS[min(zi, len(PORTS) - 1)]
    tot, n = 0.0, 0
    for f in FISH.values():
        if not (f["minDepth"] < b["bottom"] and f["maxDepth"] > b["top"]):
            continue
        steps = max(0.0, f["rarity"] - 1)
        w = (0.45 ** steps) * ((1 + (b["rarityBias"] + ZONES[zi]["rarityBias"]) * 0.55) ** steps)
        tot += w * f["value"] * port.get("price_" + f["id"], 1.0)
        n += w
    return (tot / n * 1.15) if n else 0.0


def cast_seconds(bid, hook=0, tier=0):
    """Descend to the middle of the band, fight, reel back.

    Hook level was missing here entirely, so the whole hook branch was worth nothing to this sim while
    the value chart rated it the strongest buy in the game. It scales BOTH ends: damage shortens the
    fight, hook speed shortens the sink and the reel."""
    b = BANDS[bid]
    depth = (b["top"] + b["bottom"]) / 2
    hm = mile_mul("hook", hook)
    hs = 1 + hook * e["hookSpeedPerLevel"] * hm + tier * e["tierHookSpeedBonus"]
    dm = 1 + hook * e["hookDamagePerLevel"] * hm + tier * e["tierDamageBonus"]
    return depth / (t["HookSinkMax"] * hs) + 6.0 / dm + depth / (t["HookRetract"] * hs)


def repair_bill(zi, hull_lv):
    """Coins lost to the shipwright. The optimal-play sim ignored this, but obstacles are unavoidable
    at speed and repair is charged per missing HP, so it is a real drain on every cycle spent far out."""
    port = PORTS[min(zi, len(PORTS) - 1)]
    worst = sum(port.get("obs_" + o["id"], 0) * o["damage"] for o in d["obstacles"])
    hm = mile_mul("hull", hull_lv)
    hp = e["startHullHp"] + hull_lv * e["hullHpPerLevel"] * hm
    armor = hull_lv * e["hullArmorPerLevel"] * hm
    taken = min(hp * .55, worst * .35 / (1 + armor))   # armour is what the hull branch actually buys
    return taken * e["repairCostPerMissingHp"]


# Quest rewards land on the same milestones the arc already gates on, so folding them in as a
# flat income lift is close enough to check they do not collapse the pacing.
QUESTBONUS = 1.0


# Per-region stock, mirroring FishStock. The player now rotates: each cycle they fish the richest
# region they can reach, and that region thins while the ones they left behind recover.
STOCK = {}
STUBBORN = False
DEPLETE, REGEN, MINSTOCK = .04, .001, .15


def stock_of(zi, bid):
    return STOCK.get((zi, bid), 1.0)


def run(efficiency=1.0, repairs=False, deplete=False):
    STOCK.clear()
    coins, clock = 0.0, 0.0
    lv = {"hook": 0, "hold": 0, "engine": 0, "hull": 0}
    tier = 0
    at = 1
    log = []
    while clock < SESSION:
        best = max(bands_for(tier), key=lambda b: region_value(min(at + 2, 9), b))
        # Where do we want to be? The richest zone we can reach without burning the whole day.
        target = at
        for zi in range(at, 10 if not STUBBORN or at < 5 else at + 1):
            travel = abs(zone_centre(zi) - zone_centre(at)) / speed(lv["engine"], tier)
            if travel > DAY * 0.45:
                break
            if region_value(zi, best) > region_value(target, best):
                target = zi
        travel = abs(zone_centre(target) - zone_centre(at)) / speed(lv["engine"], tier)
        at = target
        fishing = max(0.0, DAY - travel - port_hop(at, lv["engine"], tier))
        # Pick the richest reachable band here — that is the choice the mechanic is meant to create.
        if STUBBORN:
            band = max(bands_for(tier), key=lambda b: region_value(at, b))
        else:
            band = max(bands_for(tier),
                       key=lambda b: region_value(at, b) * (stock_of(at, b) if deplete else 1.0))
        cap = (e["basketBaseCapacity"] + lv["hold"] * e["holdCapacityPerLevel"] * mile_mul("hold", lv["hold"])
               + tier * e["tierCapacityBonus"])
        catches = min(fishing / cast_seconds(band, lv["hook"], tier), cap)
        if deplete:
            st = stock_of(at, band)
            catches *= st                       # a thin region simply has less to hook
            STOCK[(at, band)] = max(MINSTOCK, st - catches * DEPLETE)
        haul = catches * region_value(at, band) * efficiency * QUESTBONUS
        if repairs: haul *= (1.0 - wreck_odds(at, lv["hull"], tier))   # a wreck loses the whole hold
        coins += haul
        if repairs: coins = max(0.0, coins - repair_bill(at, lv["hull"] + tier * 4))
        # A player who works out that the night table exists instead of sleeping through it. Nothing in
        # the game points at this -- it is left to be discovered.
        #
        # There is ONE hold, shared by everything caught: PlayerSave.AddForced is the single entry point
        # and evil fish go into the same cargo list as the rest. Night is therefore not a second haul --
        # it is the same haul, with the leftover space in it. An earlier pass here modelled night as a
        # fresh full hold and concluded the whole game could be finished in 16 minutes; that was this bug,
        # not the design.
        night = 105 if tier == 0 else 150 if tier == 1 else 180
        if NIGHTFISH:
            # NIGHTONLY: the optimal night player does not fish the day at all -- one hold of piranha
            # beats a hold of bream several times over, so day casts just waste the space. This is the
            # worst case the night balance actually has to survive, not the polite day-then-night player.
            if NIGHTONLY:
                coins -= catches * region_value(at, band) * efficiency * QUESTBONUS
                catches = 0.0
            room = max(0.0, cap - catches)                       # what the day left empty
            # Night fish obey the same depth window as day fish: a species only shows up in a band that
            # overlaps its min/max depth. Reading only rarity here made minDepth look like it did nothing.
            nb = BANDS[band]
            # The kraken is NOT ambient night catch any more: it arrives as a set piece in zones 8-9,
            # six tentacles at once, and only after a warning. Counting it as ordinary night stock made
            # this sim read deep nights as far richer than they are.
            # Zone gates apply too -- a species locked to the far water is simply not here.
            reach = [f for f in EVIL
                     if f["id"] != "kraken"
                     and f["minDepth"] < nb["bottom"] and f["maxDepth"] > nb["top"]
                     and f.get("minZone", 1) <= at <= f.get("maxZone", 9)
                     and f["rarity"] <= tier + 2]
            # An empty band just means a quiet night -- no night income. It must NOT skip the rest of the
            # cycle: an earlier version used `continue` here and silently skipped the buying phase too,
            # which read as "night fishing is worse than sleeping" when really nobody was shopping.
            val = sum(f["value"] for f in reach) / len(reach) if reach else 0.0
            # Night fish are not day fish with a bigger price tag: they carry 20 / 80 / 240 HP against a
            # day average near 39, so each one is a longer fight. Model the fight from HP rather than
            # reusing the day's flat 6s, or "make them harder" cannot show up as anything at all.
            DAY_HP = sum(f.get("hp", 30) for f in FISH.values()) / max(len(FISH), 1)
            hp = (sum(f.get("hp", 30) for f in reach) / len(reach) if reach else 30) * NIGHTHP
            b = BANDS[band]; depth = (b["top"] + b["bottom"]) / 2
            hm = mile_mul("hook", lv["hook"])
            hs = 1 + lv["hook"] * e["hookSpeedPerLevel"] * hm + tier * e["tierHookSpeedBonus"]
            dm = 1 + lv["hook"] * e["hookDamagePerLevel"] * hm + tier * e["tierDamageBonus"]
            ncast = depth / (t["HookSinkMax"] * hs) + 6.0 * (hp / DAY_HP) / dm + depth / (t["HookRetract"] * hs)
            # Head count, not just density: only so many hunters exist in this zone at once, so a night
            # cannot yield more than the pack can replace. Zone 1 holds one -- see GameCatalog.EvilAliveAt.
            alive = max(1, round(e.get("evilAliveZone1", 1) + (at - 1) * e.get("evilAlivePerZone", .5)))
            respawn = night / ncast * NIGHTRATE
            ncatch = min(respawn, alive * EVILTURNS, room) if reach else 0.0
            coins += ncatch * val * efficiency * QUESTBONUS * (1.0 - wreck_odds(at, lv["hull"], tier))
            # Staying out costs hull. Every night fish near the boat bites on its own timer, and repairs
            # are billed per missing HP -- this was missing entirely, so night looked like free money.
            atk = (sum(f["atk"] for f in reach) / len(reach) if reach else 0.0) * NIGHTATK
            every = sum(f["attackEvery"] for f in reach) / len(reach) if reach else 99.0
            hullm = mile_mul("hull", lv["hull"])
            armor = lv["hull"] * e["hullArmorPerLevel"] * hullm + tier * e["tierArmorBonus"]
            maxhp = e["startHullHp"] + lv["hull"] * e["hullHpPerLevel"] * hullm + tier * e["tierHullHpBonus"]
            near = max(1.0, 3.0 * NIGHTRATE)
            taken = min(maxhp * .9, night / every * atk * near / 3.0 / (1 + armor))
            coins = max(0.0, coins - taken * e["repairCostPerMissingHp"])
        cycle = DAY + night
        clock += cycle
        if deplete:
            for k in list(STOCK):
                STOCK[k] = min(1.0, STOCK[k] + REGEN * cycle)

        bought = []
        while True:
            cap = level_cap(tier)
            # New Ship first when it is available: it is the only thing that opens water, and every
            # branch is stuck at the cap until it is bought.
            if tier < MAX_TIER and all(lv[k] >= cap for k in lv):
                cost = NEW_SHIP[tier] if tier < len(NEW_SHIP) else 10 ** 9
                if cost > coins: break
                coins -= cost; tier += 1; bought.append(f"SHIP-{'ABCD'[tier]}*")
                continue
            opts = [(UP[k][lv[k]], k) for k in lv if lv[k] < min(cap, len(UP[k]))]
            opts = [o for o in opts if o[0] <= coins]
            if not opts: break
            cost, k = min(opts)
            coins -= cost; lv[k] += 1; bought.append(f"{k}{lv[k]}")
        log.append((clock / 60, at, band, catches, coins, dict(lv), bought))
    return log, lv, tier


import sys
EFF = float(sys.argv[1]) if len(sys.argv) > 1 else 1.0
REP = len(sys.argv) > 2
QUESTBONUS = 1.30 if len(sys.argv) > 3 else 1.0
DEP = "d" in sys.argv
STUBBORN = "s" in sys.argv
NIGHTFISH = "n" in sys.argv
# Default to the game's own night thinning; "nrX" only overrides it for experiments.
NIGHTRATE = float(next((a[2:] for a in sys.argv if a.startswith("nr")),
                       t.get("EvilDensityMul", .45) / .45))
NIGHTHP = float(next((a[2:] for a in sys.argv if a.startswith("nh")), 1.0))
NIGHTONLY = "no" in sys.argv
NIGHTATK = float(next((a[2:] for a in sys.argv if a.startswith("na")), 1.0))
# How many times over a night the pack can be cleared and refill.
EVILTURNS = float(next((a[2:] for a in sys.argv if a.startswith("et")), 2.0))
log, lv, tier = run(EFF, REP, DEP)
print(f"hieu suat {EFF:.0%} | sua tau {'CO' if REP else 'KHONG'} | can kiet {'CO' if DEP else 'KHONG'} | {'CAM RE' if STUBBORN else 'di chuyen'} | tong nang cap {sum(sum(v) for v in UP.values())} xu")
print(f"{'phut':>5s} {'vung':>4s} {'tang':>4s} {'ca':>5s} {'xu con':>8s}  mua")
for m, z, b, c, coins, l, bought in log:
    if bought or int(m) % 15 < 5:
        print(f"{m:5.0f} {z:4d} {b:>4s} {c:5.1f} {coins:8.0f}  {' '.join(bought)}")
print(f"\nket thuc: hook{lv['hook']} hold{lv['hold']} engine{lv['engine']} hull{lv['hull']}"
      f"  /  toi da {len(UP['hook'])}/{len(UP['hold'])}/{len(UP['engine'])}/{len(UP['hull'])}")
