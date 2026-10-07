using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Game
{
    /// <summary>Every Turkish UI string in one place. Text only: no rule logic.</summary>
    public static class Strings
    {
        public const string Title = "HexPortal";
        public const string Hotseat = "Aynı cihazda 2 kişi";
        // UX-13…UX-15 (v3.3)
        public const string PauseIcon = "II";
        public const string PauseTitle = "Duraklatıldı";
        public const string Resume = "Devam";
        public const string SettingTimerOn = "Süre: Açık";
        public const string SettingTimerOff = "Süre: Kapalı";
        public const string SettingVibrationOn = "Titreşim: Açık";
        public const string SettingVibrationOff = "Titreşim: Kapalı";
        public const string SettingSoundSoon = "Ses: yakında";
        public const string BackToMenu = "Ana menüye dön";
        public const string LeaveMatchConfirm = "Maçtan çıkılsın mı? Bu maç kaybolur.";
        public const string PortalOpenedYou = "Portal senin için açıldı!";
        public const string PortalOpenedOpp = "Portal rakip için açıldı!";
        public static string EnergyBonus(int n) => "+" + n + " Enerji";
        public static string ManaBonus(int n) => "+" + n + " Mana";
        // UX-08 tutorial
        public const string Tutorial = "Öğretici";
        public const string TutorialSkip = "Atla";
        public const string TutorialExit = "Çık";
        public const string TutorialStep1 = "1/4 · Enerji: Önce sağdan bir kart çek. Sonra bir birimine dokun: mavi karolara hareket (1 Enerji), kırmızı hedefe saldırı (1 Enerji). Turuncu hedef ilerle-saldır'dır (2 Enerji). Bir birimini hareket ettir ya da saldır.";
        public const string TutorialStep2 = "2/4 · Mana: Kartlar Mana ile oynanır ve yalnızca Kontrol Alanında (birimlerinin ve kulenin yanı) kullanılabilir. Elinden bir kartı vurgulu bir karoya sürükle ya da dokun.";
        public const string TutorialStep3 = "3/4 · Sis: Koyu bulutlar keşfedilmemiş karolardır. Birimlerini ilerleterek sisi aç. Enerjin bittiyse Turu Bitir.";
        public const string TutorialStep4 = "4/4 · Görevler ve Portal: Görevler düğmesinden gizli görevlerine bak. 2 görevi bitirince merkezdeki Portal açılır; bir birimin orada rakibin turunu atlatırsa kazanırsın.";
        public const string TutorialDone = "Öğretici bitti! İstersen bu maça devam et ya da Çık ile menüye dön.";
        public const string TimerOn = "Süre (2 kişi): Açık";
        public const string AiEasy = "Yapay zekâya karşı — Kolay";
        public const string AiNormal = "Yapay zekâya karşı — Normal";
        public const string SeatA = "YZ maçında sen: Oyuncu A (ilk oynar)";
        public const string SeatB = "YZ maçında sen: Oyuncu B";
        public const string AiTimerOn = "Süre (YZ): Açık";
        public const string AiTimerOff = "Süre (YZ): Kapalı";
        public const string TimerOff = "Süre (2 kişi): Kapalı";
        public const string HandoffTitle = "Telefonu rakibine ver";
        public const string HandoffTap = "Hazır olunca ekrana dokun";
        public const string PlayerA = "Oyuncu A";
        public const string PlayerB = "Oyuncu B";
        public const string ChooseQuests = "3 gizli görev seç";
        public const string ChoosePassive = "1 komutan pasifi seç";
        public const string Confirm = "Onayla";
        public const string PlaceTower = "Kuleni başlangıç bölgene yerleştir (mavi karolar)";
        public const string PlaceUnits = "Kartı sürükle ya da karta, sonra karoya dokun. Kartlar hazırlık Manasından düşer; en az 1 karakter.";
        public const string FinishSetup = "Yerleşimi bitir";
        public const string EndTurn = "Turu Bitir";
        public const string Overwatch = "Nöbet";
        public const string Blind = "Kör çek";
        public const string DrawFirst = "Önce kart çek: Pazar veya kör çekme";
        public const string OpponentTurn = "Rakibin turu";
        public const string YourTurn = "Senin turun";
        public const string Mana = "Mana";
        public const string Energy = "Enerji";
        public const string Quests = "Görevler";
        public const string Close = "Kapat";
        public const string Tower = "Kule";
        public const string Hand = "El";
        public const string Completed = "Tamamlandı";
        public const string Failed = "Başarısız";
        public const string Active = "Aktif";
        public const string PickDirection = "İtme yönünü seç (mavi karolar)";
        public const string PickDestination = "Işınlanma karosunu seç (mavi karolar)";
        public const string Draw = "Berabere";
        public const string Wins = " kazandı!";
        public const string NewMatch = "Ana menü";
        public const string Round = "Raunt ";
        public const string Bank = "Banka ";
        public const string Empty = "Boş";
        public const string Event = "Olay: ";
        public const string Passive = "Pasif: ";
        public const string OppPassive = "Rakip pasifi: ";
        public const string OppQuests = "Rakibin tamamladığı: ";
        public const string OppFailed = "Rakibin başarısız görevi: ";
        public const string Setup = "Hazırlık";

        // ---------- M6c: compact HUD, icons, reasons, notices, event log ----------
        public const string AttackIcon = "⚔";
        public const string OverwatchIcon = "◉";
        public const string TowerIcon = "♜";
        public const string HandIcon = "▯";
        public const string QuestDone = "✓";
        public const string QuestFail = "✗";
        public const string ManaIcon = "◆";
        public const string EnergyIcon = "⚡";
        public const string EventIcon = "⚠";
        public const string RoundShort = "R";
        public const string Blocked = "Engellendi";
        public const string DeathMark = "✖";
        public const string TrapHit = "Tuzak!";
        public const string TowerShotMark = "Kule atışı!";
        public const string OverwatchMark = "Nöbet!";
        public const string Cancel = "Vazgeç";
        public const string ErrorCounter = "Hata ";

        // Why a control does nothing right now (shown on tap; text only, the legal list decides).
        public const string ReasonOpponentTurn = "Rakibin turu";
        public const string ReasonDrawFirst = "Önce kart çek";
        public const string ReasonNoEnergy = "Enerji yok";
        public const string ReasonNoMana = "Mana yetmiyor";
        public const string ReasonActed = "Bu birim bu tur aksiyonunu kullandı";
        public const string ReasonNoAction = "Bu birim şu an bir şey yapamaz";
        public const string ReasonNoTarget = "Geçerli hedef yok";
        public const string ReasonHandFull = "El dolu";
        public const string ReasonSlotEmpty = "Bu Pazar slotu boş";
        public const string ReasonAlreadyDrew = "Bu tur kart çektin";
        public const string ReasonSetupNoDraw = "Hazırlıkta kart çekilmez";
        public const string ReasonCannotDraw = "Şu an kart çekilemez";
        public const string ReasonFinishSetup = "Önce kuleyi ve karakterlerini yerleştir";
        public const string ReasonPlaceTowerFirst = "Önce kuleyi yerleştir";
        public static readonly string ReasonMinCharacters = "En az " + Catalog.MinPlacedCharacters + " karakter yerleştir";
        public const string ReasonSetupMana = "Hazırlık Manası yetmiyor";
        public static readonly string ReasonMoveAttackEnergy = "Enerji yetmiyor (" + Catalog.MoveAttackEnergy + " gerekir)";
        public const string ReasonPickThree = "3 görev seç";
        public const string ReasonNotNow = "Şu an olmaz";

        // ---------- v3c: move-attack (T-05 v3.2), range preview (UX-10), setup budget (S-05), info panel (UX-11) ----------
        public const string PickApproach = "İlerleme karosunu seç (mavi). Başka yere dokun: vazgeç";
        public static readonly string UnitLegend = "Mavi hareket · Kırmızı saldırı · Turuncu ilerle-saldır (" + Catalog.MoveAttackEnergy + " Enerji)";
        public const string RangePreview = "Menzil: mavi hareket · kırmızı saldırı · turuncu ilerle-saldır";
        public const string SetupMana = "Hazırlık Manası";
        public const string InfoHealth = "Can";
        public const string InfoAttack = "Saldırı";
        public const string InfoMove = "Hareket";
        public const string InfoSight = "Görüş";
        public const string InfoRange = "Menzil";
        public const string InfoCost = "Maliyet";
        public const string InfoGhost = "Son görülen hal (sis)";
        public const string InfoActed = "Bu tur aksiyonunu kullandı";
        public const string InfoOverwatch = "Nöbette";
        public const string InfoTurnsLeft = " tur kaldı";
        public const string InfoLasting = "kalıcı";
        public const string InfoMyUnit = "Senin birimin";
        public const string InfoEnemyUnit = "Rakip birim";
        public const string InfoOwnTower = "Kulen";
        public const string InfoEnemyTower = "Rakip kule";
        public static readonly string InfoActions = "Hareket, saldırı, nöbet: 1 Enerji\nİlerle-saldır: " + Catalog.MoveAttackEnergy + " Enerji (tek hamle)";
        public static readonly string InfoCharge = " (+" + Catalog.RiderChargeBonus + " Hücum ile ilerle-saldırda)";
        public const string InfoTowerText = "Hareket etmez. Rakibin turunda menzilinde duran, sahaya çıkan ya da saldıran tek birime 1 kez ateş eder";

        /// <summary>UX-13 compact range chip under the Health bar (icon + numbers from the class data).</summary>
        public static string RangeChip(RangeKind kind, int min, int max)
        {
            string n = min == max ? max.ToString() : min + "-" + max;
            return "➶" + n; // glyph known to be in the UI font; the kind (line / distance) is in the UX-11 panel
        }

        /// <summary>UX-11 range line from the class data (text only).</summary>
        public static string RangeText(RangeKind kind, int min, int max)
        {
            switch (kind)
            {
                case RangeKind.Adjacent: return "Komşu";
                case RangeKind.StraightLine: return "Düz hatta " + min + "–" + max;
                default: return "Mesafe " + min + "–" + max;
            }
        }

        /// <summary>UX-11 ability line (GDD §3.1 wording).</summary>
        public static string Ability(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Guardian: return "Siper: Komşu dost birimler ve kule saldırıyla hedef alınamaz";
                case UnitClass.Rider: return "Atik: Birimlerin üstünden geçer. Hücum: İlerle-saldırda +" + Catalog.RiderChargeBonus + " Saldırı";
                case UnitClass.Archer: return "Uzak atış: Hat üzerindeki hiçbir şey atışı engellemez";
                case UnitClass.Mage: return "Patlama: Hedefe komşu düşmanlar " + Catalog.MageSplashDamage + " hasar alır";
                default: return "Şifa: Tur başında komşu dostlar +" + Catalog.HealerHealAmount + " Can";
            }
        }

        /// <summary>C-05/C-06 hint before a buff/debuff replaces the unit's current one.</summary>
        public static string Replaces(string oldId) => Name(oldId) + " kartının yerini alacak";

        /// <summary>Short symbol for a buff/debuff on a unit tag (no text on the board).</summary>
        public static string EffectIcon(string id)
        {
            switch (id)
            {
                case "C-10": return "⚔+";
                case "C-11": return "⚔+";
                case "C-12": return "◈";
                case "C-16": return "⚔−";
                case "C-17": return "☠";
                case "C-19": return "⚓";
                default: return "★";
            }
        }

        /// <summary>Small card icon (UX-07: icon, name, cost).</summary>
        public static string CardIcon(CardCategory c)
        {
            switch (c)
            {
                case CardCategory.Power: return "⚔";
                case CardCategory.Protection: return "◈";
                case CardCategory.Movement: return "➜";
                case CardCategory.Curse: return "☠";
                case CardCategory.Control: return "⚓";
                default: return "✱";
            }
        }

        public static string ClassIcon(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Guardian: return "■";
                case UnitClass.Rider: return "➜";
                case UnitClass.Archer: return "➶";
                case UnitClass.Mage: return "✦";
                default: return "✚";
            }
        }

        /// <summary>"Okçun" (own) / "Rakip Okçu" (enemy) for notices and the event log.</summary>
        public static string UnitRef(UnitClass c, bool own)
        {
            if (!own) return "Rakip " + ClassName(c);
            switch (c)
            {
                case UnitClass.Guardian: return "Muhafızın";
                case UnitClass.Rider: return "Süvarin";
                case UnitClass.Archer: return "Okçun";
                case UnitClass.Mage: return "Büyücün";
                default: return "Şifacın";
            }
        }

        public const string SomeUnit = "Bir birim";
        public const string OwnTowerRef = "Kulen";
        public const string EnemyTowerRef = "Rakip kule";
        public const string Died = " öldü";
        public const string Deployed = " sahaya çıktı";
        public const string ShieldUsed = ": Kalkan hasarı engelledi";
        public const string WallUsed = ": Kalın Duvar hasarı engelledi";
        public const string TrapOn = "Tuzak tetiklendi";
        public const string TowerFired = "Kule ateş etti";
        public const string OverwatchFiredText = "Nöbet ateşi";
        public const string OppPlayed = "Rakip oynadı: ";
        public const string YouPlayed = "Oynadın: ";
        public const string QuestDoneText = "Görev tamamlandı: ";
        public const string OppQuestDoneText = "Rakip görev tamamladı: ";
        public const string QuestFailText = "Görev başarısız: ";
        public const string OppQuestFailText = "Rakibin görevi başarısız: ";
        public const string PassiveOpened = "Pasif açıldı: ";
        public const string EventHappened = "Olay: ";
        public const string Revived = " geri döndü";

        public static string DamageKindName(DamageKind k)
        {
            switch (k)
            {
                case DamageKind.Splash: return "patlama";
                case DamageKind.TowerShot: return "kule";
                case DamageKind.Overwatch: return "nöbet";
                case DamageKind.Trap: return "tuzak";
                case DamageKind.Poison: return "zehir";
                case DamageKind.PortalWarden: return "portal";
                default: return "saldırı";
            }
        }

        public static string Player(PlayerId p) => p == PlayerId.A ? PlayerA : PlayerB;

        public static string ClassName(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Guardian: return "Muhafız";
                case UnitClass.Rider: return "Süvari";
                case UnitClass.Archer: return "Okçu";
                case UnitClass.Mage: return "Büyücü";
                default: return "Şifacı";
            }
        }

        public static string ClassLetter(UnitClass c)
        {
            switch (c)
            {
                case UnitClass.Guardian: return "M";
                case UnitClass.Rider: return "S";
                case UnitClass.Archer: return "O";
                case UnitClass.Mage: return "B";
                default: return "Ş";
            }
        }

        public static string BiomeName(Biome b)
        {
            switch (b)
            {
                case Biome.Forest: return "Orman";
                case Biome.Desert: return "Çöl";
                case Biome.Snow: return "Kar";
                default: return "";
            }
        }

        /// <summary>Display names by GDD id (§4, §8, §9, §10).</summary>
        public static string Name(string id)
        {
            switch (id)
            {
                case "C-10": return "Öfke";
                case "C-11": return "Dev Gücü";
                case "C-12": return "Kalkan";
                case "C-13": return "Şifa İksiri";
                case "C-14": return "Rüzgâr Adımı";
                case "C-15": return "Işınlanma";
                case "C-16": return "Zayıflık";
                case "C-17": return "Zehir";
                case "C-18": return "İtme";
                case "C-19": return "Kök Salma";
                case "C-20": return "Diken Tuzağı";
                case "C-21": return "Ayna Tuzağı";
                case "Q-10": return "Rün Bekçisi";
                case "Q-11": return "Çift Rün";
                case "Q-12": return "Avcı";
                case "Q-13": return "Kuşatma";
                case "Q-14": return "Sağlam Kale";
                case "Q-15": return "Biyom Ustası";
                case "Q-16": return "Kayıpsız";
                case "Q-17": return "Tuzakçı";
                case "Q-18": return "Kaynak Lordu";
                case "Q-19": return "Derin Akın";
                case "P-01": return "Son Nefes";
                case "P-02": return "Tuzak Ustası";
                case "P-03": return "Hızlı Başlangıç";
                case "P-04": return "Kalın Duvar";
                case "P-05": return "Pazarcı";
                case "P-06": return "Portal Bekçisi";
                case "E-10": return "Deprem";
                case "E-11": return "Biyom Kayması";
                case "E-12": return "Rün Yağmuru";
                default: return id ?? "";
            }
        }

        /// <summary>One-line card/quest/passive texts (UX-07), copied from the GDD wording.</summary>
        public static string Text(string id)
        {
            switch (id)
            {
                case "C-10": return "+2 Saldırı, 2 tur";
                case "C-11": return "+1 Saldırı, kalıcı";
                case "C-12": return "Sonraki hasarı engeller";
                case "C-13": return "+5 Can";
                case "C-14": return "Bu tur +2 Hareket";
                case "C-15": return "Dost birimi boş karoya taşır";
                case "C-16": return "−2 Saldırı, 2 tur";
                case "C-17": return "Tur başında −2 Can, 2 tur";
                case "C-18": return "Düz yönde 2 karo iter";
                case "C-19": return "Hareket edemez, 2 tur";
                case "C-20": return "Tuzak: 3 hasar";
                case "C-21": return "Tuzak: başlangıca ışınlar";
                case "Q-10": return "Aynı rün taşını tut";
                case "Q-11": return "İki farklı rün taşında birimin olsun";
                case "Q-12": return "2 düşman birimi yok et";
                case "Q-13": return "Rakip kuleye toplam 10 hasar ver";
                case "Q-14": return "10. raunt sonunda kule Canı 14 veya üstü";
                case "Q-15": return "3 birimin aynı anda kendi biyomunda";
                case "Q-16": return "8. raunt sonuna kadar birim kaybetme";
                case "Q-17": return "Tuzağın bir düşmanda tetiklensin";
                case "Q-18": return "Kaynak karosunu tut";
                case "Q-19": return "Rakip başlangıç bölgesinde 2 birimin olsun";
                case "P-01": return "İlk ölen birimin 4 Can ile geri gelir";
                case "P-02": return "Tuzaklar +1 hasar verir";
                case "P-03": return "3. rauntta +2 Mana";
                case "P-04": return "Kulene gelen ilk 5 hasar engellenir";
                case "P-05": return "İlk Pazar alımında +1 kör kart";
                case "P-06": return "Portal'a giren rakip 3 hasar alır";
                default: return "";
            }
        }

        public static string Reason(GameResult r)
        {
            switch (r.Reason)
            {
                case WinReason.Portal: return "Portal zaferi (W-01)";
                case WinReason.Tower: return "Kule yıkıldı (W-02)";
                case WinReason.Timeout: return "Süre cezası (W-04)";
                default: return "Raunt sınırı (W-03), ölçüt " + r.Criterion;
            }
        }

        public static string SlotName(int slot)
        {
            switch (slot)
            {
                case 0: return "Karakter";
                case 1: return "Buff";
                case 2: return "Debuff/Tuzak";
                default: return Blind;
            }
        }

        public static string Status(QuestStatus s) =>
            s == QuestStatus.Completed ? Completed : s == QuestStatus.Failed ? Failed : Active;
    }
}
