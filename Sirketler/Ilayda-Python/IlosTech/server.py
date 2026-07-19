from __future__ import annotations

import json
import math
import socketserver
import statistics
import threading
import time
import uuid
from collections import Counter, OrderedDict
from dataclasses import dataclass, field
from typing import Any, Callable

HOST = "0.0.0.0"
PORT = 7004
MAX_MESSAGE_BYTES = 65_536
MAX_TEXT_BYTES = 50_000
MAX_ARRAY_ITEMS = 10_000
REPLAY_CACHE_SIZE = 5_000

PROTOCOL_VERSION = "0.1"
COMPANY_ID = "ilayda-ilos-tech"
COMPANY_NAME = "İlos Tech"
SERVER_VERSION = "1.0.0"
SECURITY_ERROR = "GUVENLIK_REDDI"


class ServiceError(Exception):
    def __init__(self, code: str, message: str) -> None:
        super().__init__(message)
        self.code = code
        self.message = message


@dataclass(frozen=True)
class ServiceDefinition:
    service_id: str
    version: str
    price: float
    capacity: int
    handler: Callable[[dict[str, Any]], dict[str, Any]]


@dataclass
class PlatformState:
    lock: threading.RLock = field(default_factory=threading.RLock)
    profiles: dict[str, dict[str, Any]] = field(default_factory=dict)
    posts: list[dict[str, Any]] = field(default_factory=list)
    post_reactions: dict[str, Counter[str]] = field(default_factory=dict)
    mails: list[dict[str, Any]] = field(default_factory=list)
    attachments: list[dict[str, Any]] = field(default_factory=list)


STATE = PlatformState()
REPLAY_LOCK = threading.Lock()
PROCESSED_JOBS: OrderedDict[str, None] = OrderedDict()
CONNECTION_LOCK = threading.Lock()
ACTIVE_CONNECTIONS = 0
LAST_FINANCE: dict[str, Any] = {}


def require_object(value: Any, name: str = "istek") -> dict[str, Any]:
    if not isinstance(value, dict):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"{name} JSON nesnesi olmalıdır.")
    return value


def require_string(data: dict[str, Any], key: str, *, min_len: int = 1, max_len: int = 2_000) -> str:
    value = data.get(key)
    if not isinstance(value, str):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"{key} metin olmalıdır.")
    value = value.strip()
    if not min_len <= len(value) <= max_len:
        raise ServiceError(
            "ISTEK_VERISI_GECERSIZ",
            f"{key} uzunluğu {min_len}-{max_len} arasında olmalıdır.",
        )
    if len(value.encode("utf-8")) > MAX_TEXT_BYTES:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"{key} byte sınırını aşıyor.")
    return value


def optional_string(data: dict[str, Any], key: str, default: str = "", max_len: int = 2_000) -> str:
    value = data.get(key, default)
    if value is None:
        return default
    if not isinstance(value, str):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"{key} metin olmalıdır.")
    value = value.strip()
    if len(value) > max_len or len(value.encode("utf-8")) > MAX_TEXT_BYTES:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"{key} çok uzun.")
    return value


def require_numbers(data: dict[str, Any], *, allow_empty: bool) -> list[float | int]:
    values = data.get("sayilar")
    if not isinstance(values, list):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "sayilar dizi olmalıdır.")
    if len(values) > MAX_ARRAY_ITEMS:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "sayilar azami eleman sınırını aşıyor.")
    if not allow_empty and not values:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "sayilar boş olamaz.")
    if any(isinstance(value, bool) or not isinstance(value, (int, float)) for value in values):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "sayilar yalnız sonlu sayılardan oluşmalıdır.")
    if any(not math.isfinite(float(value)) for value in values):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "NaN ve sonsuz değer kabul edilmez.")
    return values


def split_words(text: str) -> list[str]:
    words: list[str] = []
    current: list[str] = []
    for char in text:
        if char in " \t\r\n":
            if current:
                words.append("".join(current))
                current.clear()
        else:
            current.append(char)
    if current:
        words.append("".join(current))
    return words


def core_sum(data: dict[str, Any]) -> dict[str, Any]:
    return {"sonuc": sum(require_numbers(data, allow_empty=True))}


def core_product(data: dict[str, Any]) -> dict[str, Any]:
    result: int | float = 1
    for value in require_numbers(data, allow_empty=False):
        result *= value
    return {"sonuc": result}


def core_average(data: dict[str, Any]) -> dict[str, Any]:
    values = require_numbers(data, allow_empty=False)
    return {"sonuc": math.fsum(float(value) for value in values) / len(values)}


def core_word_count(data: dict[str, Any]) -> dict[str, Any]:
    text = optional_string(data, "metin", max_len=MAX_TEXT_BYTES)
    return {"sonuc": len(split_words(text))}


def core_character_count(data: dict[str, Any]) -> dict[str, Any]:
    text = optional_string(data, "metin", max_len=MAX_TEXT_BYTES)
    # Motor/.NET String.Length ile uyumlu UTF-16 kod birimi sayısı.
    return {"sonuc": len(text.encode("utf-16-le")) // 2}


def core_median(data: dict[str, Any]) -> dict[str, Any]:
    values = [float(value) for value in require_numbers(data, allow_empty=False)]
    return {"sonuc": statistics.median(values)}


def core_stddev(data: dict[str, Any]) -> dict[str, Any]:
    values = [float(value) for value in require_numbers(data, allow_empty=False)]
    mean = math.fsum(values) / len(values)
    variance = math.fsum((value - mean) ** 2 for value in values) / len(values)
    return {"sonuc": math.sqrt(variance)}


def core_sort(data: dict[str, Any]) -> dict[str, Any]:
    values = require_numbers(data, allow_empty=True)
    direction = require_string(data, "yon", min_len=5, max_len=6).lower()
    if direction not in {"artan", "azalan"}:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "yon yalnız artan veya azalan olabilir.")
    return {"sonuc": sorted(values, reverse=direction == "azalan")}


def core_prime_factors(data: dict[str, Any]) -> dict[str, Any]:
    value = data.get("sayi")
    if isinstance(value, bool) or not isinstance(value, int) or value < 2:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "sayi en az 2 olan tam sayı olmalıdır.")
    factors: list[int] = []
    divisor = 2
    remaining = value
    while divisor * divisor <= remaining:
        while remaining % divisor == 0:
            factors.append(divisor)
            remaining //= divisor
        divisor = 3 if divisor == 2 else divisor + 2
    if remaining > 1:
        factors.append(remaining)
    return {"sonuc": factors}


def core_frequency(data: dict[str, Any]) -> dict[str, Any]:
    text = optional_string(data, "metin", max_len=MAX_TEXT_BYTES)
    return {"sonuc": dict(sorted(Counter(word.lower() for word in split_words(text)).items()))}


def auth_validate(data: dict[str, Any]) -> dict[str, Any]:
    user_id = require_string(data, "kullaniciKimligi", max_len=120)
    with STATE.lock:
        profile = STATE.profiles.setdefault(
            user_id,
            {
                "kullaniciKimligi": user_id,
                "gorunenAd": optional_string(data, "gorunenAd", user_id, 120) or user_id,
                "biyografi": "",
                "olusturulmaZamani": time.time(),
            },
        )
    return {"sonuc": {"gecerli": True, "kullanici": profile}}


def social_profile(data: dict[str, Any]) -> dict[str, Any]:
    user_id = require_string(data, "kullaniciKimligi", max_len=120)
    with STATE.lock:
        profile = STATE.profiles.setdefault(
            user_id,
            {
                "kullaniciKimligi": user_id,
                "gorunenAd": user_id,
                "biyografi": "",
                "olusturulmaZamani": time.time(),
            },
        )
        post_count = sum(1 for post in STATE.posts if post["kullaniciKimligi"] == user_id)
        return {"sonuc": {**profile, "gonderiSayisi": post_count}}


def social_create_post(data: dict[str, Any]) -> dict[str, Any]:
    user_id = require_string(data, "kullaniciKimligi", max_len=120)
    text = require_string(data, "metin", max_len=2_000)
    post = {
        "gonderiKimligi": f"ilos-post-{uuid.uuid4().hex}",
        "kullaniciKimligi": user_id,
        "metin": text,
        "olusturulmaZamani": time.time(),
        "etkilesimler": {},
    }
    with STATE.lock:
        STATE.posts.append(post)
    return {"sonuc": post}


def social_feed(data: dict[str, Any]) -> dict[str, Any]:
    limit = data.get("limit", 20)
    if isinstance(limit, bool) or not isinstance(limit, int) or not 1 <= limit <= 100:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "limit 1-100 arasında tam sayı olmalıdır.")
    with STATE.lock:
        posts = [dict(post) for post in reversed(STATE.posts[-limit:])]
        for post in posts:
            post["etkilesimler"] = dict(STATE.post_reactions.get(post["gonderiKimligi"], Counter()))
    return {"sonuc": posts}


def social_reaction(data: dict[str, Any]) -> dict[str, Any]:
    user_id = require_string(data, "kullaniciKimligi", max_len=120)
    post_id = require_string(data, "gonderiKimligi", max_len=120)
    reaction = require_string(data, "tur", max_len=32).lower()
    if reaction not in {"begen", "alkis", "destek", "geri-al"}:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "Desteklenmeyen etkileşim türü.")
    with STATE.lock:
        if not any(post["gonderiKimligi"] == post_id for post in STATE.posts):
            raise ServiceError("KAYIT_BULUNAMADI", "Gönderi bulunamadı.")
        counter = STATE.post_reactions.setdefault(post_id, Counter())
        key = f"{reaction}:{user_id}"
        if reaction == "geri-al":
            for existing in list(counter):
                if existing.endswith(f":{user_id}"):
                    del counter[existing]
        else:
            for existing in list(counter):
                if existing.endswith(f":{user_id}"):
                    del counter[existing]
            counter[key] = 1
        summary = Counter(item.split(":", 1)[0] for item in counter)
    return {"sonuc": {"gonderiKimligi": post_id, "etkilesimler": dict(summary)}}


def social_comment(data: dict[str, Any]) -> dict[str, Any]:
    post_id = require_string(data, "gonderiKimligi", max_len=120)
    user_id = require_string(data, "kullaniciKimligi", max_len=120)
    text = require_string(data, "metin", max_len=800)
    with STATE.lock:
        if not any(post["gonderiKimligi"] == post_id for post in STATE.posts):
            raise ServiceError("KAYIT_BULUNAMADI", "Gönderi bulunamadı.")
    return {"sonuc": {"yorumKimligi": f"ilos-comment-{uuid.uuid4().hex}", "gonderiKimligi": post_id, "kullaniciKimligi": user_id, "metin": text}}


def social_search(data: dict[str, Any]) -> dict[str, Any]:
    query = require_string(data, "sorgu", max_len=120).casefold()
    with STATE.lock:
        result = [dict(post) for post in reversed(STATE.posts) if query in post["metin"].casefold()][:50]
    return {"sonuc": result}


def mail_send(data: dict[str, Any]) -> dict[str, Any]:
    sender = require_string(data, "gonderen", max_len=160)
    recipient = require_string(data, "alici", max_len=160)
    subject = require_string(data, "konu", max_len=240)
    body = optional_string(data, "govde", max_len=20_000)
    spam = spam_check({"konu": subject, "govde": body})["sonuc"]
    mail = {
        "epostaKimligi": f"ilos-mail-{uuid.uuid4().hex}",
        "gonderen": sender,
        "alici": recipient,
        "konu": subject,
        "govde": body,
        "spamPuani": spam["spamPuani"],
        "spam": spam["spam"],
        "gonderilmeZamani": time.time(),
    }
    with STATE.lock:
        STATE.mails.append(mail)
    return {"sonuc": {"epostaKimligi": mail["epostaKimligi"], "teslimDurumu": "teslim-edildi", "spam": mail["spam"]}}


def mail_inbox(data: dict[str, Any]) -> dict[str, Any]:
    user = require_string(data, "kullaniciKimligi", max_len=160)
    limit = data.get("limit", 30)
    if isinstance(limit, bool) or not isinstance(limit, int) or not 1 <= limit <= 100:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "limit 1-100 arasında olmalıdır.")
    with STATE.lock:
        mails = [dict(mail) for mail in reversed(STATE.mails) if mail["alici"].casefold() == user.casefold()][:limit]
    return {"sonuc": mails}


def mail_search(data: dict[str, Any]) -> dict[str, Any]:
    user = require_string(data, "kullaniciKimligi", max_len=160)
    query = require_string(data, "sorgu", max_len=240).casefold()
    with STATE.lock:
        mails = [
            dict(mail)
            for mail in reversed(STATE.mails)
            if mail["alici"].casefold() == user.casefold()
            and query in f"{mail['gonderen']} {mail['konu']} {mail['govde']}".casefold()
        ][:100]
    return {"sonuc": mails}


def spam_check(data: dict[str, Any]) -> dict[str, Any]:
    subject = optional_string(data, "konu", max_len=240)
    body = optional_string(data, "govde", max_len=20_000)
    text = f"{subject} {body}".casefold()
    signals = ["bedava", "hemen kazan", "şifre", "kredi kartı", "acil tıkla", "ödül", "kripto fırsat"]
    score = min(100, sum(20 for signal in signals if signal in text) + (15 if text.count("!") >= 5 else 0))
    return {"sonuc": {"spamPuani": score, "spam": score >= 40, "nedenler": [signal for signal in signals if signal in text]}}


def mail_attachment(data: dict[str, Any]) -> dict[str, Any]:
    mail_id = require_string(data, "epostaKimligi", max_len=120)
    filename = require_string(data, "dosyaAdi", max_len=240)
    size = data.get("boyutByte")
    if isinstance(size, bool) or not isinstance(size, int) or not 0 <= size <= 25_000_000:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "boyutByte 0-25.000.000 arasında olmalıdır.")
    attachment = {"ekKimligi": f"ilos-attachment-{uuid.uuid4().hex}", "epostaKimligi": mail_id, "dosyaAdi": filename, "boyutByte": size}
    with STATE.lock:
        STATE.attachments.append(attachment)
    return {"sonuc": attachment}


def mail_folder(data: dict[str, Any]) -> dict[str, Any]:
    user = require_string(data, "kullaniciKimligi", max_len=160)
    return {"sonuc": {"kullaniciKimligi": user, "klasorler": ["gelen", "gonderilen", "taslak", "spam", "arsiv"]}}


def tlink_identity(data: dict[str, Any]) -> dict[str, Any]:
    application = require_string(data, "uygulamaKimligi", max_len=120)
    version = optional_string(data, "protokolSurumu", "1.0", 30) or "1.0"
    return {"sonuc": {"uygulamaKimligi": application, "protokol": "ilos-ilink", "surum": version, "uyumlu": version.startswith("1.")}}


def tlink_pack(data: dict[str, Any]) -> dict[str, Any]:
    source = require_string(data, "kaynak", max_len=120)
    target = require_string(data, "hedef", max_len=120)
    payload = data.get("veri")
    canonical = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    checksum = sum(canonical.encode("utf-8")) % 1_000_000_007
    return {"sonuc": {"protokol": "ilos-ilink", "surum": "1.0", "kaynak": source, "hedef": target, "veri": payload, "butunluk": checksum}}


def tlink_verify(data: dict[str, Any]) -> dict[str, Any]:
    packet = require_object(data.get("paket"), "paket")
    payload = packet.get("veri")
    canonical = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    expected = sum(canonical.encode("utf-8")) % 1_000_000_007
    return {"sonuc": {"gecerli": packet.get("protokol") == "ilos-ilink" and packet.get("butunluk") == expected, "beklenenButunluk": expected}}


def service(service_id: str, price: float, capacity: int, handler: Callable[[dict[str, Any]], dict[str, Any]]) -> ServiceDefinition:
    return ServiceDefinition(service_id, "1.0", price, capacity, handler)


SERVICE_LIST = [
    service("matematik.topla", 2, 8, core_sum),
    service("matematik.carp", 3, 8, core_product),
    service("veri.ortalama-hesapla", 4, 8, core_average),
    service("metin.kelime-say", 2, 8, core_word_count),
    service("metin.karakter-say", 2, 8, core_character_count),
    service("veri.medyan-hesapla", 5, 7, core_median),
    service("veri.standart-sapma", 6, 7, core_stddev),
    service("dizi.sirala", 5, 7, core_sort),
    service("matematik.asal-carpanlar", 7, 6, core_prime_factors),
    service("metin.frekans-analizi", 5, 7, core_frequency),
    service("ilos.kimlik.dogrula", 1.5, 16, auth_validate),
    service("ilos.isosyal.profil.getir", 2, 14, social_profile),
    service("ilos.isosyal.gonderi.olustur", 3, 12, social_create_post),
    service("ilos.isosyal.akisi.getir", 2.5, 14, social_feed),
    service("ilos.isosyal.etkilesim", 2, 16, social_reaction),
    service("ilos.isosyal.yorum", 2, 14, social_comment),
    service("ilos.isosyal.arama", 2.5, 12, social_search),
    service("ilos.imail.gonder", 3, 14, mail_send),
    service("ilos.imail.gelen-kutusu", 2, 16, mail_inbox),
    service("ilos.imail.ara", 2.5, 14, mail_search),
    service("ilos.imail.spam-kontrol", 2, 18, spam_check),
    service("ilos.imail.ek-yukle", 2.5, 12, mail_attachment),
    service("ilos.imail.klasor", 1.5, 18, mail_folder),
    service("ilos.ilink.kimlik", 1, 20, tlink_identity),
    service("ilos.ilink.paketle", 1.5, 20, tlink_pack),
    service("ilos.ilink.dogrula", 1, 20, tlink_verify),
]
SERVICES = {(item.service_id, item.version): item for item in SERVICE_LIST}


def application_manifests() -> list[dict[str, Any]]:
    return [
        {
            "uygulamaKimligi": "ilos-isosyal",
            "uygulamaAdi": "İSosyal",
            "surum": "1.0",
            "kategori": "sosyal-medya",
            "aciklama": "İlos Tech tarafından Python ile geliştirilen sosyal medya platformu.",
            "ozellikler": [
                feature("kimlik.dogrula", "ilos.kimlik.dogrula"),
                feature("sosyal.profil.getir", "ilos.isosyal.profil.getir"),
                feature("sosyal.gonderi.olustur", "ilos.isosyal.gonderi.olustur"),
                feature("sosyal.akisi.getir", "ilos.isosyal.akisi.getir"),
                feature("sosyal.etkilesim", "ilos.isosyal.etkilesim"),
                feature("sosyal.yorum", "ilos.isosyal.yorum", False),
                feature("sosyal.arama", "ilos.isosyal.arama", False),
            ],
            "bagimliliklar": [
                {"sirketKimligi": COMPANY_ID, "uygulamaKimligi": "ilos-imail", "asgariSurum": "1.0", "protokolKimligi": "ilos-ilink", "zorunlu": False}
            ],
            "desteklenenProtokoller": ["ilos-ilink"],
        },
        {
            "uygulamaKimligi": "ilos-imail",
            "uygulamaAdi": "İMail",
            "surum": "1.0",
            "kategori": "eposta",
            "aciklama": "Spam denetimi ve arama özellikli Python e-posta platformu.",
            "ozellikler": [
                feature("kimlik.dogrula", "ilos.kimlik.dogrula"),
                feature("eposta.gonder", "ilos.imail.gonder"),
                feature("eposta.gelen-kutusu", "ilos.imail.gelen-kutusu"),
                feature("eposta.ara", "ilos.imail.ara"),
                feature("eposta.spam-kontrol", "ilos.imail.spam-kontrol"),
                feature("eposta.ek-yukle", "ilos.imail.ek-yukle", False),
                feature("eposta.klasor", "ilos.imail.klasor", False),
            ],
            "bagimliliklar": [],
            "desteklenenProtokoller": ["ilos-ilink"],
        },
    ]


def feature(feature_id: str, service_id: str, required: bool = True) -> dict[str, Any]:
    return {"ozellikKimligi": feature_id, "hizmetKimligi": service_id, "hizmetSurumu": "1.0", "zorunlu": required}


def protocol_manifests() -> list[dict[str, Any]]:
    return [
        {
            "protokolKimligi": "ilos-ilink",
            "protokolAdi": "İLink",
            "surum": "1.0",
            "aciklama": "İlos Tech uygulamaları ve diğer şirketler arasında güvenli, sürümlü veri zarfı standardı.",
            "semaKimligi": "ilos-ilink-schema-v1",
            "semaOzeti": "kaynak, hedef, veri, protokol, sürüm ve bütünlük alanlarından oluşan JSON zarfı",
            "yetkinlikler": [
                protocol_capability("kimlik", "ilos.ilink.kimlik"),
                protocol_capability("paketle", "ilos.ilink.paketle"),
                protocol_capability("dogrula", "ilos.ilink.dogrula"),
            ],
            "uyumluProtokoller": ["tunix-tlink"],
        }
    ]


def protocol_capability(capability_id: str, service_id: str) -> dict[str, Any]:
    return {"yetkinlikKimligi": capability_id, "hizmetKimligi": service_id, "hizmetSurumu": "1.0"}


def company_intro(message_id: str) -> dict[str, Any]:
    return {
        "mesajTuru": "sirketTanitim",
        "mesajKimligi": message_id,
        "protokolSurumu": PROTOCOL_VERSION,
        "sirketKimligi": COMPANY_ID,
        "sirketAdi": COMPANY_NAME,
        "sunucuSurumu": SERVER_VERSION,
        "hizmetler": [
            {
                "hizmetKimligi": item.service_id,
                "hizmetSurumu": item.version,
                "birimFiyat": item.price,
                "azamiEszamanliIs": item.capacity,
                "aktif": True,
            }
            for item in SERVICE_LIST
        ],
        "uygulamalar": application_manifests(),
        "ozelProtokoller": protocol_manifests(),
    }


def parse_payload(raw: Any) -> dict[str, Any]:
    if not isinstance(raw, str):
        raise ServiceError("ISTEK_VERISI_GECERSIZ", "istekVerisiJson metin olmalıdır.")
    if len(raw.encode("utf-8")) > MAX_MESSAGE_BYTES:
        raise ServiceError(SECURITY_ERROR, "İstek verisi boyut sınırını aşıyor.")
    try:
        return require_object(json.loads(raw), "istekVerisiJson")
    except json.JSONDecodeError as exc:
        raise ServiceError("ISTEK_VERISI_GECERSIZ", f"İstek JSON'u ayrıştırılamadı: {exc.msg}") from exc


def is_attack(payload: dict[str, Any]) -> bool:
    probe = payload.get("_guvenlikSinamasi")
    return isinstance(probe, dict) and probe.get("etiket") == "motor-saldiri-v1"


def remember_job(job_id: str) -> bool:
    with REPLAY_LOCK:
        if job_id in PROCESSED_JOBS:
            return False
        PROCESSED_JOBS[job_id] = None
        PROCESSED_JOBS.move_to_end(job_id)
        while len(PROCESSED_JOBS) > REPLAY_CACHE_SIZE:
            PROCESSED_JOBS.popitem(last=False)
        return True


def execute_job(message: dict[str, Any]) -> dict[str, Any]:
    started = time.perf_counter_ns()
    request_id = str(message.get("istekKimligi", ""))
    job_id = str(message.get("isKimligi", ""))
    service_id = str(message.get("hizmetKimligi", ""))
    version = str(message.get("hizmetSurumu", ""))

    try:
        if not request_id or not job_id:
            raise ServiceError("ISTEK_VERISI_GECERSIZ", "İstek ve iş kimliği zorunludur.")
        payload = parse_payload(message.get("istekVerisiJson"))
        if is_attack(payload):
            raise ServiceError(SECURITY_ERROR, "Motor imzalı kötü niyetli istek İlos güvenlik katmanı tarafından engellendi.")
        if not remember_job(job_id):
            raise ServiceError("KOTU_NIYETLI_ISTEK", "Tekrarlanan iş kimliği reddedildi.")
        definition = SERVICES.get((service_id, version))
        if definition is None:
            raise ServiceError("HIZMET_DESTEKLENMIYOR", "Hizmet veya sürüm İlos Tech tarafından sunulmuyor.")
        result = definition.handler(payload)
        return job_result(request_id, job_id, True, result, None, None, started)
    except ServiceError as exc:
        return job_result(request_id, job_id, False, {}, exc.code, exc.message, started)
    except Exception as exc:  # Bağlantı işleyicisini ve ana sunucuyu ayakta tutar.
        return job_result(request_id, job_id, False, {}, "SUNUCU_HATASI", f"İş güvenli biçimde sonlandırıldı: {exc}", started)


def job_result(request_id: str, job_id: str, success: bool, result: dict[str, Any], error_code: str | None, error_message: str | None, started_ns: int) -> dict[str, Any]:
    elapsed_ms = (time.perf_counter_ns() - started_ns) / 1_000_000
    response: dict[str, Any] = {
        "mesajTuru": "isSonucu",
        "istekKimligi": request_id,
        "isKimligi": job_id,
        "sirketKimligi": COMPANY_ID,
        "basarili": success,
        "sonucVerisiJson": json.dumps(result, ensure_ascii=False, separators=(",", ":")),
        "islemSuresiMs": elapsed_ms,
    }
    if error_code is not None:
        response["hataKodu"] = error_code
    if error_message is not None:
        response["hataMesaji"] = error_message
    return response


class IlosRequestHandler(socketserver.StreamRequestHandler):
    def handle(self) -> None:
        global ACTIVE_CONNECTIONS, LAST_FINANCE
        with CONNECTION_LOCK:
            ACTIVE_CONNECTIONS += 1
        try:
            while True:
                raw = self.rfile.readline(MAX_MESSAGE_BYTES + 2)
                if not raw:
                    return
                if len(raw) > MAX_MESSAGE_BYTES + 1 or not raw.endswith(b"\n"):
                    return
                try:
                    message = json.loads(raw.decode("utf-8"))
                    if not isinstance(message, dict):
                        continue
                except (UnicodeDecodeError, json.JSONDecodeError):
                    continue

                message_type = message.get("mesajTuru")
                if message_type == "merhaba":
                    if message.get("protokolSurumu") != PROTOCOL_VERSION:
                        return
                    self.send(company_intro(f"ilos-intro-{uuid.uuid4().hex}"))
                elif message_type == "kayitSonucu":
                    print(f"Kayıt sonucu: {message.get('basarili')} · {message.get('aciklama', '')}")
                elif message_type == "saglikKontrolu":
                    with CONNECTION_LOCK:
                        active = ACTIVE_CONNECTIONS
                    self.send(
                        {
                            "mesajTuru": "saglikSonucu",
                            "mesajKimligi": f"ilos-health-{uuid.uuid4().hex}",
                            "protokolSurumu": PROTOCOL_VERSION,
                            "istekKimligi": message.get("istekKimligi", ""),
                            "durum": "calisiyor",
                            "aktifBaglanti": active,
                            "kuyrukUzunlugu": 0,
                        }
                    )
                elif message_type == "isIstegi":
                    self.send(execute_job(message))
                elif message_type == "finansDurumu":
                    LAST_FINANCE = dict(message)
                    print(
                        "Finans · tick={tick} kasa={cash} net={net}".format(
                            tick=message.get("tickNumarasi", 0),
                            cash=message.get("kasa", 0),
                            net=message.get("netGelir", 0),
                        )
                    )
        finally:
            with CONNECTION_LOCK:
                ACTIVE_CONNECTIONS = max(0, ACTIVE_CONNECTIONS - 1)

    def send(self, message: dict[str, Any]) -> None:
        encoded = (json.dumps(message, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
        if len(encoded) > MAX_MESSAGE_BYTES:
            raise ValueError("Gönderilecek mesaj boyut sınırını aşıyor.")
        self.wfile.write(encoded)
        self.wfile.flush()


class ThreadedIlosServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True
    request_queue_size = 128


def main() -> None:
    print("=" * 56)
    print("İLOS TECH · Python Yazılım Şirketi Sunucusu")
    print(f"Şirket: {COMPANY_NAME} ({COMPANY_ID})")
    print(f"Sürüm: {SERVER_VERSION} · Port: {PORT}")
    print(f"Hizmet: {len(SERVICE_LIST)} · Uygulama: 2 · Protokol: 1")
    print("İSosyal · İMail · İLink motora kod tabanlı manifest olarak ilan edilir.")
    print("=" * 56)
    with ThreadedIlosServer((HOST, PORT), IlosRequestHandler) as server:
        try:
            server.serve_forever(poll_interval=0.25)
        except KeyboardInterrupt:
            print("\nİlos Tech sunucusu kapatılıyor.")
        finally:
            server.shutdown()


if __name__ == "__main__":
    main()
