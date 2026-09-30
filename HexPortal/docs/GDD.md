# HexPortal — Oyun Tasarım Dokümanı (GDD)

> Sürüm: 2.4 · Tarih: 2026-09-30 · Durum: Prototip (Faz 1) kapsamı onaylandı
> "HexPortal" çalışma adıdır, sonra değişebilir.

Bu doküman oyunun **tek doğruluk kaynağıdır**. Her kuralın kalıcı bir kimliği vardır (ör. `U-04`). Kod, testler ve commit mesajları bu kimliklere referans verir. Kimlikler **asla yeniden numaralandırılmaz**. Bir kural kalkarsa "KALDIRILDI" diye işaretlenir, yeni kural yeni numara alır. Değişiklikler en alttaki **Değişiklik Günlüğü**'ne yazılır.

---

## 0. Özet

- **Tür:** Sıra tabanlı, 1v1, altıgen tahtada kart + taktik strateji.
- **Platform:** Mobil (önce Android), dikey (portre) ekran.
- **Görsel stil:** Renkli, sevimli, canlı; low poly 3D tahta ve birimler, minimal 2D kartlar.
- **Maç süresi hedefi:** 10–12 dakika.
- **Oyun akışı:** Oyuncular kulelerini ve birimlerini rakibe göstermeden yerleştirir. Maç **sis altında** başlar: rakibin yarısı keşfedilene kadar gizlidir. Oyuncular ortak havuzdan kart çeker, kartları **Mana** ile tahtaya sürer, birimlerini **Enerji** harcayarak hareket ettirir veya saldırtır ve gizli görevlerini tamamlar. 3 görevden 2'sini bitiren oyuncu için merkezdeki portal açılır. Portala girip rakibin bir turunu hayatta atlatan oyuncu kazanır. Rakibin kulesini yıkmak da kazandırır.

### 0.1 Kapsam dışı (Faz 1 ve Faz 2)

Online mod (Faz 3), deste oluşturma ve koleksiyon, 2'den fazla oyuncu, karşı saldırı, Gölge sınıfı, Sihir ve Arazi kartları, karakter özelleştirmeleri.

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
| Havuz | `Pool` | Enerji (hareket/saldırı kaynağı) | `Energy` |
| Mana (kart oynama kaynağı) | `Mana` | Maliyet (bir kartın Mana bedeli) | `Cost` |
| Aksiyon | `Action` | Görev | `Quest` |
| Komutan pasifi | `CommanderPassive` | Harita olayı | `MapEvent` |
| Saldırı / Can / Hareket | `Attack` / `Health` / `Move` | Nadirlik | `Rarity` |
| Görüş | `Sight` | Kontrol Alanı | `ControlZone` |
| Görünürlük | `Visibility` | Keşfedilmemiş / Keşfedilmiş / Görünür | `Hidden` / `Explored` / `Visible` |
| Son görülen hal | `LastSeen` | Nöbet | `Overwatch` |
| Kule atışı | `TowerShot` | Ön seçim (rakip turunda kart seçimi) | `DrawPick` |

---

## 2. Tahta (B)

- **B-01** Tahta 9 sıradan oluşur. Sıra uzunlukları üstten alta **7, 6, 7, 6, 7, 6, 7, 6, 7** karodur, toplam **59 karo**. Karolar sivri tepeli (pointy-top) altıgendir. Koordinat detayı için `hex-grid` skill'ine bakın.
- **B-02** Merkez karo (5. sıra, 4. karo) **Portal**'dır.
- **B-03** Alttaki 2 sıra (8–9) **Oyuncu A**'nın, üstteki 2 sıra (1–2) **Oyuncu B**'nin başlangıç bölgesidir (her biri 13 karo).
- **B-04** Tahta, merkeze göre **180° dönme simetrisine** sahiptir. Her karonun bir "eş karosu" vardır ve eş karolar her zaman aynı türdedir.
- **B-05** Her oyuncu tahtayı **kendi bölgesi altta** olacak şekilde görür. Oyuncu B'nin ekranında görünüm 180° döndürülür, kurallar değişmez.
- **B-06 Oyuncu yarıları:** Portal dışındaki 58 karo iki yarıya bölünür. **B'nin yarısı** B-21'deki "üst yarı"dır (29 karo). **A'nın yarısı** bunun eşidir (8–6. sıralar ve 5. sıranın Portal'ın sağındaki 3 karosu, 29 karo). Portal hiçbir yarıya ait değildir.

### 2.1 Karo türleri

| ID | Tür | Etki |
|---|---|---|
| **B-10** | Orman / Çöl / Kar | Kendi biyomunda saldıran birim +1 Saldırı alır (bkz. U-20) |
| **B-11** | Rün Taşı | Görev hedefi. Üzerindeki biyom geçerliliğini korur |
| **B-12** | Kaynak | Tur başında üzerinde kendi birimin varsa +1 Mana (bkz. T-03) |
| **B-13** | Portal | Kazanma karosu. Biyomu yoktur. Her zaman iki oyuncuya görünür (V-06) |
| **B-14** | Kaya | Geçilmez, üzerine birim konamaz. Sadece harita olaylarıyla oluşur |

Rün Taşı ve Kaynak, biyomun üstünde duran bir **işarettir**. Karonun biyomu korunur.

### 2.2 Harita üretimi

- **B-20** Harita tek bir tohum (seed) değerinden deterministik olarak üretilir. Aynı tohum her zaman aynı haritayı verir.
- **B-21** Önce "üst yarı" üretilir: 1–4. sıralar ve 5. sıranın portalın solundaki 3 karosu, toplam 29 karo. Alt yarı, eş karolardan kopyalanır.
- **B-22** Biyom dağılımı: Üst yarının 29 karosunda her biyom **en az 8** karo alır. Biyomlar küçük kümeler halinde dağılır:
  1. **Çekirdekler:** Her biyom için ayrı ayrı rastgele **3–5 çekirdek karo** seçilir (toplam 9–15), üst yarının rastgele, birbirinden farklı karolarına konur.
  2. **Yayılma:** Tüm karolar dolana kadar her adımda, boş komşusu olan biyomlar arasından **o an en az karoya sahip olan** seçilir (eşitlikte rastgele). Bu biyom, kendisine komşu boş karolardan rastgele birine yayılır.
  3. Amaç: Her bölgede her biyomdan birkaç karo bulunması ve B-24'teki yeniden üretimin nadir olması.
- **B-23** Özel karolar (her yarıda): **2 Rün Taşı** 3–4. sıralarda, **1 Kaynak** 3–4. sıralarda. Başlangıç bölgesinde özel karo olmaz. İki rün taşı birbirine komşu olmaz. **Kaynak hiçbir Rün Taşı'na komşu olmaz** (hedefler tek bir noktada toplanmasın).
- **B-24** Üretilen harita doğrulanır: Simetri tam olmalı, sayılar B-22 ve B-23'e uymalı. Uymazsa **aynı tohumun rastgele sayı akışına devam edilerek** yeniden üretilir (2. deneme, 3. deneme…). Böylece farklı tohumlar asla aynı haritayı vermez. Harita, istenen tohumu ve deneme sayısını saklar.

---

## 3. Birimler (U)

### 3.1 Sınıflar

Her sınıfın 3 biyom varyantı vardır. **Varyantların statları ve yetenekleri aynıdır**, tek fark biyomdur. 5 sınıf × 3 biyom = 15 benzersiz karakter kartı.

**Maliyet**, kartı tahtaya sürmek için ödenen **Mana**dır (T-07). Hareket ve saldırı ise **Enerji** harcar (T-05). İkisi ayrı kaynaklardır.

| ID | Sınıf | Maliyet (Mana) | Saldırı | Can | Hareket | Görüş | Saldırı menzili | Yetenek |
|---|---|---|---|---|---|---|---|---|
| **U-01** | Muhafız | 3 | 2 | 6 | 1 | 2 | Komşu | **Siper:** Bkz. U-11 |
| **U-02** | Süvari | 3 | 3 | 4 | 3 | 3 | Komşu | **Atik:** Hareket ederken birimlerin ve kulelerin üstünden geçer (U-09). **Hücum:** Bir önceki kendi turunda hareket ettiyse +1 Saldırı |
| **U-03** | Okçu | 2 | 2 | 3 | 2 | 3 | Düz hatta 1–3 | **Uzak atış:** Hat üzerindeki hiçbir şey (birim, kule, kaya) atışı engellemez |
| **U-04** | Büyücü | 3 | 2 | 3 | 2 | 2 | Mesafe 1–2 | **Patlama:** Hedefe komşu düşman birimleri ve kuleler 1 hasar alır |
| **U-05** | Şifacı | 2 | 1 | 3 | 1 | 2 | Komşu | **Şifa:** Kendi tur başında komşu dost birimler +1 Can alır. Birden çok Şifacı'nın etkisi toplanır, Şifacılar birbirini de iyileştirir. Şifacı kendini ve kuleyi iyileştirmez (U-24 tavanı geçerli) |

**Kule (U-06):** Her oyuncunun 1 kulesi vardır. Can **10**, Saldırı **2**, saldırı menzili **mesafe 1–2**, Görüş **2**. Hareket etmez. Bir karoyu kaplar ve geçişi engeller. Kule yalnızca **savunma amaçlı** saldırır (U-27). Kartlar kuleyi hedef alamaz. Kule biyom bonusu almaz. Kule yıkılırsa sahibi kaybeder (W-02).

### 3.2 Hareket

Altıgen tahtada her karonun 6 komşusu vardır, yani birimler zaten her yöne gidebilir. Bu yüzden ayrı "düz / çapraz / sıçrama" kalıpları yoktur: **tüm birimler aynı basit kuralla** hareket eder.

- **U-07 Hareket:** Bir hareket aksiyonunda (T-05) birim, **Hareket değeri kadar adım** atar. Her adımda 6 komşu karodan birine geçer ve yol boyunca yön değiştirebilir. Birim, kule veya kaya olan karolardan **geçemez** (Süvari hariç, U-09). Varış karosu boş olmalıdır. Pratikte: birimden en fazla Hareket adımda, dolu karolardan geçmeden ulaşılabilen her boş karo geçerli bir varış noktasıdır.
- ~~**U-08** Çapraz kalıp~~ **KALDIRILDI (v2.0).** Altıgen tahtada kenar komşuları zaten 6 yönü kapsıyor. Hareket artık U-07'deki tek kuraldır.
- **U-09 Süvari (Atik):** Süvari hareket ederken birimlerin ve kulelerin **üstünden geçebilir**. Kayadan geçemez. Varış karosu yine boş olmalıdır. (Eski "atın sıçrayışı" kalıbı v2.0'da kaldırıldı.)
- **U-10** Bir birim dolu karoda (birim, kule, kaya) duramaz. Portal'da durabilir.
- **U-11 Siper:** Bir Muhafız'a komşu olan dost birimler ve dost kule, **saldırılarla** hedef alınamaz. **Muhafız'lar Siper'den hiçbir zaman faydalanmaz:** Başka bir Muhafız'a komşu olsalar bile saldırıyla hedef alınabilirler (saldırıya kapalı, yenilmez gruplar oluşmasın). Siper yalnızca saldıran tarafa **Görünür** olan Muhafız'larla çalışır (V-11): Sis altındaki bir Muhafız, görünür bir hedefi saldırıya karşı korumaz. Büyücü'nün Patlama hasarı ve kartlar Siper'i yok sayar. Nöbet ve kule atışları da saldırıdır, Siper'e tabidir.
- **U-12 Görünürlük sınırı:** Hareketin yolu ve varış karosu, hareket eden oyuncu için **Görünür** (V-01) karolardan oluşmalıdır. Keşfedilmiş ama şu an görünmeyen veya keşfedilmemiş karolara hareket edilemez. Böylece geçerli hamle listesi gizli bilgiyi sızdırmaz.

### 3.3 Savaş

- **U-20 Hasar:** Saldırı hasarı = Saldırı statı + biyom bonusu + buff'lar − debuff'lar, en az 0. Biyom bonusu, **saldıran birim** kendi biyomundaki bir karodayken geçerlidir.
- **U-21** Karşı saldırı yoktur. (Nöbet ve kule atışı karşı saldırı değildir; kendi tetik kuralları vardır, U-27 ve U-28.)
- **U-22** Can 0'a inen birim ölür ve tahtadan kalkar. Üzerindeki tüm etkiler silinir.
- **U-23 Ölüm çekişi:** Birimi ölen oyuncu hemen 1 kör kart çeker (el doluysa çekmez).
- **U-24** Can, iyileşme ile başlangıç değerini aşamaz.
- **U-25** Okçu komşu karodaki düşmana da saldırabilir (menzil 1–3).
- **U-26 Kuleye saldırı:** Birimler rakip kuleye saldırabilir. Kule; menzil, Siper (U-11) ve görünürlük (V-07) açısından normal bir hedeftir. Q-13 ve W-02 bu hasarı sayar.
- **U-27 Kule savunması (kule atışı):** Kule her zaman savunmadadır ve **rakibin her turunda en fazla 1 kez**, **tek bir** rakip birime otomatik saldırır. Tetik: rakibin turunda bir rakip birim, kulenin saldırı menzilinde **hareketini bitirirse**, **tahtaya çıkarsa** (T-07) veya **menzilindeyken saldırı yaparsa**. Tetikleyen birim hedef alınır. Hedef geçerli değilse (Siper, görünmüyor) kule ateş etmez ve hakkını korur. Tetik listesi kapalıdır: İtme veya ışınlanmayla (C-15, C-18, C-21) menzile gelen birim tetik oluşturmaz. Tuzaklar bundan ayrıdır (C-32). Kule atışı **her zaman 2 hasar** verir (kule statı). Biyom bonusu, buff veya debuff almaz. Hedef tarafındaki etkiler geçerlidir: Kalkan (C-12) hasarı engeller. Oyuncunun girdi vermesi gerekmez.
- **U-28 Nöbet:** Bir birim, kendi turunda aksiyonu olarak (1 Enerji, T-05) **nöbete** geçebilir. Rakibin bir sonraki turu boyunca, U-27'deki aynı tetiklerle, saldırı menzilinde ve görüşünde olan **ilk** rakip birime 1 kez otomatik saldırır (U-20). Nöbet ateş edince veya rakibin turu bitince sona erer. Nöbetteki birim itilir veya ışınlanırsa nöbet bozulur. Nöbet durumu, birim rakibe görünürse rakibe de gösterilir.
- **U-29 Tetik sırası:** Bir olay birden fazla nöbetçiyi tetiklerse önce kule, sonra birim kimliği sırasıyla ateş edilir. Hedef ölürse kalan nöbetçiler ateş etmez ve nöbette kalır. Saldırı tetikli atışlar, tetikleyen saldırı çözüldükten sonra yapılır.

---

## 4. Destek kartları (C)

### 4.1 Genel kurallar

- **C-01** Destek kartı oynamak **aksiyon ve Enerji harcamaz**; kartın **Maliyeti** Mana ile ödenir.
- **C-02 Yakınlık (Kontrol Alanı):** **Tüm kartlar** (karakter kartları dahil) yalnızca oyuncunun **Kontrol Alanı** içinde oynanabilir. Kontrol Alanı; oyuncunun tahtadaki birimlerinin ve kulesinin bulunduğu karolar ile bunlara **komşu** (mesafe ≤ 1, Catalog'da `ControlRange`) karolardır. Hiç birimi veya kulesi olmayan bir bölgeye kart oynanamaz.
  - Karakter kartı: Kontrol Alanındaki boş bir karoya konur (T-07).
  - Buff: Dost bir birimi hedefler (dost birim her zaman kendi Kontrol Alanındadır).
  - Debuff: Kontrol Alanındaki ve **Görünür** (V-07) bir düşman birimi hedefler.
  - Tuzak: Kontrol Alanındaki boş bir karoya konur (C-30).
  - Konum seçen kartlar (Işınlanma, İtme'nin hedefi): Seçilen karo/hedef Kontrol Alanında olmalıdır.
- **C-03** Süre türleri: **Anlık** (hemen uygulanır, iz bırakmaz), **2 tur**, **Kalıcı** (birim ölene veya etki tetiklenene kadar).
- **C-04** "2 tur" sayacı, **etkilenen birimin sahibinin her tur sonunda** 1 azalır. 0 olunca etki biter. Kendi turunda oynadığın buff o turu da sayar. Rakibe oynadığın debuff, rakibin sonraki 2 turu boyunca sürer.
- **C-05** Bir birim aynı anda en fazla **1 buff** ve **1 debuff** taşır (süreli veya kalıcı). Anlık etkiler sayılmaz. Buff'ı olan birime yeni bir buff oynanırsa **yenisi eskisinin yerini alır** (eski etki silinir). Debuff için de aynısı geçerlidir.
- **C-06** Aynı kart aynı birime tekrar oynanırsa etkiler **toplanmaz**; süre baştan başlar (C-05'e göre eskisinin yerini alır).

### 4.2 Kart listesi

| ID | Kart | Kategori | Etki | Süre | Maliyet (Mana) | Nadirlik | Havuzdaki kopya |
|---|---|---|---|---|---|---|---|
| **C-10** | Öfke | Güç (Buff) | +2 Saldırı | 2 tur | 1 | Sıradan | 4 |
| **C-11** | Dev Gücü | Güç (Buff) | +1 Saldırı | Kalıcı | 2 | Nadir | 2 |
| **C-12** | Kalkan | Koruma (Buff) | Birime gelen bir sonraki hasarı tamamen engeller, sonra biter | Kalıcı | 1 | Sıradan | 4 |
| **C-13** | Şifa İksiri | Koruma (Buff) | +3 Can | Anlık | 1 | Sıradan | 4 |
| **C-14** | Rüzgâr Adımı | Hareket (Buff) | Bu tur +2 Hareket | Anlık | 1 | Sıradan | 4 |
| **C-15** | Işınlanma | Hareket (Buff) | Dost birimi, Kontrol Alanındaki (C-02) Portal hariç boş bir karoya taşır. Enerji harcamaz ve birimin aksiyonunu kullanmaz | Anlık | 3 | Epik | 1 |
| **C-16** | Zayıflık | Lanet (Debuff) | −2 Saldırı | 2 tur | 1 | Sıradan | 4 |
| **C-17** | Zehir | Lanet (Debuff) | Sahibinin tur başında −1 Can | 2 tur | 2 | Nadir | 2 |
| **C-18** | İtme | Kontrol (Debuff) | Hedefi seçilen bir düz yönde 2 karo iter. Engele çarparsa durur, hasar olmaz. Durduğu karodaki tuzak tetiklenir | Anlık | 1 | Sıradan | 4 |
| **C-19** | Kök Salma | Kontrol (Debuff) | Hedef hareket edemez ama saldırabilir | 2 tur | 2 | Nadir | 2 |
| **C-20** | Diken Tuzağı | Tuzak | Tetikleyen birim 3 hasar alır | Kalıcı | 1 | Nadir | 2 |
| **C-21** | Ayna Tuzağı | Tuzak | Tetikleyen birim, kendi başlangıç bölgesinde rastgele boş bir karoya ışınlanır | Kalıcı | 2 | Epik | 1 |

Kopya toplamı: 6 sıradan × 4 + 4 nadir × 2 + 2 epik × 1 = **34 kart**.

### 4.3 Tuzaklar

- **C-30** Tuzak, kapalı olarak Kontrol Alanındaki (C-02) **boş bir karoya** konur. Portal, kule ve kaya karoları hariç. Konulduğu anda karoda birim olmamalıdır.
- **C-31** Bir oyuncunun aynı anda en fazla **2 aktif tuzağı** olabilir.
- **C-32** Tuzak yalnızca **rakip** bir birim o karoda **durduğunda** tetiklenir: hareketin varış karosu, itmenin durduğu karo veya ışınlanmanın varış karosu. Yoldan geçmek tetiklemez. Tuzak tek kullanımlıktır.
- **C-33** Rakip tuzaklar, karo Görünür olsa bile görünmez. Tetiklenince iki oyuncuya da gösterilir.
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
  - Önce kule kendi başlangıç bölgesine konur.
  - **En az 3** karakter kendi başlangıç bölgesine konur. Hazırlıkta **Maliyet ödenmez**. Başlangıç bölgesi hazırlığa özel bir istisnadır; maç başladıktan sonra karakterler yalnızca Kontrol Alanına çıkar (T-07).
  - İstenirse **Mana harcamadan** tuzak konabilir: kendi yarısında (B-06) ve o ana kadar yerleştirilen kendi birimlerinin/kulesinin Kontrol Alanında (C-02). C-31 sınırı geçerlidir.
  - Oyuncular birbirinin yerleşimini görmez.
- **S-06** Süre dolarsa eksikler otomatik tamamlanır: Kule ve gerekli sayıda karakter rastgele boş karolara konur.
- **S-07 Açılış:** İki yerleşim de tamamlanınca 1. raunt **sis altında** başlar (V-04). Her oyuncu yalnızca kendi birimlerini, kendi yarısının arazisini ve birimlerinin görüş alanındakileri görür. **Rakibin yarısı keşfedilene kadar gizli kalır**; rakip birimler ve kule ancak görüş alanına girince görünür olur. Tuzaklar her zaman gizlidir (C-33).
- **S-08 Aynı cihazda 2 kişi modu:** A yerleştirir → "Telefonu rakibine ver" ekranı → B yerleştirir → açılış. Sis nedeniyle **her tur geçişinde** de bu ekran gösterilir ve bir sonraki oyuncu dokunana kadar tahta gizli kalır.

---

## 7. Tur yapısı (T)

İki ayrı kaynak vardır:
- **Mana** → kart oynamak için (karakter çıkarma ve destek kartları). Her kartın kendi **Maliyeti** vardır.
- **Enerji** → tahtadaki birimleri hareket ettirmek, saldırtmak veya nöbete koymak için.

- **T-01 Mana:** Tur başında oyuncunun Manası **min(raunt numarası, 6)** değerine doldurulur. Harcanmayan Mana sonraki tura aktarılmaz.
- **T-02** Oyuncu B, **1. rauntta +1 Mana** alır (2 Mana ile başlar).
- **T-03** Kaynak karosunda kendi birimi olan oyuncu tur başında +1 Mana alır. Bu bonus 6 sınırını aşabilir.
- **T-04 Tur başı sırası:**
  1. Mana ve Enerji doldurulur (T-01…T-03, T-05).
  2. Tur başı etkileri uygulanır: Şifa (U-05), Zehir (C-17), Son Nefes (P-01).
  3. Kazanma kontrolü yapılır (W-01).
  4. Kart çekilir: Oyuncu Pazar'dan 1 kart alır **veya** 1 kör kart çeker (D-07 geçerli). Seçim rakibin turunda önceden yapıldıysa (T-11) o uygulanır.
- **T-05 Enerji ve aksiyon:** Tur başında Enerji **3**'e doldurulur (Catalog'da `EnergyPerTurn`). Harcanmayan Enerji aktarılmaz. Tahtadaki her birim kendi turunda **en fazla 1 aksiyon** yapar: **hareket** (U-07), **saldırı** veya **nöbet** (U-28). Her aksiyon **1 Enerji** harcar. Bir birim aynı turda hem hareket edip hem saldıramaz.
- **T-06 Kart oynama:** Mana yettiği sürece turda istenen sayıda kart oynanabilir (C-01, C-02). Kart oynamak Enerji harcamaz.
- **T-07 Karakter çıkarma:** Eldeki bir karakter, kartın **Maliyeti** (§3.1) kadar Mana ödenerek tahtaya çıkarılır. Enerji ve aksiyon harcamaz. Kontrol Alanında (C-02) Portal hariç boş bir karoya konur. Çıktığı tur aksiyon yapamaz.
- **T-08 Tur sonu:** Süreli etkilerin sayacı azalır (C-04). Görevler kontrol edilir (Q-02). Sıra rakibe geçer. Rakibin nöbetleri (U-28) bu turun sonunda biter.
- **T-09 Süre:** Her tur **30 saniye**. Ek olarak her oyuncunun maç boyu **60 saniyelik süre bankası** vardır. İkisi de biterse tur otomatik sona erer. **Üst üste 3** otomatik tur sonu olursa oyuncu hükmen kaybeder. Yapay zekâya karşı modda süre ayarlardan kapatılabilir.
- **T-10** Bir raunt, A'nın turu ve ardından B'nin turundan oluşur.

### 7.1 Rakibin turunda (bekleme süresi)

Amaç: Turunu bitiren oyuncu boş beklemesin. Rakibin turu hem izlenecek hem de karar verilecek bir an olsun.

- **T-11 Ön seçim:** Rakibin turu sırasında oyuncu, **bir sonraki turunun kart çekişini** (Pazar'daki bir slot veya kör çekme) önceden seçebilir. Seçim rakibin turu bitene kadar değiştirilebilir ve kendi tur başında (T-04 adım 4) uygulanır. Seçilen Pazar kartı o ana kadar değişmişse oyuncu tur başında yeniden seçer. Aynı cihazda 2 kişi modunda bu adım kendi tur başında yapılır.
- **T-12 Canlı izleme ve planlama:** Rakibin hamleleri, oyuncunun görebildiği kadarıyla (V-*) anında animasyonla gösterilir. Oyuncu bu sırada kartlarını, görevlerini ve birimlerinin menzillerini inceleyebilir, tahtaya plan okları çizebilir (UX-10). Planlama oyun durumunu değiştirmez.
- **T-13 Savunma tepkileri:** Oyuncunun kulesi (U-27) ve nöbetteki birimleri (U-28) rakibin turunda otomatik ateş eder. Böylece oyuncunun kendi turunda verdiği kararlar rakibin turunda sonuç verir.

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
| **Q-12** | Avcı | Toplam 2 düşman birimi yok et (kule sayılmaz; kulenin ve nöbetin öldürdükleri de sayılır) | – |
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
| **P-03** | Hızlı Başlangıç | 3. rauntta +2 Mana alırsın (6 sınırını aşabilir) |
| **P-04** | Kalın Duvar | Kulene gelen ilk 3 hasar (toplam) engellenir |
| **P-05** | Pazarcı | Pazar'dan ilk kart aldığında ek olarak 1 kör kart çekersin (D-07 geçerli). Maçta 1 kez çalışır |
| **P-06** | Portal Bekçisi | Rakip bir birim Portal'a her girdiğinde 3 hasar alır |

---

## 10. Dinamik harita olayları (E)

- **E-01** Olaylar **3, 6, 9 ve 12. rauntların başında** (A'nın turu başlamadan) gerçekleşir.
- **E-02** Olay, **bir önceki raundun başında** duyurulur: 2, 5, 8 ve 11. rauntlar. Etkilenecek karolar tahtada vurgulanır.
- **E-03** Olay türü rastgele seçilir. Etkilediği karolar her zaman **eş karo çifti** olarak seçilir, böylece simetri korunur.
- **E-04** Olay duyurulduğu anda karolar belirlenir. Olay anında bir karo artık uygun değilse (ör. üzerine birim gelmişse), o eş çift atlanır.
- **E-05** Olay duyuruları ve karo türü değişiklikleri **sisten bağımsız** olarak iki oyuncuya gösterilir. Değişen karolar, oyuncunun keşif hafızasında yeni türleriyle güncellenir (üzerlerindeki birimler açığa çıkmaz).

| ID | Olay | Etki |
|---|---|---|
| **E-10** | Deprem | Başlangıç bölgeleri dışında, boş ve özel olmayan 1 eş karo çifti Kaya olur (B-14). Karodaki tuzaklar yok olur |
| **E-11** | Biyom Kayması | Bir karo ve komşularından oluşan en fazla 7 karoluk bir küme (ve eşi) rastgele başka bir biyoma dönüşür. Kümenin merkezi Portal'a en az 3 mesafededir, böylece küme eşiyle çakışmaz. Tahta dışındaki karolar ve Portal atlanır. Özel işaretler korunur |
| **E-12** | Rün Yağmuru | Başlangıç bölgeleri dışında, boş ve özel olmayan 1 eş karo çiftinde yeni Rün Taşı belirir |

---

## 11. Görünürlük ve sis (V)

- **V-01 Üç görünürlük durumu:** Her oyuncu her karoyu şu 3 durumdan birinde görür:
  | Durum | Kod | Oyuncu ne görür |
  |---|---|---|
  | **Keşfedilmemiş (gizli)** | `Hidden` | Hiçbir şey: karo bulutla kaplıdır, arazi ve üzerindekiler bilinmez |
  | **Keşfedilmiş, görüş dışında** | `Explored` | Karoyu ve üzerindekileri **en son gördüğü halde**, soluk (inaktif) renkte görür. Bu bilgi eskimiş olabilir: orada gördüğü birim artık gitmiş olabilir |
  | **Görünür (aktif)** | `Visible` | Karonun ve üzerindekilerin **şu anki** gerçek durumu |
- **V-02 Görüş:** Her birim ve kule, Görüş değeri (§3.1) kadar mesafedeki tüm karoları görür. Görüş hattını hiçbir şey engellemez (kaya dahil). Görünür olan her karo kalıcı olarak Keşfedilmiş olur.
- **V-03 Güncelleme:** Görünürlük her olaydan sonra anında yeniden hesaplanır (hareket, çıkarma, ölüm, itme, ışınlanma). Görüş dışına çıkan karo `Explored` durumuna düşer ve **o anki hali** "son görülen hal" olarak saklanır.
- **V-04 Açılış görünürlüğü:** Her oyuncu maça **kendi yarısı** (B-06) Keşfedilmiş olarak başlar. Rakibin yarısı, birimlerin görüşüne girene kadar **Keşfedilmemiş**tir. Rakip birimleri ve kulesi hiçbir durumda baştan gösterilmez.
- **V-05 Gizli bilgi:** Rakip birimleri, kulesi, üzerlerindeki etkiler ve nöbet durumu yalnızca Görünür karolarda gösterilir. Keşfedilmiş karolarda son görülen hali "hayalet" olarak gösterilir. Tuzaklar Görünür karoda bile gizlidir (C-33).
- **V-06 Portal:** Portal karosu ve üzerindeki birim **her zaman iki oyuncuya Görünür**dür (W-01'in adil olması için).
- **V-07 Hedefleme:** Saldırı, nöbet, kule atışı ve debuff kartları yalnızca **Görünür** hedeflere yapılır. Hareket için U-12 geçerlidir.
- **V-08 Açığa çıkma:** Saldırı yapan birim veya kule, saldırdığı andan itibaren **rakibin bir sonraki tur sonuna kadar** rakibe Görünür olur (nöbet veya kule atışı rakibin turunda yapıldıysa, o turun sonuna kadar). Büyücü'nün Patlama hasarını alan gizli birimler açığa çıkmaz.
- **V-09 Kamu bilgisi:** Tamamlanan/başarısız görevler, açılan pasifler, tetiklenen tuzaklar, olay duyuruları, el sayısı ve kule Can değerleri sisten bağımsız olarak gösterilir.
- **V-10 Uygulama:** Her oyuncunun keşif haritası ve son görülen halleri Core'da oyun durumunun bir parçası olarak tutulur. Sisli görünümü yalnızca `PlayerView` üretir. Geçerli hamle listesi de yalnızca oyuncunun görebildiği bilgiye dayanır.
- **V-11 Görünen bilgi ilkesi:** Bir hamlenin yasallığı ve bir hedefin geçerliliği (Siper dahil) yalnızca hamleyi yapan tarafın **Görünür** bilgisine göre belirlenir. Kule atışı ve nöbette bu taraf, kulenin ya da birimin sahibidir. Görünmeyen birimler başkasının seçeneklerini ve saldırıların sonucunu etkilemez. Tek istisna tuzaklardır (C-32, C-33).

---

## 12. Kazanma koşulları (W)

- **W-01 Portal zaferi:** Bir oyuncu **2 görevi tamamladığında** Portal onun için açılır. Portal açıkken oyuncunun bir birimi **kendi tur sonunda** Portal'da durur ve **bir sonraki tur başında hâlâ orada** ise oyuncu kazanır. Rakip birimleri Portal'da durarak girişi engelleyebilir. Portal her zaman görünürdür (V-06).
- **W-02 Kule zaferi:** Rakibin kulesinin Can değeri 0'a inerse kazanırsın. Bu kontrol anlık yapılır.
- **W-03 Raunt sınırı:** 15. raunt sonunda kazanan yoksa sırasıyla şu ölçütlere bakılır:
  1. Kule Can'ı yüksek olan,
  2. Tamamlanan görevi fazla olan,
  3. Tahtadaki birimlerin toplam Can'ı yüksek olan kazanır.
  4. Hepsi eşitse berabere.
- **W-04** Süre cezasıyla hükmen mağlubiyet (T-09).

---

## 13. Yapay zekâ rakip (AI)

- **AI-01** Yapay zekâ **yalnızca kendi görebildiği bilgiyi** kullanır (`PlayerView`): sis (V-*) dahil. Rakibin eli, görevleri, pasifi, tuzakları ve görüş dışındaki birimleri gizlidir. Keşfedilmiş karolarda yalnızca son görülen hali bilir.
- **AI-02 Yöntem:** Açgözlü (greedy) değerlendirme. Olası her aksiyon ve kart oynama için bir puan hesaplanır: verilen hasar, öldürme, görev ilerlemesi, portala uzaklık, kendi kulesinin tehdidi, biyom bonusu, Mana ve Enerji verimliliği, keşif (gizli bölgeyi açma) ve nöbet değeri. Tur içinde adım adım en iyi hamle seçilir.
- **AI-03 Zorluk:** **Kolay** (en iyi 3 hamleden rastgele seçer) ve **Normal** (en iyi hamleyi seçer).
- **AI-04 Yerleşim:** Kuleyi arka sıraya koyar, Muhafız'ı kulenin yanına koyar, diğerlerini dağıtır.
- **AI-05** Hamle süresi, mobilde akıcı his için en fazla 1 saniye/hamle. Gerekirse animasyon arkasında hesaplar.
- **AI-06** Yapay zekâ da rakibin turunda ön seçimini (T-11) yapar. İnsanın turunda "düşünme" gecikmesi yaratmaz.

---

## 14. Arayüz ve deneyim (UX)

- **UX-01** Portre ekran, tek elle oynanabilir. Dokunma alanları en az **48 dp**.
- **UX-02** Birime dokununca gidilebilecek karolar (mavi), saldırılabilecek hedefler (kırmızı) ve bir **"Nöbet"** butonu gösterilir. Aksiyonunu kullanmış birim soluk görünür.
- **UX-03** Kart, hedefin üzerine **sürüklenerek** oynanır. Sürükleme sırasında Kontrol Alanı (C-02) ve geçerli hedefler vurgulanır. Uzun basınca kart detayı açılır.
- **UX-04** Ekran düzeni (yukarıdan aşağıya):
  - Rakip bilgisi: kule Can'ı, el sayısı, tamamlanan görevler
  - Tahta
  - Pazar (3 slot) ve kör çekme butonu
  - Kendi elin
  - **Mana** ve **Enerji** göstergeleri (iki ayrı renk ve ikon), tur süresi, "Turu Bitir" butonu
- **UX-05** Görevler ve komutan pasifi, köşedeki bir ikondan açılan panelde görünür.
- **UX-06** Olay duyurusu, tahtanın üstünde bir şerit ve karolarda nabız efektiyle gösterilir.
- **UX-07** Kart görünümü minimal: ikon, isim, Maliyet (Mana), karakterlerde 3 sayı (Saldırı/Can/Hareket), tek satır açıklama. Destek kartlarında çerçeve rengi **nadirlik** rengidir. Karakter kartlarının nadirliği **yoktur**; çerçeve rengi **biyom** rengidir (A-02).
- **UX-08 Öğretici (Faz 2):** 4 adım: hareket ve saldırı (Enerji) → kart oynama (Mana, Kontrol Alanı) → sis ve keşif → görev ve portal.
- **UX-09 Sis görünümü:** Keşfedilmemiş karolar koyu bulutla kaplıdır. Keşfedilmiş karolar gri/soluk renkte, son görülen rakip birimleri yarı saydam "hayalet" olarak gösterilir. Görünür karolar tam renklidir.
- **UX-10 Rakibin turu:** Ekranın üstünde "Rakibin turu" şeridi. Pazar ve kör çekme butonu ön seçim (T-11) için aktiftir. Oyuncu kendi birimlerine dokunarak menzilleri görebilir ve tahtaya plan okları çizebilir (tur başında silinir). Nöbet ve kule atışları vurgulu animasyonla oynatılır.

---

## 15. Görsel ve ses (A)

- **A-01** Low poly, canlı ve doygun renkler, yumuşak gölgeler (URP).
- **A-02** 5 karakter modeli + 1 kule. Biyom varyantları **renk paleti değişimiyle** yapılır. Biyom renkleri: Orman yeşil, Çöl turuncu, Kar açık mavi.
- **A-03** Karo setleri: 3 biyom karosu, Rün Taşı işareti, Kaynak işareti, Portal, Kaya, sis bulutu, soluk (keşfedilmiş) karo görünümü.
- **A-04** Ses (Faz 2): Tıklama, hareket, vuruş, ölüm, kart oynama, tuzak, olay, nöbet/kule atışı ve zafer sesleri.

---

## 16. Teknik mimari (özet)

Detaylar `CLAUDE.md` ve `.claude/rules/` altındadır.

- **Unity 6.6 (6000.6.x), URP**, önce Android, portre yönü.
- **Kural motoru (`Assets/_Project/Core`):**
  - Saf C#, UnityEngine bağımlılığı yok.
  - Deterministik: aynı tohum ve aynı komut dizisi her zaman aynı sonucu verir.
  - Komut tabanlı (`MoveCommand`, `AttackCommand`, `OverwatchCommand`, `DeployCommand` vb.).
  - Olay yayınlar (`UnitMoved`, `DamageDealt`, `TowerShot`, `VisibilityChanged` …). Unity katmanı bu olayları dinleyip animasyon oynatır.
  - Sis: Oyuncu başına keşif haritası ve son görülen haller `GameState` içindedir. Olaylar ve `PlayerView` oyuncuya göre süzülür (V-10).
- **Testler:** `Tools/Engine.Tests` projesi, Core kaynaklarını derleyip `dotnet test` ile Unity açmadan çalıştırır.
- **Simülasyon:** `Tools/Sim`, yapay zekâya karşı yapay zekâ ile toplu maç oynatır ve denge istatistiklerini üretir.
- **İçerik verisi:** Tüm sayılar tek dosyadadır: `Core/Data/Catalog.cs`. Bu dosya bu GDD ile birebir uyumlu olmalıdır.
- **Online (Faz 3):** Sunucu yetkili mimari. Aynı Core kodu sunucuda çalışır. Aday: Azure üzerinde ASP.NET Core + SignalR.

---

## 17. Yol haritası

| Kilometre taşı | Kapsam | Bitti sayılması için |
|---|---|---|
| **M0** İskelet | Klasörler, asmdef dosyaları, test ve simülasyon projeleri, dolu Catalog | `dotnet test` yeşil, Unity projesi hatasız derleniyor |
| **M1** Tahta | Altıgen matematiği, 59 karo, oyuncu yarıları, simetrik harita üretimi (B-*) | Simetri ve dağılım testleri, 1000 tohumda doğrulama |
| **M2** Birimler | Hareket (U-07, U-09…U-12), savaş, kuleye saldırı, kule savunması, nöbet, Siper, yetenekler (U-*) | Her U kuralı için test |
| **M3** Kartlar | Mana, Enerji, Kontrol Alanı, havuz, dağıtım, Pazar, destek kartları, tuzaklar (C-*, D-*, T-*) | Her kural için test |
| **M4** Maç | Hazırlık, sis ve görünürlük, görevler, pasifler, olaylar, kazanma koşulları (S-*, V-*, Q-*, P-*, E-*, W-*) | Baştan sona tam bir maç testte oynanıyor, `PlayerView` sızıntı testleri yeşil |
| **M5** Yapay zekâ ve simülasyon | Açgözlü yapay zekâ, Sim aracı (AI-*) | 1000 maç çökmeden oynanıyor. A/B kazanma oranı %45–55 arasında |
| **M6** Gri kutu istemci | Tahta çizimi, sis görünümü, dokunmatik giriş, arayüz, el, Pazar, süre, rakip turu ekranı, aynı cihazda 2 kişi, yapay zekâya karşı (UX-*) | Android cihazda baştan sona maç oynanabiliyor |
| **M7** Demo cilası (Faz 2) | Low poly sanat, animasyon, ses, öğretici | Yabancı biri yardımsız bir maçı bitirebiliyor |

---

## 18. Açık sorular

- Oyunun kalıcı adı.
- Kolay/Normal dışında zorluk seviyesi gerekli mi?
- Faz 3 online altyapısı: kendi sunucumuz (Azure + SignalR) mı, hazır bir servis mi?
- v2.0'daki yeni sayılar (karakter Maliyetleri, Enerji = 3, Görüş değerleri, kule Saldırı 2 / menzil 1–2, tur süresi 30 sn, Kontrol Alanı mesafesi 1) ilk tahmindir; M5 simülasyonundan sonra gözden geçirilecek.

---

## Değişiklik Günlüğü

| Sürüm | Tarih | Değişiklik |
|---|---|---|
| 1.0 | 2026-09-25 | İlk sürüm. Kararlar: Pazar 3 slot (Karakter/Buff/Debuff-Tuzak), online Faz 3'e ertelendi, kule saldırmaz |
| 2.0 | 2026-09-29 | **Kaynaklar:** Enerji ikiye ayrıldı: kart oynamak için **Mana** (T-01…T-03, C-01, T-06) ve hareket/saldırı için **Enerji** (T-05, turda 3). Her karakterin kendi **Maliyeti** var (§3.1, T-07). **Aksiyon:** "Turda 2 aksiyon" kalktı; her birim turda ya hareket eder ya saldırır ya nöbet tutar, her biri 1 Enerji (T-05). **Hareket:** Düz/çapraz/sıçrama kalıpları yerine tek kural: komşu karolar üzerinden Hareket kadar adım (U-07). U-08 KALDIRILDI. Süvari: Hareket 3, birimlerin üstünden geçer, Hücum bir önceki turdaki harekete bağlandı (U-02, U-09). **Yakınlık:** Tüm kartlar yalnızca Kontrol Alanında oynanır (C-02, C-30, C-15, T-07, S-05). **Buff/debuff sınırı:** 1 buff + 1 debuff, yenisi eskisinin yerini alır (C-05, C-06). **Kule:** Birimler rakip kuleye saldırabilir (U-26). Kule rakip turunda 1 kez, tek birime otomatik savunma atışı yapar (U-06, U-27). Yeni **Nöbet** aksiyonu (U-28, U-29). **Sis:** 3 görünürlük durumu ve Görüş statı (§11 V-*, U-12). Açılışta rakip yarısı gizli (S-07, V-04). Portal her zaman görünür (V-06). **Bekleme:** Rakibin turunda ön seçim, canlı izleme, planlama ve savunma tepkileri (§7.1 T-11…T-13, UX-10). Tur süresi 45 → 30 sn (T-09). Tuzak yalnızca durulan karoda tetiklenir (C-32). Oyuncu yarıları tanımlandı (B-06). Unity 6.6. Bölüm numaraları kaydı: Görünürlük §11 olarak eklendi, sonraki bölümler bir kaydı |
| 2.1 | 2026-09-29 | Netleştirme: Kule atışı her zaman 2 hasar, biyom/buff/debuff yok, hedefin Kalkan'ı geçerli (U-27). Karakter kartlarında nadirlik yok, çerçeve biyom rengi (UX-07) |
| 2.2 | 2026-09-30 | Netleştirme (M1 soruları): Çekirdekler biyom başına 3–5 olur, yayılmada en az karosu olan biyom büyür (B-22). Kaynak, Rün Taşı'na komşu olmaz (B-23). Yeniden üretim, aynı tohumun rastgele sayı akışıyla devam eder, "sonraki tohum" kullanılmaz (B-24). Gerekçe: 1000 tohumluk ön simülasyonda eski yorum (toplam 3–5 çekirdek, rastgele yayılma) ile haritaların yalnızca ~%7'si geçerliydi ve kümeler çok büyüktü (en büyüğü ortalama 16 karo). Yeni kuralla ~%96'sı geçerli, kümeler ortalama ~4 karo |
| 2.3 | 2026-09-30 | Netleştirme (M2 soruları): Muhafız'lar Siper'den faydalanmaz, komşu iki Muhafız birbirini korumaz (U-11). Gerekçe: Aksi halde komşu iki Muhafız ve korudukları birimler (ör. Portal'daki birim) saldırıyla hiç hedef alınamazdı. Şifa etkileri toplanır, Şifacılar birbirini iyileştirir, Şifacı kendini ve kuleyi iyileştirmez (U-05). Okçu atışını birim, kule veya kaya engellemez (U-03) |
| 2.4 | 2026-09-30 | Netleştirme (M2 soruları): Yeni V-11 görünen bilgi ilkesi eklendi. Siper yalnızca saldırana görünen Muhafız'larla çalışır (U-11). Gerekçe: Aksi halde yasal hedef listesi sis altındaki Muhafız'ı ele veriyordu, "boşa giden saldırı" gibi yeni bir durum eklemek yerine basit kural seçildi. Kule ve nöbet tetik listesi kapalıdır, itme/ışınlanmayla menzile gelmek tetik oluşturmaz (U-27, U-28) |
