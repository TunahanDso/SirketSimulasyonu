namespace SirketMotoru.CanliPano;

internal static class CanliPanoHtml
{
    public const string Icerik = """
<!doctype html>
<html lang="tr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
  <title>Üç Kardeş Canlı Borsa</title>
  <style>
    :root {
      color-scheme: dark;
      --bg:#050914;
      --panel:#0b1220;
      --panel2:#101a2d;
      --line:#1d2a42;
      --text:#e8f0ff;
      --muted:#8ea0bc;
      --green:#35e6a4;
      --red:#ff6174;
      --yellow:#ffd166;
      --blue:#54a8ff;
      --purple:#b68cff;
      --cyan:#42d4f4;
      --shadow:0 18px 55px rgba(0,0,0,.32);
    }
    *{box-sizing:border-box}
    html,body{margin:0;min-height:100%;background:radial-gradient(circle at 15% 0%,#102146 0,transparent 36%),radial-gradient(circle at 85% 10%,#18133e 0,transparent 32%),var(--bg);color:var(--text);font-family:Inter,Segoe UI,Arial,sans-serif}
    body{padding:18px}
    .shell{max-width:1900px;margin:0 auto}
    .topbar{display:flex;gap:16px;align-items:center;justify-content:space-between;margin-bottom:16px;padding:16px 18px;border:1px solid var(--line);border-radius:18px;background:rgba(11,18,32,.88);backdrop-filter:blur(12px);box-shadow:var(--shadow)}
    .brand{display:flex;align-items:center;gap:14px;min-width:0}
    .logo{width:44px;height:44px;border-radius:14px;display:grid;place-items:center;font-weight:900;background:linear-gradient(135deg,var(--green),var(--blue));color:#04131a;box-shadow:0 0 32px rgba(53,230,164,.25)}
    h1{font-size:20px;margin:0;letter-spacing:.02em}
    .subtitle{font-size:12px;color:var(--muted);margin-top:4px}
    .status{display:flex;align-items:center;gap:10px;flex-wrap:wrap;justify-content:flex-end}
    .pill{border:1px solid var(--line);background:#0a1323;border-radius:999px;padding:8px 11px;font-size:12px;color:var(--muted);white-space:nowrap}
    .pill strong{color:var(--text)}
    .dot{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:7px;background:var(--yellow);box-shadow:0 0 12px currentColor}
    .dot.ok{background:var(--green)}
    .dot.bad{background:var(--red)}
    .grid{display:grid;gap:14px}
    .kpis{grid-template-columns:repeat(6,minmax(150px,1fr));margin-bottom:14px}
    .card{border:1px solid var(--line);border-radius:16px;background:linear-gradient(180deg,rgba(16,26,45,.96),rgba(8,14,27,.96));box-shadow:var(--shadow);overflow:hidden}
    .kpi{padding:16px;min-height:112px;position:relative}
    .kpi:after{content:"";position:absolute;inset:auto -35px -55px auto;width:110px;height:110px;border-radius:50%;background:var(--accent,var(--blue));filter:blur(44px);opacity:.18}
    .kpi-label{font-size:11px;text-transform:uppercase;letter-spacing:.12em;color:var(--muted)}
    .kpi-value{font-size:27px;font-weight:800;margin-top:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
    .kpi-sub{font-size:11px;color:var(--muted);margin-top:8px}
    .layout{grid-template-columns:minmax(0,1.5fr) minmax(350px,.7fr);align-items:start}
    .stack{display:grid;gap:14px}
    .section-head{display:flex;align-items:center;justify-content:space-between;padding:15px 16px;border-bottom:1px solid var(--line)}
    .section-title{font-size:14px;font-weight:800;letter-spacing:.04em}
    .section-note{font-size:11px;color:var(--muted)}
    .company-grid{display:grid;grid-template-columns:repeat(3,minmax(260px,1fr));gap:12px;padding:12px}
    .company{border:1px solid var(--line);border-radius:14px;background:#081120;padding:14px;position:relative;overflow:hidden}
    .company:before{content:"";position:absolute;left:0;top:0;bottom:0;width:4px;background:var(--company,var(--blue))}
    .company-head{display:flex;justify-content:space-between;gap:12px;align-items:flex-start}
    .company-name{font-size:18px;font-weight:850}
    .company-id{font-size:10px;color:var(--muted);margin-top:3px}
    .state{font-size:10px;border-radius:999px;padding:5px 8px;background:rgba(53,230,164,.1);color:var(--green);border:1px solid rgba(53,230,164,.25)}
    .state.off{background:rgba(255,97,116,.1);color:var(--red);border-color:rgba(255,97,116,.25)}
    .metrics{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:13px}
    .metric{background:#0d182a;border:1px solid #17253b;border-radius:10px;padding:9px;min-width:0}
    .metric span{display:block;color:var(--muted);font-size:9px;text-transform:uppercase;letter-spacing:.07em}
    .metric b{display:block;font-size:13px;margin-top:5px;overflow:hidden;text-overflow:ellipsis}
    .bars{margin-top:12px;display:grid;gap:8px}
    .bar-row{display:grid;grid-template-columns:78px 1fr 35px;gap:8px;align-items:center;font-size:10px;color:var(--muted)}
    .bar{height:7px;border-radius:99px;background:#162238;overflow:hidden}
    .bar>i{display:block;height:100%;border-radius:99px;background:linear-gradient(90deg,var(--blue),var(--green))}
    .services{display:flex;flex-wrap:wrap;gap:6px;margin-top:12px}
    .tag{font-size:9px;padding:5px 7px;border-radius:7px;border:1px solid #21314d;background:#0c1729;color:#b9c8df}
    .tag em{font-style:normal;color:var(--green);font-weight:800;margin-left:5px}
    .table-wrap{overflow:auto;max-height:510px}
    table{width:100%;border-collapse:collapse;font-size:12px}
    th{position:sticky;top:0;background:#0e182a;color:var(--muted);font-size:10px;text-transform:uppercase;letter-spacing:.08em;text-align:left;padding:11px;border-bottom:1px solid var(--line);z-index:1}
    td{padding:11px;border-bottom:1px solid rgba(29,42,66,.72);vertical-align:top}
    tr:hover td{background:rgba(84,168,255,.04)}
    .num{text-align:right;font-variant-numeric:tabular-nums}
    .good{color:var(--green)} .bad{color:var(--red)} .warn{color:var(--yellow)} .muted{color:var(--muted)}
    .chart{padding:12px;height:260px}
    canvas{width:100%;height:100%;display:block}
    .log{height:540px;overflow:auto;padding:8px 12px;background:#050b15;font-family:Consolas,Monaco,monospace;font-size:11px;line-height:1.45}
    .log-line{display:grid;grid-template-columns:68px 72px 1fr;gap:8px;padding:5px 0;border-bottom:1px dashed rgba(29,42,66,.45)}
    .log-time{color:#677a99}.log-level{font-weight:800}.log-level.BASARILI{color:var(--green)}.log-level.UYARI{color:var(--yellow)}.log-level.HATA{color:var(--red)}.log-level.TICK{color:var(--purple)}.log-level.BILGI{color:var(--cyan)}
    .footer{padding:14px 4px 4px;color:var(--muted);font-size:10px;text-align:center}
    .empty{padding:28px;color:var(--muted);text-align:center}
    @media(max-width:1350px){.kpis{grid-template-columns:repeat(3,1fr)}.company-grid{grid-template-columns:repeat(2,1fr)}.layout{grid-template-columns:1fr}}
    @media(max-width:760px){body{padding:8px}.topbar{align-items:flex-start;flex-direction:column}.status{justify-content:flex-start}.kpis{grid-template-columns:repeat(2,1fr)}.company-grid{grid-template-columns:1fr}.metrics{grid-template-columns:repeat(2,1fr)}.log{height:420px}.log-line{grid-template-columns:58px 62px 1fr}.kpi-value{font-size:22px}}
  </style>
</head>
<body>
<div class="shell">
  <header class="topbar">
    <div class="brand">
      <div class="logo">3K</div>
      <div><h1>Üç Kardeş Yazılım Borsası</h1><div class="subtitle">Motor tarafından yayınlanan canlı şirket ve hizmet piyasası terminali</div></div>
    </div>
    <div class="status">
      <span class="pill"><i id="connectionDot" class="dot"></i><strong id="connectionText">Bağlanıyor</strong></span>
      <span class="pill">Motor: <strong id="motorId">—</strong></span>
      <span class="pill">Tick: <strong id="tick">0</strong></span>
      <span class="pill">Saat: <strong id="clock">—</strong></span>
    </div>
  </header>

  <section class="grid kpis">
    <div class="card kpi" style="--accent:var(--green)"><div class="kpi-label">Toplam şirket kasası</div><div class="kpi-value" id="totalCash">₺0</div><div class="kpi-sub" id="cashSub">Net gelir bekleniyor</div></div>
    <div class="card kpi" style="--accent:var(--blue)"><div class="kpi-label">Son tick talebi</div><div class="kpi-value" id="demandCount">0</div><div class="kpi-sub" id="demandBudget">₺0 bütçe</div></div>
    <div class="card kpi" style="--accent:var(--purple)"><div class="kpi-label">Son tick cirosu</div><div class="kpi-value" id="turnover">₺0</div><div class="kpi-sub" id="successRate">%0 başarı</div></div>
    <div class="card kpi" style="--accent:var(--cyan)"><div class="kpi-label">Bağlı şirket</div><div class="kpi-value" id="connectedCompanies">0 / 0</div><div class="kpi-sub" id="serviceCount">0 hizmet ilanı</div></div>
    <div class="card kpi" style="--accent:var(--yellow)"><div class="kpi-label">Aktif müşteri</div><div class="kpi-value" id="activeCustomers">0</div><div class="kpi-sub" id="customerBalance">₺0 müşteri bakiyesi</div></div>
    <div class="card kpi" style="--accent:var(--red)"><div class="kpi-label">Toplam tamamlanan iş</div><div class="kpi-value" id="completedJobs">0</div><div class="kpi-sub" id="failedJobs">0 başarısız / zaman aşımı</div></div>
  </section>

  <main class="grid layout">
    <div class="stack">
      <section class="card">
        <div class="section-head"><div><div class="section-title">ŞİRKET BORSASI</div><div class="section-note">Finans, bağlantı, performans ve yayınlanan hizmetler</div></div><div class="section-note" id="lastUpdate">—</div></div>
        <div id="companies" class="company-grid"></div>
      </section>

      <section class="card">
        <div class="section-head"><div><div class="section-title">KASA HAREKETİ</div><div class="section-note">Tarayıcı açık kaldığı sürece şirket kasalarının canlı eğrisi</div></div><div class="section-note">Son 120 örnek</div></div>
        <div class="chart"><canvas id="cashChart"></canvas></div>
      </section>

      <section class="card">
        <div class="section-head"><div><div class="section-title">HİZMET PİYASASI</div><div class="section-note">Talep, bütçe, sağlayıcılar, fiyatlar ve kapasite</div></div></div>
        <div class="table-wrap"><table><thead><tr><th>Hizmet</th><th>Durum</th><th class="num">Talep</th><th class="num">Talep bütçesi</th><th class="num">En ucuz</th><th class="num">En pahalı</th><th class="num">Sağlayıcı</th><th>Şirketler</th></tr></thead><tbody id="services"></tbody></table></div>
      </section>
    </div>

    <aside class="stack">
      <section class="card">
        <div class="section-head"><div><div class="section-title">SON TICK SONUCU</div><div class="section-note">Motorun işleme ve eşleştirme özeti</div></div></div>
        <div id="tickSummary" class="metrics" style="padding:12px;margin:0"></div>
      </section>

      <section class="card">
        <div class="section-head"><div><div class="section-title">CANLI MOTOR AKIŞI</div><div class="section-note">Son 250 motor olayı</div></div></div>
        <div id="logs" class="log"></div>
      </section>
    </aside>
  </main>
  <div class="footer">Yerel ağ canlı pano · Veriler doğrudan simülasyon motorundan gelir · Sayfa her 1 saniyede yenilenir</div>
</div>
<script>
const state={history:new Map(),lastLogKey:""};
const money=new Intl.NumberFormat("tr-TR",{style:"currency",currency:"TRY",maximumFractionDigits:2});
const number=new Intl.NumberFormat("tr-TR",{maximumFractionDigits:2});
const int=new Intl.NumberFormat("tr-TR",{maximumFractionDigits:0});
const esc=v=>String(v??"").replace(/[&<>\"']/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;","\"":"&quot;","'":"&#39;"}[c]));
const pct=v=>`${number.format(v||0)}%`;
const statusClass=s=>["Bagli","Calisiyor"].includes(s)?"":"off";
const companyColor=i=>["#35e6a4","#54a8ff","#b68cff","#ffd166","#42d4f4","#ff6174"][i%6];
function setText(id,value){document.getElementById(id).textContent=value}
function metric(label,value,cls=""){return `<div class="metric"><span>${esc(label)}</span><b class="${cls}">${esc(value)}</b></div>`}
function render(data){
  const g=data.genel||{}, p=data.piyasa||{}, o=p.islemeOzeti||{};
  setText("motorId",data.motor?.motorKimligi||"—");setText("tick",data.motor?.tickNumarasi??0);setText("lastUpdate",new Date(data.motor?.sunucuZamani||Date.now()).toLocaleString("tr-TR"));
  setText("totalCash",money.format(g.toplamSirketKasasi||0));setText("cashSub",`${money.format(g.toplamNetGelir||0)} toplam net gelir`);
  setText("demandCount",int.format(p.toplamTalepSayisi||0));setText("demandBudget",`${money.format(p.toplamTalepButcesi||0)} toplam bütçe`);
  setText("turnover",money.format(o.toplamCiro||0));setText("successRate",`${pct(o.basariOrani||0)} başarı oranı`);
  setText("connectedCompanies",`${g.bagliSirketSayisi||0} / ${g.toplamSirketSayisi||0}`);setText("serviceCount",`${g.toplamHizmetIlani||0} hizmet ilanı`);
  setText("activeCustomers",int.format(g.aktifMusteriSayisi||0));setText("customerBalance",`${money.format(g.toplamMusteriBakiyesi||0)} müşteri bakiyesi`);
  setText("completedJobs",int.format(g.toplamTamamlananIs||0));setText("failedJobs",`${int.format(g.toplamBasarisizIs||0)} başarısız / zaman aşımı`);
  renderCompanies(data.sirketler||[]);renderServices(data.hizmetPiyasasi||[]);renderSummary(o);renderLogs(data.olaylar||[]);updateHistory(data.sirketler||[]);drawCashChart();
}
function renderCompanies(companies){
  const el=document.getElementById("companies");
  if(!companies.length){el.innerHTML='<div class="empty">Şirket kaydı bulunamadı.</div>';return}
  el.innerHTML=companies.map((c,i)=>{
    const services=(c.hizmetler||[]).map(h=>`<span class="tag">${esc(h.hizmetKimligi)}@${esc(h.hizmetSurumu)} <em>${money.format(h.birimFiyat)}</em></span>`).join("");
    return `<article class="company" style="--company:${companyColor(i)}"><div class="company-head"><div><div class="company-name">${esc(c.sirketAdi)}</div><div class="company-id">${esc(c.sirketKimligi)} · ${esc(c.sunucuAdresi)}:${esc(c.sunucuPortu)} · v${esc(c.sunucuSurumu||"—")}</div></div><span class="state ${statusClass(c.durum)}">${esc(c.durum)}</span></div>
    <div class="metrics">${metric("Kasa",money.format(c.kasa||0),"good")}${metric("Net gelir",money.format(c.netGelir||0))}${metric("Toplam gelir",money.format(c.toplamGelir||0))}${metric("Gecikme",`${number.format(c.sonGecikmeMs||0)} ms`)}${metric("Kuyruk",int.format(c.kuyrukUzunlugu||0))}${metric("Aktif iş",int.format(c.aktifIsSayisi||0))}${metric("Başarılı iş",int.format(c.tamamlananIsSayisi||0),"good")}${metric("Başarısız",int.format((c.basarisizIsSayisi||0)+(c.zamanAsiminaUgrayanIsSayisi||0)),(c.basarisizIsSayisi||0)>0?"bad":"")}${metric("Ceza",money.format(c.toplamCeza||0),(c.toplamCeza||0)>0?"bad":"")}</div>
    <div class="bars"><div class="bar-row"><span>İtibar</span><div class="bar"><i style="width:${Math.max(0,Math.min(100,c.itibarPuani||0))}%"></i></div><b>${number.format(c.itibarPuani||0)}</b></div><div class="bar-row"><span>Güven</span><div class="bar"><i style="width:${Math.max(0,Math.min(100,c.guvenilirlikPuani||0))}%"></i></div><b>${number.format(c.guvenilirlikPuani||0)}</b></div><div class="bar-row"><span>Memnuniyet</span><div class="bar"><i style="width:${Math.max(0,Math.min(100,c.ortalamaMusteriMemnuniyeti||0))}%"></i></div><b>${number.format(c.ortalamaMusteriMemnuniyeti||0)}</b></div></div>
    <div class="services">${services||'<span class="tag">Hizmet ilan edilmedi</span>'}</div></article>`;
  }).join("");
}
function renderServices(services){
  const el=document.getElementById("services");
  if(!services.length){el.innerHTML='<tr><td colspan="8" class="empty">Hizmet bulunamadı.</td></tr>';return}
  el.innerHTML=services.map(s=>`<tr><td><b>${esc(s.hizmetKimligi)}</b><div class="muted">v${esc(s.hizmetSurumu)} · ${esc(s.aciklama||"")}</div></td><td class="${s.aktif?"good":"bad"}">${s.aktif?"AKTİF":"KAPALI"}</td><td class="num">${int.format(s.talepSayisi||0)}</td><td class="num">${money.format(s.toplamTalepButcesi||0)}</td><td class="num good">${s.enUcuzFiyat==null?"—":money.format(s.enUcuzFiyat)}</td><td class="num">${s.enPahaliFiyat==null?"—":money.format(s.enPahaliFiyat)}</td><td class="num">${int.format(s.saglayiciSayisi||0)}</td><td>${(s.saglayicilar||[]).map(x=>`<span class="tag">${esc(x.sirketAdi)} <em>${money.format(x.birimFiyat)}</em></span>`).join(" ")||'<span class="bad">Sağlayıcı yok</span>'}</td></tr>`).join("");
}
function renderSummary(o){
  document.getElementById("tickSummary").innerHTML=[metric("Toplam talep",int.format(o.toplamTalepSayisi||0)),metric("Başarılı",int.format(o.basariliIsSayisi||0),"good"),metric("Başarısız",int.format(o.basarisizIsSayisi||0),(o.basarisizIsSayisi||0)>0?"bad":""),metric("Şirket yok",int.format(o.sirketBulunamayanIsSayisi||0),(o.sirketBulunamayanIsSayisi||0)>0?"warn":""),metric("Bütçe yetersiz",int.format(o.butceYetersizIsSayisi||0),(o.butceYetersizIsSayisi||0)>0?"warn":""),metric("Zaman aşımı",int.format(o.zamanAsimiSayisi||0),(o.zamanAsimiSayisi||0)>0?"bad":""),metric("Ciro",money.format(o.toplamCiro||0),"good"),metric("İşlem süresi",`${number.format(o.toplamIslemSuresiMs||0)} ms`),metric("Başarı oranı",pct(o.basariOrani||0))].join("");
}
function renderLogs(logs){
  const el=document.getElementById("logs");const nearBottom=el.scrollHeight-el.scrollTop-el.clientHeight<80;
  el.innerHTML=logs.map(l=>`<div class="log-line"><span class="log-time">${new Date(l.zaman).toLocaleTimeString("tr-TR")}</span><span class="log-level ${esc(l.seviye)}">${esc(l.seviye)}</span><span>${esc(l.mesaj)}</span></div>`).join("")||'<div class="empty">Henüz motor olayı yok.</div>';
  if(nearBottom)el.scrollTop=el.scrollHeight;
}
function updateHistory(companies){
  const now=Date.now();companies.forEach(c=>{if(!state.history.has(c.sirketKimligi))state.history.set(c.sirketKimligi,{name:c.sirketAdi,points:[]});const h=state.history.get(c.sirketKimligi);h.name=c.sirketAdi;h.points.push({x:now,y:Number(c.kasa||0)});if(h.points.length>120)h.points.shift()});
}
function drawCashChart(){
  const canvas=document.getElementById("cashChart"),dpr=window.devicePixelRatio||1,rect=canvas.getBoundingClientRect();canvas.width=Math.max(1,rect.width*dpr);canvas.height=Math.max(1,rect.height*dpr);const ctx=canvas.getContext("2d");ctx.scale(dpr,dpr);const w=rect.width,h=rect.height,p={l:54,r:18,t:18,b:30};ctx.clearRect(0,0,w,h);ctx.strokeStyle="#1d2a42";ctx.fillStyle="#7386a4";ctx.font="10px Segoe UI";const series=[...state.history.values()].filter(x=>x.points.length);const vals=series.flatMap(x=>x.points.map(p=>p.y));let min=Math.min(0,...vals),max=Math.max(1,...vals);if(max===min)max=min+1;for(let i=0;i<=4;i++){const y=p.t+(h-p.t-p.b)*i/4;ctx.beginPath();ctx.moveTo(p.l,y);ctx.lineTo(w-p.r,y);ctx.stroke();const v=max-(max-min)*i/4;ctx.fillText(number.format(v),4,y+3)}const colors=["#35e6a4","#54a8ff","#b68cff","#ffd166","#42d4f4","#ff6174"];series.forEach((s,idx)=>{ctx.strokeStyle=colors[idx%colors.length];ctx.lineWidth=2;ctx.beginPath();s.points.forEach((pt,i)=>{const x=p.l+(w-p.l-p.r)*(s.points.length<=1?0:i/(Math.max(1,s.points.length-1)));const y=p.t+(h-p.t-p.b)*(1-(pt.y-min)/(max-min));i?ctx.lineTo(x,y):ctx.moveTo(x,y)});ctx.stroke();ctx.fillStyle=colors[idx%colors.length];ctx.fillRect(p.l+idx*125,h-15,10,3);ctx.fillStyle="#b7c5db";ctx.fillText(s.name,p.l+14+idx*125,h-11)})
}
async function refresh(){
  try{const r=await fetch(`/api/durum?t=${Date.now()}`,{cache:"no-store"});if(!r.ok)throw new Error(`HTTP ${r.status}`);const data=await r.json();render(data);document.getElementById("connectionDot").className="dot ok";setText("connectionText","CANLI")}catch(e){document.getElementById("connectionDot").className="dot bad";setText("connectionText","BAĞLANTI YOK");console.error(e)}
}
setInterval(()=>setText("clock",new Date().toLocaleTimeString("tr-TR")),250);setInterval(refresh,1000);window.addEventListener("resize",drawCashChart);refresh();
</script>
</body>
</html>
""";
}
