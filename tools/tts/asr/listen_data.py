"""Builds clips.json for the listening page: texts no take of which Whisper heard correctly (argv: results out).

Items follow the deck's plan order (phrases and sentences after the deck); each carries the Russian gloss,
what Whisper heard in each take and the takes themselves as base64 Opus.
"""
import base64
import json
import pathlib
import sys

from revoice import OUT, SPEECH

SEED = pathlib.Path(__file__).resolve().parents[3] / "src" / "KorTrainer.Infrastructure" / "Seed"
STAGES = {"FirstPhrases": "Первые фразы", "Level1": "Уровень 1", "Level2": "Уровень 2", "TopikPrep": "Подготовка к TOPIK"}


def deck() -> dict[str, tuple[int, str, str]]:
    found: dict[str, tuple[int, str, str]] = {}
    for name in ("words.json", "words-nikl.json"):
        for section in json.loads((SEED / name).read_text(encoding="utf-8"))["sections"]:
            for word in section["words"]:
                found.setdefault(word[0].strip(), (len(found), word[1], f"{STAGES.get(section['stage'], section['stage'])} · {section['topic']}"))
    return found


def clip(path: pathlib.Path) -> str:
    return base64.b64encode(path.read_bytes()).decode("ascii")


def main() -> None:
    rows = [json.loads(l) for l in pathlib.Path(sys.argv[1]).read_text(encoding="utf-8").splitlines()]
    words = deck()
    items = []
    for row in rows:
        if row["best"] is not None:
            continue
        order, gloss, section = words.get(row["text"], (10**6, "", "Фразы и предложения"))
        takes = [{"take": 0, "heard": row["heard"][0] if row["heard"] else row["was"],
                  "audio": clip(SPEECH / "ko-melo" / f"{row['key']}.opus")}]
        for variant in (1, 2, 3):
            path = OUT / f"{row['key']}-v{variant}.opus"
            if path.exists():
                heard = row["heard"][variant] if len(row["heard"]) > variant else ""
                takes.append({"take": variant, "heard": heard, "audio": clip(path)})
        items.append({"key": row["key"], "text": row["text"], "gloss": gloss, "section": section,
                      "order": order, "takes": takes})
    items.sort(key=lambda i: i["order"])
    pathlib.Path(sys.argv[2]).write_text(json.dumps({"items": items}, ensure_ascii=False), encoding="utf-8")
    print(f"{len(items)} texts to listen to, {pathlib.Path(sys.argv[2]).stat().st_size / 1e6:.1f} MB")


if __name__ == "__main__":
    main()
