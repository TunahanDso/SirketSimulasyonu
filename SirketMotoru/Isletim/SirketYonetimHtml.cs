namespace SirketMotoru.Isletim;

public static class SirketYonetimHtml
{
    public const string Icerik = """
<!doctype html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Şirket İşletim Merkezi</title>
<style>
:root{color-scheme:dark;--bg:#07101e;--panel:#0d192a;--panel2:#111f33;--line:#21334e;--text:#edf5ff;--muted:#8fa3bf;--accent:#38e6ad;--accent2:#55a7ff;--bad:#ff6678;--warn:#ffd166;--good:#42e69f}
*{box-sizing:border-box}body{margin:0;background:radial-gradient(circle at top,#12233c 0,#07101e 42%);color:var(--text);font:14px/1.45 system-ui,-apple-system,"Segoe UI",sans-serif;min-height:100vh}button,input,select,textarea{font:inherit}button{cursor:pointer}.hidden{display:none!important}.shell{max-width:1500px;margin:auto;padding:18px}.top{display:flex;justify-content:space-between;align-items:center;gap:16px;margin-bottom:16px}.brand{font-weight:800;letter-spacing:.08em}.brand small{display:block;color:var(--muted);font-weight:500;letter-spacing:0}.live{display:flex;align-items:center;gap:8px;color:var(--muted)}.dot{width:9px;height:9px;border-radius:50%;background:var(--bad)}.dot.ok{background:var(--good);box-shadow:0 0 12px var(--good)}.card{background:rgba(13,25,42,.94);border:1px solid var(--line);border-radius:16px;padding:15px}.login{max-width:430px;margin:10vh auto}.login h1{margin:0 0 8px}.login p{color:var(--muted)}label{display:block;color:var(--muted);margin:10px 0 5px}input,select,textarea{width:100%;background:#081322;color:var(--text);border:1px solid var(--line);border-radius:10px;padding:10px;outline:none}input:focus,select:focus,textarea:focus{border-color:var(--accent2)}textarea{min-height:80px;resize:vertical}.btn{border:0;border-radius:10px;padding:10px 13px;background:var(--accent);color:#032016;font-weight:750}.btn.secondary{background:#203552;color:var(--text)}.btn.danger{background:var(--bad);color:white}.btn.small{padding:7px 10px;font-size:12px}.btn:disabled{opacity:.45;cursor:not-allowed}.full{width:100%;margin-top:14px}.notice{margin:12px 0;padding:10px 12px;border:1px solid var(--line);border-radius:10px;color:var(--muted)}.notice.good{border-color:#205d4d;color:var(--good)}.notice.bad{border-color:#69303c;color:var(--bad)}.metrics{display:grid;grid-template-columns:repeat(6,minmax(125px,1fr));gap:10px;margin-bottom:14px}.metric{background:var(--panel);border:1px solid var(--line);border-radius:14px;padding:12px}.metric span{display:block;color:var(--muted);font-size:12px}.metric b{font-size:19px}.layout{display:grid;grid-template-columns:minmax(0,2fr) minmax(320px,1fr);gap:14px}.stack{display:grid;gap:14px}.section-head{display:flex;justify-content:space-between;align-items:center;gap:12px;margin-bottom:12px}.section-head h2{font-size:15px;margin:0;letter-spacing:.04em}.muted{color:var(--muted)}.tabs{display:flex;flex-wrap:wrap;gap:7px;margin-bottom:14px}.tab{border:1px solid var(--line);background:#0a1626;color:var(--muted);padding:8px 11px;border-radius:999px}.tab.active{background:var(--accent);border-color:var(--accent);color:#032016}.tab-page{display:none}.tab-page.active{display:block}.grid2{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:10px}.item{background:var(--panel2);border:1px solid var(--line);border-radius:12px;padding:12px}.item h3{margin:0 0 6px;font-size:14px}.row{display:flex;justify-content:space-between;align-items:center;gap:10px}.tags{display:flex;flex-wrap:wrap;gap:6px}.tag{border:1px solid var(--line);border-radius:999px;padding:4px 8px;color:var(--muted);font-size:12px}.tag.good{border-color:#245d4d;color:var(--good)}.tag.bad{border-color:#713342;color:var(--bad)}.bar{height:8px;background:#07101e;border-radius:99px;overflow:hidden;margin-top:6px}.bar i{display:block;height:100%;background:var(--accent);border-radius:inherit}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;min-width:720px}th,td{text-align:left;border-bottom:1px solid var(--line);padding:9px 7px}th{color:var(--muted);font-size:12px}.num{text-align:right}.forms{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px}.form-box{border:1px solid var(--line);border-radius:12px;padding:12px}.form-box h3{margin:0 0 8px}.event{border-left:3px solid var(--warn);padding:8px 10px;background:#171a22;margin-bottom:8px}.tx{display:grid;grid-template-columns:70px 110px 1fr auto;gap:8px;padding:7px 0;border-bottom:1px solid var(--line);font-size:12px}.positive{color:var(--good)}.negative{color:var(--bad)}#toast{position:fixed;right:18px;bottom:18px;max-width:420px;padding:12px 15px;border-radius:12px;background:#10233a;border:1px solid var(--line);box-shadow:0 15px 40px #0008;z-index:10}.danger-banner{border:1px solid #78404b;background:#25141a;color:#ffabb4;padding:11px;border-radius:12px;margin-bottom:12px}@media(max-width:1050px){.metrics{grid-template-columns:repeat(3,1fr)}.layout{grid-template-columns:1fr}}@media(max-width:680px){.shell{padding:10px}.metrics,.grid2,.grid3,.forms{grid-template-columns:1fr 1fr}.top{align-items:flex-start}.tx{grid-template-columns:55px 1fr}.tx span:nth-child(3){grid-column:1/-1}.metric b{font-size:16px}}@media(max-width:430px){.metrics,.grid2,.grid3,.forms{grid-template-columns:1fr}}
</style>
</head>
<body>
<div class="shell">
  <div class="top">
    <div class="brand">ŞİRKET İŞLETİM MERKEZİ<small>Altyapı · Finans · Ürünler · Protokoller · Sözleşmeler</small></div>
    <div class="live"><span id="dot" class="dot"></span><span id="connection">BAĞLANTI YOK</span><button id="logout" class="btn secondary small hidden">Çıkış</button></div>
  </div>

  <section id="loginView" class="card login">
    <h1>Şirket hesabına giriş</h1>
    <p>Yalnız kendi şirketinin parasını, yatırımlarını, ürünlerini ve sözleşmelerini yönetebilirsin.</p>
    <label for="username">Kullanıcı adı</label><input id="username" autocomplete="username">
    <label for="password">Parola</label><input id="password" type="password" autocomplete="current-password">
    <button id="login" class="btn full">Giriş yap</button>
    <div id="loginMessage" class="notice">Motor ilk açılışta geçici hesap bilgilerini konsola yazar.</div>
  </section>

  <main id="appView" class="hidden">
    <div id="passwordBanner" class="danger-banner hidden">Geçici parolayı kullanıyorsun. Güvenlik sekmesinden hemen değiştir.</div>
    <section id="metrics" class="metrics"></section>
    <div class="tabs" id="tabs">
      <button class="tab active" data-tab="overview">Genel</button>
      <button class="tab" data-tab="infra">Altyapı</button>
      <button class="tab" data-tab="finance">Banka</button>
      <button class="tab" data-tab="services">Hizmetler</button>
      <button class="tab" data-tab="products">Ürünler</button>
      <button class="tab" data-tab="protocols">Protokoller</button>
      <button class="tab" data-tab="contracts">Sözleşmeler</button>
      <button class="tab" data-tab="security">Güvenlik</button>
    </div>

    <section id="page-overview" class="tab-page active">
      <div class="layout">
        <div class="stack">
          <div class="card"><div class="section-head"><h2>ŞİRKET DURUMU</h2><span id="companyStatus" class="tag"></span></div><div id="scores" class="grid3"></div></div>
          <div class="card"><div class="section-head"><h2>ÜRÜN PORTFÖYÜ</h2><span id="productCount" class="muted"></span></div><div id="productSummary" class="grid2"></div></div>
          <div class="card"><div class="section-head"><h2>AKTİF SÖZLEŞMELER</h2></div><div id="contractSummary"></div></div>
        </div>
        <aside class="stack">
          <div class="card"><div class="section-head"><h2>PİYASA OLAYLARI</h2></div><div id="events"></div></div>
          <div class="card"><div class="section-head"><h2>SON İŞLEMLER</h2></div><div id="transactions"></div></div>
        </aside>
      </div>
    </section>

    <section id="page-infra" class="tab-page"><div class="card"><div class="section-head"><h2>ALTYAPI VE DEPARTMAN YATIRIMLARI</h2><span class="muted">Maliyet her seviyede karesel artar</span></div><div id="investments" class="grid3"></div></div></section>

    <section id="page-finance" class="tab-page">
      <div class="layout">
        <div class="card"><div class="section-head"><h2>KREDİ PAKETLERİ</h2><span id="creditScore" class="tag"></span></div><div id="loanPackages" class="grid2"></div></div>
        <aside class="card"><div class="section-head"><h2>AKTİF KREDİLER</h2></div><div id="activeLoans"></div></aside>
      </div>
    </section>

    <section id="page-services" class="tab-page"><div class="card"><div class="section-head"><h2>HİZMET FİYATLARI VE KAPASİTE</h2><span class="muted">Fiyat değişikliği motor piyasasına anında uygulanır</span></div><div class="table-wrap"><table><thead><tr><th>Hizmet</th><th>Sürüm</th><th class="num">Kapasite</th><th class="num">Mevcut fiyat</th><th>Yeni fiyat</th><th></th></tr></thead><tbody id="services"></tbody></table></div></div></section>

    <section id="page-products" class="tab-page">
      <div class="layout">
        <div class="stack">
          <div class="card"><div class="section-head"><h2>ÜRÜNLERİM</h2><span class="muted">E-posta, sosyal medya, mesajlaşma, bulut ve daha fazlası</span></div><div id="products" class="grid2"></div></div>
        </div>
        <aside class="card">
          <div class="section-head"><h2>YENİ ÜRÜN ÇIKAR</h2></div>
          <label>Ürün adı</label><input id="productName" maxlength="80">
          <label>Kategori</label><select id="productCategory"></select>
          <label>Model</label><select id="productModel"><option value="abonelik">Abonelik</option><option value="kullanim">Kullanım başına</option><option value="freemium">Freemium</option><option value="lisans">Tek seferlik lisans</option></select>
          <div class="grid2"><div><label>Abonelik/lisans ücreti</label><input id="subscriptionPrice" type="number" min="0" step="0.01" value="25"></div><div><label>Kullanım ücreti</label><input id="usagePrice" type="number" min="0" step="0.001" value="0.1"></div></div>
          <label>Arka uç hizmet kimliği (isteğe bağlı)</label><input id="backendService" placeholder="ornegin: mail.gonder">
          <label>Özel protokol (isteğe bağlı)</label><select id="productProtocol"><option value="">Protokol yok</option></select>
          <button id="createProduct" class="btn full">Ürünü piyasaya çıkar</button>
        </aside>
      </div>
    </section>

    <section id="page-protocols" class="tab-page">
      <div class="layout">
        <div class="card"><div class="section-head"><h2>PROTOKOL PAZARI</h2></div><div id="protocols" class="grid2"></div></div>
        <aside class="card">
          <div class="section-head"><h2>ÖZEL PROTOKOL YAYINLA</h2></div>
          <label>Protokol adı</label><input id="protocolName" maxlength="80">
          <label>Sürüm</label><input id="protocolVersion" value="1.0">
          <label>Açıklama</label><textarea id="protocolDescription"></textarea>
          <label>Lisans modeli</label><select id="licenseModel"><option value="acik">Açık</option><option value="tek-sefer">Tek sefer</option><option value="abonelik">Abonelik</option><option value="karma">Karma</option></select>
          <div class="grid2"><div><label>Benimseme bedeli</label><input id="adoptionFee" type="number" min="0" value="0"></div><div><label>Tick lisans bedeli</label><input id="licenseFee" type="number" min="0" value="0"></div></div>
          <button id="createProtocol" class="btn full">Protokolü yayınla</button>
        </aside>
      </div>
    </section>

    <section id="page-contracts" class="tab-page"><div class="layout"><div class="card"><div class="section-head"><h2>AÇIK SÖZLEŞME VE İHALELER</h2></div><div id="contractOffers" class="grid2"></div></div><aside class="card"><div class="section-head"><h2>SÖZLEŞMELERİM</h2></div><div id="contracts"></div></aside></div></section>

    <section id="page-security" class="tab-page">
      <div class="card" style="max-width:580px"><div class="section-head"><h2>HESAP GÜVENLİĞİ</h2></div><p class="muted">Yönetim kapısı yerel ağ içindir. Parolanı kardeşlerinle paylaşma ve portu doğrudan internete açma.</p><label>Mevcut parola</label><input id="oldPassword" type="password"><label>Yeni parola</label><input id="newPassword" type="password"><button id="changePassword" class="btn full">Parolayı değiştir</button></div>
    </section>
  </main>
</div>
<div id="toast" class="hidden" aria-live="polite"></div>
<script>
const state={data:null,refresh:null};
const money=new Intl.NumberFormat("tr-TR",{style:"currency",currency:"TRY",maximumFractionDigits:2});
const num=new Intl.NumberFormat("tr-TR",{maximumFractionDigits:2});
const int=new Intl.NumberFormat("tr-TR",{maximumFractionDigits:0});
const $=id=>document.getElementById(id);
const esc=v=>String(v??"").replace(/[&<>"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
function toast(text,ok=false){const el=$("toast");el.textContent=text;el.className=ok?"notice good":"notice bad";el.classList.remove("hidden");setTimeout(()=>el.classList.add("hidden"),4200)}
async function api(path,method="GET",body){const opt={method,headers:{}};if(body!==undefined){opt.headers["Content-Type"]="application/json";opt.body=JSON.stringify(body)}const r=await fetch(path,opt);let data;try{data=await r.json()}catch{data={basarili:false,aciklama:`HTTP ${r.status}`}}if(!r.ok)throw new Error(data.aciklama||`HTTP ${r.status}`);return data}
function showLogin(message){$("loginView").classList.remove("hidden");$("appView").classList.add("hidden");$("logout").classList.add("hidden");$("dot").classList.remove("ok");$("connection").textContent="GİRİŞ BEKLENİYOR";if(message)$("loginMessage").textContent=message;clearInterval(state.refresh);state.refresh=null}
function showApp(){$("loginView").classList.add("hidden");$("appView").classList.remove("hidden");$("logout").classList.remove("hidden");$("dot").classList.add("ok");$("connection").textContent="YÖNETİM CANLI";if(!state.refresh)state.refresh=setInterval(refresh,2500)}
function metric(label,value,sub=""){return `<div class="metric"><span>${esc(label)}</span><b>${esc(value)}</b>${sub?`<span>${esc(sub)}</span>`:""}</div>`}
function score(label,value){const v=Math.max(0,Math.min(100,Number(value||0)));return `<div class="item"><div class="row"><b>${esc(label)}</b><span>${num.format(v)}</span></div><div class="bar"><i style="width:${v}%"></i></div></div>`}
async function login(){try{const r=await api("/api/giris","POST",{kullaniciAdi:$("username").value,parola:$("password").value});toast(r.aciklama,true);showApp();await refresh()}catch(e){$("loginMessage").textContent=e.message;$("loginMessage").className="notice bad"}}
async function refresh(){try{state.data=await api("/api/durum");render();$("dot").classList.add("ok");$("connection").textContent="YÖNETİM CANLI"}catch(e){if(/oturum|giriş/i.test(e.message)){showLogin(e.message);return}$("dot").classList.remove("ok");$("connection").textContent="BAĞLANTI HATASI"}}
function render(){const d=state.data,s=d.sirket||{},o=d.isletim||{};showApp();$("passwordBanner").classList.toggle("hidden",!d.parolaDegistirilmeli);$("metrics").innerHTML=[metric("Kasa",money.format(s.kasa||0)),metric("Şirket değeri",money.format(o.sirketDegeri||0)),metric("Tahmini hisse",money.format(o.tahminiHisseFiyati||0)),metric("Aktif kullanıcı",int.format(o.toplamAboneSayisi||0)),metric("Toplam borç",money.format(o.toplamBorc||0)),metric("Kredi notu",int.format(o.krediNotu||0),`Tick ${d.tickNumarasi||0}`)].join("");$("companyStatus").textContent=s.bagliMi?"SUNUCU BAĞLI":"SUNUCU ÇEVRİMDIŞI";$("companyStatus").className=`tag ${s.bagliMi?"good":"bad"}`;$("scores").innerHTML=[score("Kod kalitesi",s.kodKalitesiPuani),score("Performans",s.performansPuani),score("Güvenlik",s.guvenlikPuani),score("İtibar",s.itibarPuani),score("Güvenilirlik",s.guvenilirlikPuani),score("Memnuniyet",s.ortalamaMusteriMemnuniyeti)].join("");renderEvents(d.piyasaOlaylari||[]);renderTx(o.sonIslemler||[]);renderInvestments(d.yatirimMagazasi||[],s.kasa||0);renderLoans(d.krediPaketleri||[],o.krediler||[],o.krediNotu||0);renderServices(s.hizmetler||[]);renderProducts(o.urunler||[],d.urunKategorileri||[],d.protokoller||[]);renderProtocols(d.protokoller||[],s.sirketKimligi);renderContracts(d.sozlesmeTeklifleri||[],o.sozlesmeler||[]);}
function renderEvents(items){$("events").innerHTML=items.length?items.map(x=>`<div class="event"><b>${esc(x.baslik)}</b><div class="muted">${esc(x.aciklama)}</div><small>Talep ×${num.format(x.talepCarpani)} · Gider ×${num.format(x.giderCarpani)} · Bitiş ${x.bitisTicki}</small></div>`).join(""):'<div class="muted">Aktif piyasa olayı yok.</div>'}
function renderTx(items){$("transactions").innerHTML=items.length?items.map(x=>`<div class="tx"><span>T${x.tickNumarasi}</span><span>${esc(x.islemTuru)}</span><span>${esc(x.aciklama)}</span><b class="${Number(x.tutar)>=0?"positive":"negative"}">${money.format(x.tutar||0)}</b></div>`).join(""):'<div class="muted">Henüz manuel işlem yok.</div>'}
function renderInvestments(items,cash){$("investments").innerHTML=items.map(x=>`<div class="item"><div class="row"><h3>${esc(x.ad)}</h3><span class="tag">${x.seviye}/${x.azamiSeviye}</span></div><p class="muted">${esc(x.aciklama)}</p><div class="row"><b>${money.format(x.sonrakiMaliyet)}</b><button class="btn small buy-invest" data-id="${esc(x.yatirimTuru)}" ${x.seviye>=x.azamiSeviye||cash<x.sonrakiMaliyet?"disabled":""}>Satın al</button></div></div>`).join("");document.querySelectorAll(".buy-invest").forEach(b=>b.addEventListener("click",()=>act("/api/yatirim",{yatirimTuru:b.dataset.id}))) }
function renderLoans(packages,loans,scoreValue){$("creditScore").textContent=`Kredi notu ${int.format(scoreValue)}`;$("loanPackages").innerHTML=packages.map(x=>`<div class="item"><h3>${esc(x.ad)}</h3><div class="muted">${int.format(x.taksitSayisi)} taksit · her ${x.odemeAraligiTick} tick · tick faiz %${num.format(Number(x.tickFaizOrani)*100)}</div><label>Tutar (${money.format(x.asgariTutar)}–${money.format(x.azamiTutar)})</label><input id="loan-${esc(x.krediTuru)}" type="number" min="${x.asgariTutar}" max="${x.azamiTutar}" value="${x.asgariTutar}"><button class="btn full take-loan" data-id="${esc(x.krediTuru)}" ${scoreValue<x.asgariKrediNotu?"disabled":""}>Kredi çek</button></div>`).join("");document.querySelectorAll(".take-loan").forEach(b=>b.addEventListener("click",()=>act("/api/kredi",{krediTuru:b.dataset.id,tutar:Number($("loan-"+b.dataset.id).value)})));$("activeLoans").innerHTML=loans.filter(x=>x.aktif).length?loans.filter(x=>x.aktif).map(x=>`<div class="item"><div class="row"><b>${esc(x.krediTuru)}</b><span class="tag">${x.kalanTaksit} taksit</span></div><div>Kalan: ${money.format(x.kalanBorc)}</div><div class="muted">Sonraki ödeme: Tick ${x.sonrakiOdemeTicki} · ${money.format(x.taksitTutari)}</div></div>`).join(""):'<div class="muted">Aktif kredi yok.</div>'}
function renderServices(items){$("services").innerHTML=items.length?items.map((x,i)=>`<tr><td><b>${esc(x.hizmetKimligi)}</b></td><td>${esc(x.hizmetSurumu)}</td><td class="num">${int.format(x.azamiEszamanliIs)}</td><td class="num">${money.format(x.birimFiyat)}</td><td><input id="servicePrice-${i}" type="number" min="0.01" step="0.01" value="${x.birimFiyat}"></td><td><button class="btn small service-price" data-index="${i}">Güncelle</button></td></tr>`).join(""):'<tr><td colspan="6" class="muted">Sunucunun ilan ettiği hizmet yok.</td></tr>';document.querySelectorAll(".service-price").forEach(b=>b.addEventListener("click",()=>{const x=items[Number(b.dataset.index)];act("/api/fiyat",{hizmetKimligi:x.hizmetKimligi,hizmetSurumu:x.hizmetSurumu,yeniFiyat:Number($("servicePrice-"+b.dataset.index).value)})}))}
function renderProducts(items,categories,protocols){$("productCount").textContent=`${items.length} ürün`;$("productCategory").innerHTML=categories.map(x=>`<option value="${esc(x)}">${esc(x)}</option>`).join("");$("productProtocol").innerHTML='<option value="">Protokol yok</option>'+protocols.map(x=>`<option value="${esc(x.protokolKimligi)}">${esc(x.protokolAdi)}@${esc(x.surum)}</option>`).join("");const html=items.length?items.map((x,i)=>`<div class="item"><div class="row"><h3>${esc(x.urunAdi)}</h3><span class="tag ${x.aktif?"good":"bad"}">${x.aktif?"AKTİF":"KAPALI"}</span></div><div class="tags"><span class="tag">${esc(x.kategori)}</span><span class="tag">${esc(x.fiyatlandirmaModeli)}</span><span class="tag">Kapasite ${int.format(x.aktifKullaniciSayisi)}/${int.format(x.kullaniciKapasitesi)}</span></div>${score("Ürün kalitesi",x.urunKalitesi)}${score("Ürün memnuniyeti",x.urunMemnuniyeti)}<div class="grid2"><div><label>Abonelik/lisans</label><input id="up-${i}" type="number" min="0" step="0.01" value="${x.abonelikUcreti}"></div><div><label>Kullanım</label><input id="uk-${i}" type="number" min="0" step="0.001" value="${x.kullanimBasinaUcret}"></div></div><div class="row" style="margin-top:8px"><button class="btn secondary small update-product" data-i="${i}">Fiyat/durum</button><button class="btn small capacity-product" data-i="${i}">+500 kapasite</button></div></div>`).join(""):'<div class="muted">Henüz ürün yok.</div>';$("products").innerHTML=html;$("productSummary").innerHTML=html;document.querySelectorAll(".update-product").forEach(b=>b.addEventListener("click",()=>{const x=items[Number(b.dataset.i)];act("/api/urun/guncelle",{urunKimligi:x.urunKimligi,abonelikUcreti:Number($("up-"+b.dataset.i).value),kullanimBasinaUcret:Number($("uk-"+b.dataset.i).value),aktif:!x.aktif})}));document.querySelectorAll(".capacity-product").forEach(b=>b.addEventListener("click",()=>act("/api/urun/kapasite",{urunKimligi:items[Number(b.dataset.i)].urunKimligi,eklenecekKapasite:500})))}
function renderProtocols(items,companyId){$("protocols").innerHTML=items.length?items.map(x=>`<div class="item"><div class="row"><h3>${esc(x.protokolAdi)}@${esc(x.surum)}</h3><span class="tag">${esc(x.lisansModeli)}</span></div><p class="muted">${esc(x.aciklama||"Açıklama yok")}</p><div>Benimseyen: ${int.format((x.benimseyenSirketler||[]).length)} · Lisans geliri: ${money.format(x.toplamLisansGeliri||0)}</div><div class="row" style="margin-top:8px"><span>${money.format(x.benimsemeBedeli)} + ${money.format(x.tickLisansBedeli)}/tick</span>${x.sahipSirketKimligi===companyId?' <span class="tag good">SENİN</span>':`<button class="btn small adopt-protocol" data-id="${esc(x.protokolKimligi)}">Benimse</button>`}</div></div>`).join(""):'<div class="muted">Piyasada özel protokol yok.</div>';document.querySelectorAll(".adopt-protocol").forEach(b=>b.addEventListener("click",()=>act("/api/protokol/benimse",{protokolKimligi:b.dataset.id})))}
function contractHtml(x,button){return `<div class="item"><div class="row"><h3>${esc(x.baslik)}</h3><span class="tag">${esc(x.kategori)}</span></div><div>Gelir: ${money.format(x.tickOdemesi)}/tick · Ceza: ${money.format(x.ihlalCezasi)}</div><div class="tags"><span class="tag">Kalite ≥ ${num.format(x.asgariKalite)}</span><span class="tag">Performans ≥ ${num.format(x.asgariPerformans)}</span><span class="tag">Güvenlik ≥ ${num.format(x.asgariGuvenlik)}</span><span class="tag">Kapasite ≥ ${x.gerekliKapasite}</span></div>${button||""}</div>`}
function renderContracts(offers,contracts){$("contractOffers").innerHTML=offers.length?offers.map(x=>contractHtml(x,`<button class="btn full accept-contract" data-id="${esc(x.teklifKimligi)}">Sözleşmeyi kabul et</button>`)).join(""):'<div class="muted">Şu an açık teklif yok.</div>';$("contracts").innerHTML=contracts.length?contracts.map(x=>contractHtml(x,`<div class="muted">Bitiş ticki ${x.bitisTicki} · İhlal ${x.ihlalSayisi}</div>`)).join(""):'<div class="muted">Sözleşme yok.</div>';$("contractSummary").innerHTML=$("contracts").innerHTML;document.querySelectorAll(".accept-contract").forEach(b=>b.addEventListener("click",()=>act("/api/sozlesme/kabul",{teklifKimligi:b.dataset.id})))}
async function act(path,body){try{const r=await api(path,"POST",body);toast(r.aciklama,true);await refresh()}catch(e){toast(e.message,false)}}
$("login").addEventListener("click",login);$("password").addEventListener("keydown",e=>{if(e.key==="Enter")login()});$("logout").addEventListener("click",async()=>{try{await api("/api/cikis","POST",{})}catch{}showLogin("Çıkış yapıldı.")});$("tabs").addEventListener("click",e=>{const b=e.target.closest("[data-tab]");if(!b)return;document.querySelectorAll(".tab").forEach(x=>x.classList.toggle("active",x===b));document.querySelectorAll(".tab-page").forEach(x=>x.classList.toggle("active",x.id==="page-"+b.dataset.tab))});$("createProduct").addEventListener("click",()=>act("/api/urun/olustur",{urunAdi:$("productName").value,kategori:$("productCategory").value,fiyatlandirmaModeli:$("productModel").value,abonelikUcreti:Number($("subscriptionPrice").value),kullanimBasinaUcret:Number($("usagePrice").value),arkaUcHizmetKimligi:$("backendService").value,arkaUcHizmetSurumu:"1.0",protokolKimligi:$("productProtocol").value}));$("createProtocol").addEventListener("click",()=>act("/api/protokol/olustur",{protokolAdi:$("protocolName").value,surum:$("protocolVersion").value,aciklama:$("protocolDescription").value,lisansModeli:$("licenseModel").value,benimsemeBedeli:Number($("adoptionFee").value),tickLisansBedeli:Number($("licenseFee").value)}));$("changePassword").addEventListener("click",()=>act("/api/parola",{eskiParola:$("oldPassword").value,yeniParola:$("newPassword").value}));refresh().catch(()=>showLogin());
</script>
</body>
</html>
""";
}
