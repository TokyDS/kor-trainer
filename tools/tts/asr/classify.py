"""Sorts the clips Whisper heard wrongly into kinds of mistakes (argv: words.jsonl out.json)."""
import collections
import json
import sys

FIRST = 0xAC00


def hangul(text: str) -> str:
    return "".join(c for c in text if FIRST <= ord(c) <= 0xD7A3)


def parts(s: str) -> tuple[int, int, int]:
    code = ord(s) - FIRST
    return code // 588, code % 588 // 28, code % 28


def without_final(s: str) -> str:
    code = ord(s) - FIRST
    return chr(FIRST + code - code % 28)


def cer(a: str, b: str) -> float:
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1] / max(1, len(b))


def kind(text: str, heard: str) -> str:
    t, h = hangul(text), hangul(heard)
    if not h:
        return "silence"
    if len(h) > 2 * len(t) + 3:
        return "hallucination"
    if len(t) == len(h) and t[:-1] == h[:-1] and parts(t[-1])[2] and without_final(t[-1]) == without_final(h[-1]):
        return "final-lost"  # 한국 → 한구, 가족 → 가조: only the last final differs
    if len(t) == len(h):
        diff = [i for i in range(len(t)) if t[i] != h[i]]
        if len(diff) == 1:
            a, b = parts(t[diff[0]]), parts(h[diff[0]])
            same = sum(x == y for x, y in zip(a, b))
            if same == 2:
                return "one-jamo"   # 의자 → 위자, 경찰 → 병찰
        return "several-syllables"
    if cer(h, t) <= 0.5:
        return "length-differs"
    return "different"


def main() -> None:
    rows = [json.loads(l) for l in open(sys.argv[1], encoding="utf-8")]
    bad = [r for r in rows if r["ok_whisper"] is False]
    groups: dict[str, list] = collections.defaultdict(list)
    for r in bad:
        groups[kind(r["text"], r["whisper"])].append({"text": r["text"], "key": r["key"], "heard": r["whisper"]})
    for k, v in sorted(groups.items(), key=lambda kv: -len(kv[1])):
        print(f"{k}: {len(v)}  e.g. " + ", ".join(f"{x['text']}→{x['heard']}" for x in v[:8]))
    json.dump(groups, open(sys.argv[2], "w", encoding="utf-8"), ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
