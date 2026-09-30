# HexPortal — Kurulum ve Master Prompt

## 1. Kurulum (bir kez, Windows)

1. **Unity Hub**'da projeyi oluştur:
   - New Project → **Unity 6.6** (sürüm numarası `6000.6.x`) → **Universal 3D (URP)** şablonu → proje adı: `HexPortal`.
   - Unity Hub'da bu sürüm için **Android Build Support** modülünün kurulu olduğundan emin ol.
2. Bu kitin içeriğini proje köküne kopyala. `Assets/` klasörüyle aynı seviyeye:
   `CLAUDE.md`, `MASTER_PROMPT.md`, `.gitignore`, `docs/`, `.claude/`
   Not: `.claude` gizli bir klasördür. Kopyalarken atlanmadığından emin ol.
3. Terminalde `dotnet --list-sdks` çalıştır. En az .NET 8 SDK görünmeli. Hook'lar için **Git for Windows** ve **PowerShell** gerekli, Windows'ta ikisi de genelde hazırdır.
4. Proje kökünde:
   ```
   git init
   git add .
   git commit -m "Initial Unity project + Claude kit"
   ```
5. Proje kökünde `claude` komutunu çalıştır. Kurulumu şu komutlarla kontrol et:
   - `/hooks`: 2 hook görünmeli (PreToolUse, Stop)
   - `/agents`: 4 ajan görünmeli
   - `/memory`: CLAUDE.md yüklenmiş olmalı

## 2. İlk oturum: bu prompt'u yapıştır

```
Bu repo HexPortal oyununun başlangıç kitidir. CLAUDE.md, docs/GDD.md ve docs/PROGRESS.md dosyalarını baştan sona oku. .claude/ altındaki rules, agents, skills ve hooks dosyalarını incele.

Rolün: Baş geliştirici ve teknik lidersin. Tüm kodu ve mimariyi sen yazacaksın. Tasarım kararlarını ben ve takım analistimiz veriyoruz.

Hedef: GDD §17'deki M0–M6 kilometre taşlarını tamamlayıp Android'de oynanabilir bir prototip çıkarmak: yapay zekâya karşı mod + aynı cihazda 2 kişi modu.

Çalışma şeklin:
1. Her kilometre taşına plan moduyla başla. Planda şunlar olsun: kapsadığın GDD kural ID'leri, oluşturacağın dosyalar, test listesi. Onayımı bekle.
2. Core işlerini rules-engineer ajanına, Unity işlerini unity-client-dev ajanına ver. Birbirine bağlı olmayan işleri paralel çalıştır.
3. Önce test yaz. `dotnet test` yeşil olmadan hiçbir kilometre taşını bitmiş sayma.
4. Kilometre taşı bitince:
   - gdd-reviewer ajanını çalıştır ve bulguları düzelt.
   - docs/PROGRESS.md dosyasını güncelle.
   - Commit et.
   - Bana Türkçe kısa bir özet ve "Unity'de kontrol et" listesi ver, sonra dur.
5. GDD belirsizse varsayım yapma, bana sor. GDD'yi ve Catalog.cs'teki sayıları onayım olmadan değiştirme.
6. Minimal kal: küçük ve tek sorumluluklu dosyalar yaz, gereksiz soyutlama ekleme.

İlk görev: M0 (İskelet)
- `dotnet --list-sdks` ile kurulu SDK'yı kontrol et. Tools projelerinde kurulu en yeni TFM'yi kullan, LangVersion 9 olsun.
- Klasör yapısını ve asmdef dosyalarını oluştur. HexPortal.Core için noEngineReferences: true olsun.
- Tools/Engine.Tests projesini kur: NUnit, Core kaynaklarını Compile Include ile derlesin.
- Tools/Sim konsol projesini kur.
- Core/Data/Catalog.cs dosyasını GDD tablolarındaki tüm sayılarla doldur. Catalog'u GDD'ye karşı doğrulayan testler yaz, örneğin: destek kartı toplamı 34, karakter havuzu 30, her sınıfın statları (Maliyet, Saldırı, Can, Hareket, Görüş), kule statları, Enerji (turda 3) ve Mana kuralları (T-01…T-03), Kontrol Alanı mesafesi (C-02), buff/debuff sınırı (C-05), görev ve pasif sayıları.
- Dikkat: Oyunda iki ayrı kaynak var. Mana kart oynamak için (her kartın Maliyeti), Enerji birimleri hareket ettirmek/saldırtmak için. Kodda bunları asla tek bir alanda birleştirme.
- Gerekiyorsa Packages/manifest.json dosyasına Input System paketini ekle.
- Minimal istemci: runtime bootstrap ile boş sahnede "HexPortal M0" yazısı ve dikey kamera.

Plan ile başla.
```

## 3. Sonraki oturumlar

```
docs/PROGRESS.md'ye göre sıradaki kilometre taşına geç. Aynı çalışma şeklini izle: plan → onay → önce test → uygula → gdd-reviewer → PROGRESS.md → commit → Türkçe özet ve dur.
```

## 4. Faydalı kısa prompt'lar

- **Denge raporu (M5 sonrası):**
  `balance-analyst ajanıyla 3 farklı seed'de 1000'er maç simüle et ve raporla.`
- **Yeni içerik:**
  `add-content skill'ini izleyerek şu kartı ekle: <isim, etki, süre, maliyet (Mana), nadirlik>. Önce GDD değişikliğini göster, onayımı bekle.`
- **Hata:**
  `Unity'de şu oldu: <ekran görüntüsü veya log>. Önce kök nedeni bul, sonra düzelt, sonra test ekle.`

## Kitteki dosyalar

| Dosya | Görevi |
|---|---|
| `CLAUDE.md` | Proje hafızası. GDD ve PROGRESS dosyalarını otomatik yükler |
| `docs/GDD.md` | Tasarım dokümanı, kalıcı kural ID'leriyle |
| `docs/PROGRESS.md` | Kilometre taşı durumu ve oturum günlüğü |
| `.claude/rules/*.md` | Klasöre göre otomatik yüklenen kurallar (Core, Game, docs) |
| `.claude/agents/*.md` | rules-engineer, unity-client-dev, gdd-reviewer, balance-analyst |
| `.claude/skills/hex-grid` | Doğrulanmış altıgen matematiği ve tahta düzeni |
| `.claude/skills/add-content` | İçerik ekleme ve değiştirme kontrol listesi |
| `.claude/hooks/guard.ps1` | Unity'nin ürettiği klasörlere, .meta dosyalarına ve Core'da yasaklı API'lere yazmayı engeller |
| `.claude/hooks/test-gate.ps1` | Core değiştiyse testler kırmızıyken oturumun bitmesini engeller |
| `.claude/settings.json` | İzinler ve hook tanımları |
