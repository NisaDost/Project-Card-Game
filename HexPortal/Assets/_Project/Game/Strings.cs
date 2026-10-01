using HexPortal.Core;
using HexPortal.Core.Data;

namespace HexPortal.Game
{
    /// <summary>Every Turkish UI string in one place. Text only: no rule logic.</summary>
    public static class Strings
    {
        public const string Title = "HexPortal";
        public const string Hotseat = "Aynı cihazda 2 kişi";
        public const string TimerOn = "Süre: Açık";
        public const string TimerOff = "Süre: Kapalı";
        public const string HandoffTitle = "Telefonu rakibine ver";
        public const string HandoffTap = "Hazır olunca ekrana dokun";
        public const string PlayerA = "Oyuncu A";
        public const string PlayerB = "Oyuncu B";
        public const string ChooseQuests = "3 gizli görev seç";
        public const string ChoosePassive = "1 komutan pasifi seç";
        public const string Confirm = "Onayla";
        public const string PlaceTower = "Kuleni başlangıç bölgene yerleştir (mavi karolar)";
        public const string PlaceUnits = "Karakter kartına dokun, sonra karoya dokun (en az 3). Tuzak da koyabilirsin.";
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
        public const string Setup = "Hazırlık";

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
                case "C-13": return "+3 Can";
                case "C-14": return "Bu tur +2 Hareket";
                case "C-15": return "Dost birimi boş karoya taşır";
                case "C-16": return "−2 Saldırı, 2 tur";
                case "C-17": return "Tur başında −1 Can, 2 tur";
                case "C-18": return "Düz yönde 2 karo iter";
                case "C-19": return "Hareket edemez, 2 tur";
                case "C-20": return "Tuzak: 3 hasar";
                case "C-21": return "Tuzak: başlangıca ışınlar";
                case "Q-10": return "Aynı rün taşını tut";
                case "Q-11": return "İki farklı rün taşında birimin olsun";
                case "Q-12": return "2 düşman birimi yok et";
                case "Q-13": return "Rakip kuleye toplam 5 hasar ver";
                case "Q-14": return "8. raunt sonunda kule Canı 7 veya üstü";
                case "Q-15": return "3 birimin aynı anda kendi biyomunda";
                case "Q-16": return "6. raunt sonuna kadar birim kaybetme";
                case "Q-17": return "Tuzağın bir düşmanda tetiklensin";
                case "Q-18": return "Kaynak karosunu tut";
                case "Q-19": return "Rakip başlangıç bölgesinde 2 birimin olsun";
                case "P-01": return "İlk ölen birimin 2 Can ile geri gelir";
                case "P-02": return "Tuzaklar +1 hasar verir";
                case "P-03": return "3. rauntta +2 Mana";
                case "P-04": return "Kulene gelen ilk 3 hasar engellenir";
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
