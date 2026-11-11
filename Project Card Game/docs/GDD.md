# HexPortal — Oyun Tasarım Dokümanı (GDD)

> Sürüm: 1.0 · Tarih: 2026-09-25 · Durum: Prototip (Faz 1) kapsamı onaylandı
> "HexPortal" çalışma adıdır, sonra değişebilir.

Bu doküman oyunun **tek doğruluk kaynağıdır**. Her kuralın kalıcı bir kimliği vardır (ör. `U-04`). Kod, testler ve commit mesajları bu kimliklere referans verir. Kimlikler **asla yeniden numaralandırılmaz**. Bir kural kalkarsa "KALDIRILDI" diye işaretlenir, yeni kural yeni numara alır. Değişiklikler en alttaki **Değişiklik Günlüğü**'ne yazılır.

---

## 0. Özet

- **Tür:** Sıra tabanlı, 1v1, altıgen tahtada kart + taktik strateji.
- **Platform:** Mobil (önce Android), dikey (portre) ekran.
- **Görsel stil:** Renkli, sevimli, canlı; low poly 3D tahta ve birimler, minimal 2D kartlar.
- **Maç süresi hedefi:** 10–12 dakika.
- **Oyun akışı:** Oyuncular kulelerini ve birimlerini rakibe göstermeden yerleştirir. Tahta açılır. Oyuncular ortak havuzdan kart çeker, birimleri satranç benzeri kalıplarla hareket ettirir ve gizli görevlerini tamamlar. 3 görevden 2'sini bitiren oyuncu için merkezdeki portal açılır. Portala girip rakibin bir turunu hayatta atlatan oyuncu kazanır. Rakibin kulesini yıkmak da kazandırır.

### 0.1 Kapsam dışı (Faz 1 ve Faz 2)

Online mod (Faz 3), deste oluşturma ve koleksiyon, 2'den fazla oyuncu, saldıran kule, karşı saldırı, Gölge sınıfı, Sihir ve Arazi kartları, karakter özelleştirmeleri.

---

## 1. Sözlük (Türkçe → kod adı)

Kodda **yalnızca İngilizce adlar** kullanılır.

| Türkçe | Kod | Türkçe | Kod |
|---|---|---|---|
| Tur (bir oyuncunun sırası) | `Turn` | Raunt (iki oyuncunun turu) | `Round` |
| Karo | `Tile` / `Cell` | Tahta | `Board` |
| Birim / Karakter | `Unit` | Kule | `Tower` |
| Muhafız | `Guardian` | Süvari | `Rider` |
| Okçu | `Archer` | Büyücü | `Mage` |
| Şifacı | `Healer` | Biyom | `Biome` |
| Orman / Çöl / Kar | `Forest` / `Desert` / `Snow` | Rün Taşı | `RuneStone` |
| Kaynak | `Wellspring` | Portal | `Portal` |
| Kaya (geçilmez) | `Rock` | Başlangıç bölgesi | `HomeZone` |
| Destek kartı | `SupportCard` | Tuzak | `Trap` |
| Açık Pazar | `Market` | Kör çekme | `BlindDraw` |
| Havuz | `Pool` | Enerji | `Energy` |
| Aksiyon | `Action` | Görev | `Quest` |
| Komutan pasifi | `CommanderPassive` | Harita olayı | `MapEvent` |
| Saldırı / Can / Hareket | `Attack` / `Health` / `Move` | Nadirlik | `Rarity` |

---

## 2. Tahta (B)

- **B-01** Tahta 9 sıradan oluşur. Sıra uzunlukları üstten alta **7, 6, 7, 6, 7, 6, 7, 6, 7** karodur, toplam **59 karo**. Karolar sivri tepeli (pointy-top) altıgendir. Koordinat detayı için `hex-grid` skill'ine bakın.
- **B-02** Merkez karo (5. sıra, 4. karo) **Portal**'dır.
- **B-03** Alttaki 2 sıra (8–9) **Oyuncu A**'nın, üstteki 2 sıra (1–2) **Oyuncu B**'nin başlangıç bölgesidir (her biri 13 karo).
- **B-04** Tahta, merkeze göre **180° dönme simetrisine** sahiptir. Her karonun bir "eş karosu" vardır ve eş karolar her zaman aynı türdedir.
- **B-05** Her oyuncu tahtayı **kendi bölgesi altta** olacak şekilde görür. Oyuncu B'nin ekranında görünüm 180° döndürülür, kurallar değişmez.

### 2.1 Karo türleri

| ID | Tür | Etki |
|---|---|---|
| **B-10** | Orman / Çöl / Kar | Kendi biyomunda saldıran birim +1 Saldırı alır (bkz. U-20) |
| **B-11** | Rün Taşı | Görev hedefi. Üzerindeki biyom geçerliliğini korur |
| **B-12** | Kaynak | Tur başında üzerinde kendi birimin varsa +1 enerji (bkz. T-03) |
| **B-13** | Portal | Kazanma karosu. Biyomu yoktur |
| **B-14** | Kaya | Geçilmez, üzerine birim konamaz. Sadece harita olaylarıyla oluşur |

Rün Taşı ve Kaynak, biyomun üstünde duran bir **işarettir**. Karonun biyomu korunur.

### 2.2 Harita üretimi

- **B-20** Harita tek bir tohum (seed) değerinden deterministik olarak üretilir. Aynı tohum her zaman aynı haritayı verir.
- **B-21** Önce "üst yarı" üretilir: 1–4. sıralar ve 5. sıranın portalın solundaki 3 karosu, toplam 29 karo. Alt yarı, eş karolardan kopyalanır.
- **B-22** Biyom dağılımı: Üst yarının 29 karosunda her biyom **en az 8** karo alır. Biyomlar küçük kümeler halinde dağılsın: rastgele 3–5 çekirdek karodan komşulara yayılma.
- **B-23** Özel karolar (her yarıda): **2 Rün Taşı** 3–4. sıralarda, **1 Kaynak** 3–4. sıralarda. Başlangıç bölgesinde özel karo olmaz. İki rün taşı birbirine komşu olmaz.
- **B-24** Üretilen harita doğrulanır: Simetri tam olmalı, sayılar B-22 ve B-23'e uymalı. Uymazsa bir sonraki tohumla yeniden üretilir.

---

## 3. Birimler (U)

### 3.1 Sınıflar

Her sınıfın 3 biyom varyantı vardır. **Varyantların statları ve yetenekleri aynıdır**, tek fark biyomdur. 5 sınıf × 3 biyom = 15 benzersiz karakter kartı.

| ID | Sınıf | Saldırı | Can | Hareket | Hareket kalıbı | Saldırı menzili | Yetenek |
|---|---|---|---|---|---|---|---|
| **U-01** | Muhafız | 2 | 6 | 1 | Düz | Komşu | **Siper:** Bkz. U-11 |
| **U-02** | Süvari | 3 | 4 | 1 | Sıçrama | Komşu | **Hücum:** Bu tur sıçradıysa +1 Saldırı |
| **U-03** | Okçu | 2 | 3 | 2 | Düz | Düz hatta 1–3 | **Uzak atış:** Aradaki birimler atışı engellemez |
| **U-04** | Büyücü | 2 | 3 | 2 | Çapraz | Mesafe 1–2 | **Patlama:** Hedefe komşu düşman birimleri ve kuleler 1 hasar alır |
| **U-05** | Şifacı | 1 | 3 | 1 | Düz | Komşu | **Şifa:** Kendi tur başında komşu dost birimler +1 Can alır |

**Kule (U-06):** Her oyuncunun 1 kulesi vardır. Can **10**. Hareket etmez, **saldırmaz**. Bir karoyu kaplar ve geçişi engeller. Kartlar kuleyi hedef alamaz. Kule yıkılırsa sahibi kaybeder (W-02).

### 3.2 Hareket

- **U-07 Düz kalıp:** 6 kenar komşusu yönünden birinde, Hareket değeri kadar karo ilerler. Hareket bir yönde düz bir hat boyuncadır, yol üzerinde birim, kule veya kaya varsa durur.
- **U-08 Çapraz kalıp:** 6 çapraz yönden birinde, Hareket değeri kadar adım ilerler. Her çapraz adımın varış karosu boş olmalıdır.
- **U-09 Sıçrama kalıbı:** Tam olarak 3 mesafedeki, fakat düz hatlarda olmayan **12 karodan** birine atlar (altıgen satrancı atı). Aradaki her şeyin üstünden atlar. Hareket bonusu Süvari'ye **en fazla +1 ek sıçrama** verir.
- **U-10** Bir birim dolu karoda (birim, kule, kaya) duramaz. Portal'da durabilir.
- **U-11 Siper:** Bir Muhafız'a komşu olan dost birimler ve dost kule, **saldırılarla** hedef alınamaz. Bu korunan birim Muhafız'ın kendisi değildir. Büyücü'nün Patlama hasarı ve kartlar Siper'i yok sayar.

### 3.3 Savaş

- **U-20 Hasar:** Saldırı hasarı = Saldırı statı + biyom bonusu + buff'lar − debuff'lar, en az 0. Biyom bonusu, **saldıran birim** kendi biyomundaki bir karodayken geçerlidir.
- **U-21** Karşı saldırı yoktur.
- **U-22** Can 0'a inen birim ölür ve tahtadan kalkar. Üzerindeki tüm etkiler silinir.
- **U-23 Ölüm çekişi:** Birimi ölen oyuncu hemen 1 kör kart çeker (el doluysa çekmez).
- **U-24** Can, iyileşme ile başlangıç değerini aşamaz.
- **U-25** Okçu komşu karodaki düşmana da saldırabilir (menzil 1–3).

---

## 4. Destek kartları (C)

### 4.1 Genel kurallar

- **C-01** Destek kartı oynamak **aksiyon harcamaz**, kartın enerji maliyetini harcar.
- **C-02** Kartların menzil kısıtı yoktur: Tahtadaki geçerli herhangi bir birim hedeflenebilir. Buff'lar dost birimleri, debuff'lar düşman birimleri hedefler.
- **C-03** Süre türleri: **Anlık** (hemen uygulanır, iz bırakmaz), **2 tur**, **Kalıcı** (birim ölene veya etki tetiklenene kadar).
- **C-04** "2 tur" sayacı, **etkilenen birimin sahibinin her tur sonunda** 1 azalır. 0 olunca etki biter. Kendi turunda oynadığın buff o turu da sayar. Rakibe oynadığın debuff, rakibin sonraki 2 turu boyunca sürer.
- **C-05** Bir birim aynı anda en fazla **2 süreli ya da kalıcı buff** taşır. Anlık etkiler sayılmaz. Debuff'ların sınırı yoktur.
- **C-06** Aynı kart aynı birime üst üste oynanabilir, etkiler toplanır (C-05 sınırı dahilinde).

### 4.2 Kart listesi

| ID | Kart | Kategori | Etki | Süre | Enerji | Nadirlik | Havuzdaki kopya |
|---|---|---|---|---|---|---|---|
| **C-10** | Öfke | Güç (Buff) | +2 Saldırı | 2 tur | 1 | Sıradan | 4 |
| **C-11** | Dev Gücü | Güç (Buff) | +1 Saldırı | Kalıcı | 2 | Nadir | 2 |
| **C-12** | Kalkan | Koruma (Buff) | Birime gelen bir sonraki hasarı tamamen engeller, sonra biter | Kalıcı | 1 | Sıradan | 4 |
| **C-13** | Şifa İksiri | Koruma (Buff) | +3 Can | Anlık | 1 | Sıradan | 4 |
| **C-14** | Rüzgâr Adımı | Hareket (Buff) | Bu tur +2 Hareket (Süvari: +1 sıçrama) | Anlık | 1 | Sıradan | 4 |
| **C-15** | Işınlanma | Hareket (Buff) | Dost birimi Portal hariç boş bir karoya taşır. Birimin hareket hakkını kullanmaz | Anlık | 3 | Epik | 1 |
| **C-16** | Zayıflık | Lanet (Debuff) | −2 Saldırı | 2 tur | 1 | Sıradan | 4 |
| **C-17** | Zehir | Lanet (Debuff) | Sahibinin tur başında −1 Can | 2 tur | 2 | Nadir | 2 |
| **C-18** | İtme | Kontrol (Debuff) | Hedefi seçilen bir düz yönde 2 karo iter. Engele çarparsa durur, hasar olmaz. Tuzak tetikleyebilir | Anlık | 1 | Sıradan | 4 |
| **C-19** | Kök Salma | Kontrol (Debuff) | Hedef hareket edemez ama saldırabilir | 2 tur | 2 | Nadir | 2 |
| **C-20** | Diken Tuzağı | Tuzak | Tetikleyen birim 3 hasar alır | Kalıcı | 1 | Nadir | 2 |
| **C-21** | Ayna Tuzağı | Tuzak | Tetikleyen birim, kendi başlangıç bölgesinde rastgele boş bir karoya ışınlanır | Kalıcı | 2 | Epik | 1 |

Kopya toplamı: 6 sıradan × 4 + 4 nadir × 2 + 2 epik × 1 = **34 kart**.

### 4.3 Tuzaklar

- **C-30** Tuzak, kapalı olarak **boş bir karoya** konur. Portal, kule ve kaya karoları hariç. Konulduğu anda tahtada birim olmamalıdır.
- **C-31** Bir oyuncunun aynı anda en fazla **2 aktif tuzağı** olabilir.
- **C-32** Tuzak yalnızca **rakip** birim karoya **girdiğinde** tetiklenir: hareket, itme, ışınlanma veya sıçrama inişi. Tetiklenen birimin hareketi orada biter. Tuzak tek kullanımlıktır.
- **C-33** Rakip tuzaklar görünmez. Tetiklenince iki oyuncuya da gösterilir.
- **C-34** Aynı karoda iki oyuncunun da birer tuzağı olabilir. Her tuzak sadece rakip birimlerde tetiklenir.
- **C-35** Kaya oluşan karodaki tuzak yok olur.

---

## 5. Havuz, dağıtım ve Pazar (D)

- **D-01 Karakter havuzu:** 15 kart × 2 kopya = **30 kart**.
- **D-02 Destek havuzları:** "Buff havuzu" (C-10…C-15, 19 kart) ve "Debuff/Tuzak havuzu" (C-16…C-21, 15 kart).
- **D-03 Dengeli dağıtım (karakter):** Her oyuncuya **her sınıftan 1 karakter** verilir, biyomu rastgele seçilir. Toplam 5 karakter. Kalan 20 kart havuzda kalır.
- **D-04 Dengeli dağıtım (destek):** Her oyuncuya **3 sıradan + 1 nadir** destek kartı rastgele verilir. **Epik kartlar dağıtılmaz**, sadece havuzdan çekilebilir.
- **D-05 Açık Pazar:** 3 açık slot vardır: **Karakter**, **Buff**, **Debuff/Tuzak**. Her slot kendi havuzundan doldurulur. Bir kart alınınca slot hemen aynı havuzdan yenilenir. Havuz boşsa slot boş kalır.
- **D-06 Kör çekme:** Karakter + Buff + Debuff/Tuzak havuzlarının birleşiminden rastgele 1 kart çekilir. Pazardaki kartlar kör çekmeye dahil değildir.
- **D-07 El sınırı: 6 kart.** El doluysa kart çekme atlanır. Hazırlık aşamasında dağıtılan kartlar bu sınıra tabi değildir, sınır hazırlık bitince devreye girer.

---

## 6. Hazırlık aşaması (S)

- **S-01** Harita üretilir (B-20). Oyuncu A ve B rastgele belirlenir, **A ilk oynar**.
- **S-02** Kartlar dağıtılır (D-03, D-04). Pazar açılır (D-05).
- **S-03 Görev seçimi:** Her oyuncuya görev havuzundan **5 farklı görev** rastgele gelir (iki oyuncunun görevleri çakışabilir). Oyuncu **3'ünü seçer**, seçimi gizlidir.
- **S-04 Komutan seçimi:** Her oyuncuya **3 farklı pasif** rastgele gelir, **1'ini seçer**. Seçim gizlidir.
- **S-05 Gizli yerleşim (45 sn):**
  - Kule kendi başlangıç bölgesine konur.
  - **En az 3** karakter kendi başlangıç bölgesine konur.
  - İstenirse **enerji harcamadan** tuzak konabilir (C-31 sınırı geçerli, kendi yarısında).
  - Oyuncular birbirinin yerleşimini görmez.
- **S-06** Süre dolarsa eksikler otomatik tamamlanır: Kule ve gerekli sayıda karakter rastgele boş karolara konur.
- **S-07 Açılış:** İki yerleşim de tamamlanınca tüm birimler ve kuleler görünür olur (tuzaklar hariç). 1. raunt başlar.
- **S-08 Aynı cihazda 2 kişi modu:** A yerleştirir → "Telefonu rakibine ver" ekranı → B yerleştirir → açılış.

---

## 7. Tur yapısı (T)

- **T-01 Enerji:** Tur başında oyuncunun enerjisi **min(raunt numarası, 6)** değerine doldurulur. Harcanmayan enerji sonraki tura aktarılmaz.
- **T-02** Oyuncu B, **1. rauntta +1 enerji** alır (2 enerjiyle başlar).
- **T-03** Kaynak karosunda kendi birimi olan oyuncu tur başında +1 enerji alır. Bu bonus 6 sınırını aşabilir.
- **T-04 Tur başı sırası:**
  1. Enerji doldurulur (T-01…T-03).
  2. Tur başı etkileri uygulanır: Şifa (U-05), Zehir (C-17), Son Nefes (P-01).
  3. Kazanma kontrolü yapılır (W-01).
  4. Kart çekilir: Oyuncu Pazar'dan 1 kart alır **veya** 1 kör kart çeker (D-07 geçerli).
- **T-05 Aksiyonlar:** Her tur **2 aksiyon** vardır. Bir aksiyon, bir birimin **hareket etmesi** veya **saldırmasıdır**. Bir birim bir turda en fazla 1 kez hareket, 1 kez saldırı yapabilir. Sıra serbesttir (önce saldırı, sonra hareket olabilir).
- **T-06 Kart oynama:** Enerji yettiği sürece turda istenen sayıda kart oynanabilir (C-01).
- **T-07 Karakter çıkarma:** Eldeki bir karakter **2 enerjiyle** tahtaya çıkarılır. Aksiyon harcamaz. Sadece kendi başlangıç bölgesinde veya kendi kulesine komşu boş bir karoya konabilir. Çıktığı tur hareket edemez ve saldıramaz.
- **T-08 Tur sonu:** Süreli etkilerin sayacı azalır (C-04). Görevler kontrol edilir (Q-02). Sıra rakibe geçer.
- **T-09 Süre:** Her tur **45 saniye**. Ek olarak her oyuncunun maç boyu **60 saniyelik süre bankası** vardır. İkisi de biterse tur otomatik sona erer. **Üst üste 3** otomatik tur sonu olursa oyuncu hükmen kaybeder. Yapay zekâya karşı modda süre ayarlardan kapatılabilir.
- **T-10** Bir raunt, A'nın turu ve ardından B'nin turundan oluşur.

---

## 8. Görevler (Q)

- **Q-01** Her oyuncunun 3 gizli görevi vardır (S-03).
- **Q-02** Görevler **kendi tur sonunda** kontrol edilir. Bu kuralın istisnası sadece "raunt sonu" yazan görevlerdir.
- **Q-03** Tamamlanan görev **rakibe gösterilir** ve kalıcı olarak tamamlanmış sayılır.
- **Q-04** Bazı görevler **başarısız olabilir**. Başarısız görev bir daha tamamlanamaz ve rakibe gösterilir.
- **Q-05** "Tut" ifadesi, koşulun oyuncunun **art arda 2 tur sonunda** sağlanması demektir.

| ID | Görev | Koşul | Başarısızlık |
|---|---|---|---|
| **Q-10** | Rün Bekçisi | Aynı rün taşını tut (üzerinde kendi birimin olsun) | – |
| **Q-11** | Çift Rün | Tur sonunda iki farklı rün taşında kendi birimin olsun | – |
| **Q-12** | Avcı | Toplam 2 düşman birimi yok et (kule sayılmaz) | – |
| **Q-13** | Kuşatma | Rakip kuleye toplam 5 hasar ver (engellenen hasar sayılmaz) | – |
| **Q-14** | Sağlam Kale | 8. raunt sonunda kulenin Can değeri 7 veya üstü olsun | 8. raunt sonunda Can < 7 |
| **Q-15** | Biyom Ustası | Tur sonunda 3 birimin aynı anda kendi biyomunda dursun | – |
| **Q-16** | Kayıpsız | 6. raunt sonuna kadar hiç birim kaybetme | Bundan önce herhangi bir birim ölümü |
| **Q-17** | Tuzakçı | Kendi tuzağın bir düşman birimde tetiklensin | – |
| **Q-18** | Kaynak Lordu | Kaynak karosunu tut | – |
| **Q-19** | Derin Akın | Tur sonunda rakibin başlangıç bölgesinde 2 birimin olsun | – |

---

## 9. Komutan pasifleri (P)

- **P-00** Pasif gizlidir. **İlk tetiklendiği anda** iki oyuncuya da gösterilir.

| ID | Pasif | Etki |
|---|---|---|
| **P-01** | Son Nefes | İlk ölen birimin, ölümünden sonraki ilk kendi tur başında kendi başlangıç bölgende rastgele boş bir karoda 2 Can ile geri gelir. Etkileri yoktur. Maçta 1 kez çalışır |
| **P-02** | Tuzak Ustası | Tuzakların tetiklendiğinde hedef ek 1 hasar alır |
| **P-03** | Hızlı Başlangıç | 3. rauntta +2 enerji alırsın (6 sınırını aşabilir) |
| **P-04** | Kalın Duvar | Kulene gelen ilk 3 hasar (toplam) engellenir |
| **P-05** | Pazarcı | Pazar'dan ilk kart aldığında ek olarak 1 kör kart çekersin (D-07 geçerli). Maçta 1 kez çalışır |
| **P-06** | Portal Bekçisi | Rakip bir birim Portal'a her girdiğinde 3 hasar alır |

---

## 10. Dinamik harita olayları (E)

- **E-01** Olaylar **3, 6, 9 ve 12. rauntların başında** (A'nın turu başlamadan) gerçekleşir.
- **E-02** Olay, **bir önceki raundun başında** duyurulur: 2, 5, 8 ve 11. rauntlar. Etkilenecek karolar tahtada vurgulanır.
- **E-03** Olay türü rastgele seçilir. Etkilediği karolar her zaman **eş karo çifti** olarak seçilir, böylece simetri korunur.
- **E-04** Olay duyurulduğu anda karolar belirlenir. Olay anında bir karo artık uygun değilse (ör. üzerine birim gelmişse), o eş çift atlanır.

| ID | Olay | Etki |
|---|---|---|
| **E-10** | Deprem | Başlangıç bölgeleri dışında, boş ve özel olmayan 1 eş karo çifti Kaya olur (B-14). Karodaki tuzaklar yok olur |
| **E-11** | Biyom Kayması | Bir karo ve komşularından oluşan en fazla 7 karoluk bir küme (ve eşi) rastgele başka bir biyoma dönüşür. Kümenin merkezi Portal'a en az 3 mesafededir, böylece küme eşiyle çakışmaz. Tahta dışındaki karolar ve Portal atlanır. Özel işaretler korunur |
| **E-12** | Rün Yağmuru | Başlangıç bölgeleri dışında, boş ve özel olmayan 1 eş karo çiftinde yeni Rün Taşı belirir |

---

## 11. Kazanma koşulları (W)

- **W-01 Portal zaferi:** Bir oyuncu **2 görevi tamamladığında** Portal onun için açılır. Portal açıkken oyuncunun bir birimi **kendi tur sonunda** Portal'da durur ve **bir sonraki tur başında hâlâ orada** ise oyuncu kazanır. Rakip birimleri Portal'da durarak girişi engelleyebilir.
- **W-02 Kule zaferi:** Rakibin kulesinin Can değeri 0'a inerse kazanırsın. Bu kontrol anlık yapılır.
- **W-03 Raunt sınırı:** 15. raunt sonunda kazanan yoksa sırasıyla şu ölçütlere bakılır:
  1. Kule Can'ı yüksek olan,
  2. Tamamlanan görevi fazla olan,
  3. Tahtadaki birimlerin toplam Can'ı yüksek olan kazanır.
  4. Hepsi eşitse berabere.
- **W-04** Süre cezasıyla hükmen mağlubiyet (T-09).

---

## 12. Yapay zekâ rakip (AI)

- **AI-01** Yapay zekâ **yalnızca kendi görebildiği bilgiyi** kullanır (`PlayerView`). Rakibin eli, görevleri, pasifi ve tuzakları gizlidir.
- **AI-02 Yöntem:** Açgözlü (greedy) değerlendirme. Olası her aksiyon ve kart oynama için bir puan hesaplanır: verilen hasar, öldürme, görev ilerlemesi, portala uzaklık, kendi kulesinin tehdidi, biyom bonusu ve enerji verimliliği. Tur içinde adım adım en iyi hamle seçilir.
- **AI-03 Zorluk:** **Kolay** (en iyi 3 hamleden rastgele seçer) ve **Normal** (en iyi hamleyi seçer).
- **AI-04 Yerleşim:** Kuleyi arka sıraya koyar, Muhafız'ı kulenin yanına koyar, diğerlerini dağıtır.
- **AI-05** Hamle süresi, mobilde akıcı his için en fazla 1 saniye/hamle. Gerekirse animasyon arkasında hesaplar.

---

## 13. Arayüz ve deneyim (UX)

- **UX-01** Portre ekran, tek elle oynanabilir. Dokunma alanları en az **48 dp**.
- **UX-02** Birime dokununca gidilebilecek karolar (mavi) ve saldırılabilecek hedefler (kırmızı) vurgulanır.
- **UX-03** Kart, birimin üzerine **sürüklenerek** oynanır. Uzun basınca kart detayı açılır.
- **UX-04** Ekran düzeni (yukarıdan aşağıya):
  - Rakip bilgisi: kule Can'ı, el sayısı, tamamlanan görevler
  - Tahta
  - Pazar (3 slot) ve kör çekme butonu
  - Kendi elin
  - Enerji ve aksiyon göstergesi, tur süresi, "Turu Bitir" butonu
- **UX-05** Görevler ve komutan pasifi, köşedeki bir ikondan açılan panelde görünür.
- **UX-06** Olay duyurusu, tahtanın üstünde bir şerit ve karolarda nabız efektiyle gösterilir.
- **UX-07** Kart görünümü minimal: ikon, isim, 3 sayı (veya enerji maliyeti), tek satır açıklama, nadirlik rengi.
- **UX-08 Öğretici (Faz 2):** 3 adım: hareket ve saldırı → kart oynama → görev ve portal.

---

## 14. Görsel ve ses (A)

- **A-01** Low poly, canlı ve doygun renkler, yumuşak gölgeler (URP).
- **A-02** 5 karakter modeli + 1 kule. Biyom varyantları **renk paleti değişimiyle** yapılır. Biyom renkleri: Orman yeşil, Çöl turuncu, Kar açık mavi.
- **A-03** Karo setleri: 3 biyom karosu, Rün Taşı işareti, Kaynak işareti, Portal, Kaya.
- **A-04** Ses (Faz 2): Tıklama, hareket, vuruş, ölüm, kart oynama, tuzak, olay ve zafer sesleri.

---

## 15. Teknik mimari (özet)

Detaylar `CLAUDE.md` ve `.claude/rules/` altındadır.

- **Unity 6 LTS, URP**, önce Android, portre yönü.
- **Kural motoru (`Assets/_Project/Core`):**
  - Saf C#, UnityEngine bağımlılığı yok.
  - Deterministik: aynı tohum ve aynı komut dizisi her zaman aynı sonucu verir.
  - Komut tabanlı (`MoveCommand`, `AttackCommand` vb.).
  - Olay yayınlar (`UnitMoved`, `DamageDealt` …). Unity katmanı bu olayları dinleyip animasyon oynatır.
- **Testler:** `Tools/Engine.Tests` projesi, Core kaynaklarını derleyip `dotnet test` ile Unity açmadan çalıştırır.
- **Simülasyon:** `Tools/Sim`, yapay zekâya karşı yapay zekâ ile toplu maç oynatır ve denge istatistiklerini üretir.
- **İçerik verisi:** Tüm sayılar tek dosyadadır: `Core/Data/Catalog.cs`. Bu dosya bu GDD ile birebir uyumlu olmalıdır.
- **Online (Faz 3):** Sunucu yetkili mimari. Aynı Core kodu sunucuda çalışır. Aday: Azure üzerinde ASP.NET Core + SignalR.

---

## 16. Yol haritası

| Kilometre taşı | Kapsam | Bitti sayılması için |
|---|---|---|
| **M0** İskelet | Klasörler, asmdef dosyaları, test ve simülasyon projeleri, boş Catalog | `dotnet test` yeşil, Unity projesi hatasız derleniyor |
| **M1** Tahta | Altıgen matematiği, 59 karo, simetrik harita üretimi (B-*) | Simetri ve dağılım testleri, 1000 tohumda doğrulama |
| **M2** Birimler | Hareket kalıpları, savaş, Siper, yetenekler (U-*) | Her U kuralı için test |
| **M3** Kartlar | Enerji, havuz, dağıtım, Pazar, destek kartları, tuzaklar (C-*, D-*, T-*) | Her kural için test |
| **M4** Maç | Hazırlık, görevler, pasifler, olaylar, kazanma koşulları (S-*, Q-*, P-*, E-*, W-*) | Baştan sona tam bir maç testte oynanıyor |
| **M5** Yapay zekâ ve simülasyon | Açgözlü yapay zekâ, Sim aracı (AI-*) | 1000 maç çökmeden oynanıyor. A/B kazanma oranı %45–55 arasında |
| **M6** Gri kutu istemci | Tahta çizimi, dokunmatik giriş, arayüz, el, Pazar, süre, aynı cihazda 2 kişi, yapay zekâya karşı (UX-*) | Android cihazda baştan sona maç oynanabiliyor |
| **M7** Demo cilası (Faz 2) | Low poly sanat, animasyon, ses, öğretici | Yabancı biri yardımsız bir maçı bitirebiliyor |

---

## 17. Açık sorular

- Oyunun kalıcı adı.
- Kolay/Normal dışında zorluk seviyesi gerekli mi?
- Faz 3 online altyapısı: kendi sunucumuz (Azure + SignalR) mı, hazır bir servis mi?

---

## Değişiklik Günlüğü

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-09-25 | İlk sürüm. Kararlar: Pazar 3 slot (Karakter/Buff/Debuff-Tuzak), online Faz 3'e ertelendi, kule saldırmaz |
