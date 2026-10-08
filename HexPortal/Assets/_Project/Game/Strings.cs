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
        public const string Quit = "Çıkış";
        public const string MarketTitle = "Kart çek";
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

        /// <summary>C-05/C-06 hint before a buff/debuff replaces the unit's current one.</summary>
        public static string Replaces(string oldId) => ArtCatalog.Name(oldId) + " kartının yerini alacak";

        public const string EnemyPrefix = "Rakip ";
        public const string SomeUnit = "Bir birim";
        public const string OwnTowerRef = "Kulen";
        public const string EnemyTowerRef = "Rakip kule";
        public const string Died = " öldü";
        public const string Deployed = " sahaya çıktı";
        public const string BlockedDamage = " hasarı engelledi";
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
