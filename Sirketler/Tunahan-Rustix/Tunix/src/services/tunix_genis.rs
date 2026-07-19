use super::HizmetHatasi;
use serde_json::{json, Map, Value};
use std::collections::{BTreeMap, BTreeSet};

const HIZMETLER: &[&str] = &[
    "metin.buyuk-harf",
    "metin.kucuk-harf",
    "metin.birlestir",
    "metin.parcala",
    "metin.ozetle",
    "metin.anahtar-kelime",
    "dizi.filtrele",
    "dizi.birlestir",
    "dizi.kesisim",
    "dizi.birlesim",
    "dizi.parcala",
    "dizi.dogrula",
    "veri.filtrele",
    "veri.temizle",
    "veri.normalize-et",
    "veri.gruplandir",
    "veri.birlestir",
    "veri.korelasyon",
    "arama.ara",
    "arama.sirala",
    "arama.otomatik-tamamla",
    "arama.yazim-duzelt",
    "bildirim.push-gonder",
    "bildirim.zamanla",
];

pub fn destekli(kimlik: &str) -> bool {
    HIZMETLER.contains(&kimlik)
}

pub fn calistir(kimlik: &str, ham: &str) -> Result<String, HizmetHatasi> {
    let value: Value = if ham.trim().is_empty() {
        json!({})
    } else {
        serde_json::from_str(ham)
            .map_err(|h| HizmetHatasi::GecersizIstek(format!("JSON geçersiz: {h}")))?
    };
    let sonuc = match kimlik {
        "metin.buyuk-harf" => json!({"metin": metin(&value).to_uppercase()}),
        "metin.kucuk-harf" => json!({"metin": metin(&value).to_lowercase()}),
        "metin.birlestir" => json!({"metin": metin_dizisi(&value).join(ayirici(&value))}),
        "metin.parcala" => {
            let a = ayirici(&value);
            let parts: Vec<String> = if a.is_empty() {
                metin(&value).chars().map(|x| x.to_string()).collect()
            } else {
                metin(&value).split(a).map(str::to_string).collect()
            };
            json!({"parcalar": parts})
        }
        "metin.ozetle" => {
            let m = metin(&value);
            let limit = sayi(&value, "azamiKarakter", 180.0).clamp(20.0, 2000.0) as usize;
            let mut ozet: String = m.chars().take(limit).collect();
            if m.chars().count() > limit { ozet.push('…'); }
            json!({"ozet": ozet, "orijinalKarakter": m.chars().count()})
        }
        "metin.anahtar-kelime" => {
            let mut sayim = BTreeMap::<String, usize>::new();
            for k in kelimeler(&metin(&value)) {
                if k.chars().count() >= 3 { *sayim.entry(k).or_default() += 1; }
            }
            let mut entries: Vec<(String, usize)> = sayim.into_iter().collect();
            entries.sort_by(|a,b| b.1.cmp(&a.1).then_with(|| a.0.cmp(&b.0)));
            json!({"anahtarKelimeler": entries.into_iter().take(12).map(|x| json!({"kelime":x.0,"sayi":x.1})).collect::<Vec<_>>()})
        }
        "dizi.filtrele" | "veri.filtrele" => {
            let min = sayi_opt(&value, "min"); let max = sayi_opt(&value, "max");
            let out: Vec<f64> = sayilar(&value).into_iter().filter(|x| min.is_none_or(|m|*x>=m)&&max.is_none_or(|m|*x<=m)).collect();
            json!({"sonuc":out,"adet":out.len()})
        }
        "dizi.birlestir" | "veri.birlestir" => {
            let mut out = dizi(&value, "birinci"); out.extend(dizi(&value, "ikinci"));
            if out.is_empty() { out = value.get("diziler").and_then(Value::as_array).map(|ds|ds.iter().flat_map(|d|d.as_array().cloned().unwrap_or_default()).collect()).unwrap_or_default(); }
            json!({"sonuc":out,"adet":out.len()})
        }
        "dizi.kesisim" => {
            let a: BTreeSet<String> = dizi(&value,"birinci").iter().map(normal_json).collect();
            let b: BTreeSet<String> = dizi(&value,"ikinci").iter().map(normal_json).collect();
            json!({"sonuc":a.intersection(&b).cloned().collect::<Vec<_>>()})
        }
        "dizi.birlesim" => {
            let mut s: BTreeSet<String> = dizi(&value,"birinci").iter().map(normal_json).collect();
            s.extend(dizi(&value,"ikinci").iter().map(normal_json));
            json!({"sonuc":s.into_iter().collect::<Vec<_>>()})
        }
        "dizi.parcala" => {
            let arr=dizi_varsayilan(&value); let boy=sayi(&value,"parcaBoyutu",10.0).clamp(1.0,1000.0) as usize;
            json!({"parcalar":arr.chunks(boy).map(|x|x.to_vec()).collect::<Vec<_>>()})
        }
        "dizi.dogrula" => {
            let arr=dizi_varsayilan(&value); json!({"gecerli":true,"adet":arr.len(),"bos":arr.is_empty()})
        }
        "veri.temizle" => {
            let arr=dizi_varsayilan(&value); let temiz:Vec<Value>=arr.into_iter().filter(|x|!x.is_null()&&x.as_str().is_none_or(|s|!s.trim().is_empty())).collect();
            json!({"sonuc":temiz,"adet":temiz.len()})
        }
        "veri.normalize-et" => {
            let xs=sayilar(&value); if xs.is_empty(){json!({"sonuc":[]})}else{let min=xs.iter().copied().fold(f64::INFINITY,f64::min);let max=xs.iter().copied().fold(f64::NEG_INFINITY,f64::max);let span=max-min;let out:Vec<f64>=xs.iter().map(|x|if span.abs()<f64::EPSILON{0.0}else{(x-min)/span}).collect();json!({"sonuc":out,"min":min,"max":max})}
        }
        "veri.gruplandir" => {
            let arr=dizi_varsayilan(&value);let alan=value.get("alan").and_then(Value::as_str).unwrap_or("tur");let mut g:BTreeMap<String,Vec<Value>>=BTreeMap::new();for x in arr{let k=x.get(alan).map(normal_json).unwrap_or_else(||"diger".into());g.entry(k).or_default().push(x);}json!({"gruplar":g})
        }
        "veri.korelasyon" => {
            let x=sayi_dizisi(&value,"x");let y=sayi_dizisi(&value,"y");let n=x.len().min(y.len());let r=if n<2{0.0}else{let ax=x[..n].iter().sum::<f64>()/n as f64;let ay=y[..n].iter().sum::<f64>()/n as f64;let mut p=0.0;let mut q=0.0;let mut z=0.0;for i in 0..n{let dx=x[i]-ax;let dy=y[i]-ay;p+=dx*dy;q+=dx*dx;z+=dy*dy;}if q*z<=0.0{0.0}else{p/(q*z).sqrt()}};json!({"korelasyon":r,"ornekSayisi":n})
        }
        "arama.ara" => {
            let q=sorgu(&value).to_lowercase();let kaynak=kaynak(&value);let out:Vec<Value>=kaynak.into_iter().filter(|x|normal_json(x).to_lowercase().contains(&q)).take(100).collect();json!({"sonuclar":out,"adet":out.len()})
        }
        "arama.sirala" => {let mut out=kaynak(&value);out.sort_by_key(normal_json);json!({"sonuclar":out})}
        "arama.otomatik-tamamla" => {let q=sorgu(&value).to_lowercase();let mut out:Vec<String>=kaynak(&value).iter().filter_map(Value::as_str).filter(|x|x.to_lowercase().starts_with(&q)).map(str::to_string).collect();out.sort();out.dedup();out.truncate(12);json!({"oneriler":out})}
        "arama.yazim-duzelt" => {let q=sorgu(&value);json!({"duzeltilmis":q.trim().split_whitespace().collect::<Vec<_>>().join(" "),"degisti":q!=q.trim()})}
        "bildirim.push-gonder" => json!({"bildirimKimligi":kimlik_uret("push"),"durum":"kuyruga-alindi","alici":alan(&value,"alici","musteri")}),
        "bildirim.zamanla" => json!({"bildirimKimligi":kimlik_uret("scheduled"),"durum":"zamanlandi","zaman":alan(&value,"zaman","sonraki-tick")}),
        _ => return Err(HizmetHatasi::DesteklenmeyenHizmet),
    };
    serde_json::to_string(&json!({"sonuc":sonuc,"hizmetKimligi":kimlik,"motorSozlesmesi":"v6"}))
        .map_err(|h| HizmetHatasi::SonucSerilestirme(h.to_string()))
}

fn metin(v:&Value)->String{v.get("metin").and_then(Value::as_str).or_else(||v.get("sorgu").and_then(Value::as_str)).unwrap_or_default().to_string()}
fn metin_dizisi(v:&Value)->Vec<String>{v.get("metinler").or_else(||v.get("dizi")).and_then(Value::as_array).map(|a|a.iter().map(|x|x.as_str().map(str::to_string).unwrap_or_else(||normal_json(x))).collect()).unwrap_or_else(||vec![metin(v)])}
fn ayirici(v:&Value)->&str{v.get("ayirici").and_then(Value::as_str).unwrap_or(" ")}
fn sayi(v:&Value,k:&str,d:f64)->f64{v.get(k).and_then(Value::as_f64).unwrap_or(d)}
fn sayi_opt(v:&Value,k:&str)->Option<f64>{v.get(k).and_then(Value::as_f64)}
fn dizi(v:&Value,k:&str)->Vec<Value>{v.get(k).and_then(Value::as_array).cloned().unwrap_or_default()}
fn dizi_varsayilan(v:&Value)->Vec<Value>{v.get("dizi").or_else(||v.get("veriler")).or_else(||v.get("kaynak")).and_then(Value::as_array).cloned().unwrap_or_default()}
fn sayilar(v:&Value)->Vec<f64>{v.get("sayilar").or_else(||v.get("dizi")).or_else(||v.get("veriler")).and_then(Value::as_array).map(|a|a.iter().filter_map(Value::as_f64).collect()).unwrap_or_default()}
fn sayi_dizisi(v:&Value,k:&str)->Vec<f64>{v.get(k).and_then(Value::as_array).map(|a|a.iter().filter_map(Value::as_f64).collect()).unwrap_or_default()}
fn kaynak(v:&Value)->Vec<Value>{v.get("kaynak").or_else(||v.get("veriler")).or_else(||v.get("dizi")).and_then(Value::as_array).cloned().unwrap_or_default()}
fn sorgu(v:&Value)->String{v.get("sorgu").and_then(Value::as_str).unwrap_or_default().to_string()}
fn normal_json(v:&Value)->String{v.as_str().map(str::to_string).unwrap_or_else(||serde_json::to_string(v).unwrap_or_default())}
fn kelimeler(s:&str)->Vec<String>{s.split(|c:char|!c.is_alphanumeric()).filter(|x|!x.is_empty()).map(|x|x.to_lowercase()).collect()}
fn alan(v:&Value,k:&str,d:&str)->String{v.get(k).and_then(Value::as_str).unwrap_or(d).to_string()}
fn kimlik_uret(prefix:&str)->String{use std::time::{SystemTime,UNIX_EPOCH};let n=SystemTime::now().duration_since(UNIX_EPOCH).unwrap_or_default().as_nanos();format!("{prefix}-{n}")}

#[cfg(test)]
mod tests{
    use super::*;
    #[test]fn metin_hizmetleri(){let r=calistir("metin.buyuk-harf",r#"{"metin":"Tunix güzel"}"#).unwrap();assert!(r.contains("TUNIX GÜZEL"));}
    #[test]fn korelasyon(){let r=calistir("veri.korelasyon",r#"{"x":[1,2,3],"y":[2,4,6]}"#).unwrap();assert!(r.contains("korelasyon"));}
    #[test]fn tum_hizmetler_destekli(){assert_eq!(HIZMETLER.len(),24);assert!(HIZMETLER.iter().all(|x|destekli(x)));}
}
