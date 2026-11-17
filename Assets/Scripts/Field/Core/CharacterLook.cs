using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Field
{
    /// <summary>色（0〜255）。Unity に依存しないように自前で持つ。</summary>
    public struct Rgb
    {
        public byte r, g, b;
        public Rgb(int r, int g, int b) { this.r = (byte)r; this.g = (byte)g; this.b = (byte)b; }
        public Rgb Shade(float f) => new Rgb(Clamp(r * f), Clamp(g * f), Clamp(b * f));
        static int Clamp(float v) => v < 0 ? 0 : v > 255 ? 255 : (int)v;
        public override string ToString() => $"{r},{g},{b}";
    }

    /// <summary>
    /// 島民1人分の見た目。重ねる絵（体・服・髪・帽子）と、キー色の塗り替え先。
    /// 絵は Tools/PixelArt/generate_art.py が作る Assets/Resources/Art/char_*.png。
    /// </summary>
    public class CharacterLook
    {
        /// <summary>下から順に重ねる絵の名前（Resources/Art 内、拡張子なし）。</summary>
        public List<string> layers = new List<string>();
        public Rgb skin, hair, outfit, accent, pants, shoes, hat;

        /// <summary>同じ見た目かどうかの判定用（スプライトの使い回し）。</summary>
        public string Key => string.Join("+", layers) + "|" + skin + "|" + hair + "|" + outfit + "|" + accent + "|" + pants + "|" + shoes + "|" + hat;

        /// <summary>キー色（generate_art.py の KEY と同じ）。</summary>
        public static readonly Rgb KeySkin = new Rgb(255, 0, 255), KeySkinShade = new Rgb(200, 0, 200);
        public static readonly Rgb KeyHair = new Rgb(0, 255, 0), KeyHairShade = new Rgb(0, 170, 0);
        public static readonly Rgb KeyOutfit = new Rgb(0, 0, 255), KeyOutfitShade = new Rgb(0, 0, 160);
        public static readonly Rgb KeyAccent = new Rgb(255, 255, 0);
        public static readonly Rgb KeyPants = new Rgb(0, 255, 255), KeyPantsShade = new Rgb(0, 160, 160);
        public static readonly Rgb KeyShoes = new Rgb(255, 128, 0);
        public static readonly Rgb KeyHat = new Rgb(128, 0, 255), KeyHatShade = new Rgb(90, 0, 180);

        /// <summary>キー色 → 実際の色。キー色でなければ false。</summary>
        public bool TryMap(Rgb key, out Rgb color)
        {
            color = key;
            if (Eq(key, KeySkin)) color = skin;
            else if (Eq(key, KeySkinShade)) color = skin.Shade(0.8f);
            else if (Eq(key, KeyHair)) color = hair;
            else if (Eq(key, KeyHairShade)) color = hair.Shade(0.7f);
            else if (Eq(key, KeyOutfit)) color = outfit;
            else if (Eq(key, KeyOutfitShade)) color = outfit.Shade(0.72f);
            else if (Eq(key, KeyAccent)) color = accent;
            else if (Eq(key, KeyPants)) color = pants;
            else if (Eq(key, KeyPantsShade)) color = pants.Shade(0.72f);
            else if (Eq(key, KeyShoes)) color = shoes;
            else if (Eq(key, KeyHat)) color = hat;
            else if (Eq(key, KeyHatShade)) color = hat.Shade(0.72f);
            else return false;
            return true;
        }

        static bool Eq(Rgb a, Rgb b) => a.r == b.r && a.g == b.g && a.b == b.b;
    }

    /// <summary>島民のデータから見た目を決める（服装は職業、髪・肌は NPCAppearance）。</summary>
    public static class CharacterLooks
    {
        static readonly Rgb[] Skins = { new Rgb(255, 222, 190), new Rgb(250, 208, 170), new Rgb(232, 186, 146), new Rgb(210, 160, 120), new Rgb(176, 124, 88), new Rgb(130, 88, 60) };
        static readonly Rgb[] Hairs =
        {
            new Rgb(32, 30, 40), new Rgb(70, 46, 30), new Rgb(120, 76, 40), new Rgb(232, 196, 90), new Rgb(190, 70, 50), new Rgb(200, 200, 205),
            new Rgb(60, 90, 170), new Rgb(70, 140, 90), new Rgb(232, 130, 170), new Rgb(170, 175, 190), new Rgb(230, 130, 50), new Rgb(120, 80, 160),
        };
        static readonly string[] MaleHair = { "short", "spiky", "buzz", "short", "spiky", "bob" };
        static readonly string[] FemaleHair = { "long", "bob", "ponytail", "long", "bob", "short" };

        public static CharacterLook For(NPCData n)
        {
            var a = n.appearance ?? new NPCAppearance();
            var look = new CharacterLook
            {
                skin = Skins[Mod(a.skinTone, Skins.Length)],
                // 年配の人は白髪まじり
                hair = n.age >= 60 ? new Rgb(200, 200, 205) : Hairs[Mod(a.hairColor, Hairs.Length)],
            };
            look.layers.Add("char_body");
            bool female = n.gender == Gender.Female;
            Outfit(n.occupation, female, n.age, look);
            var styles = female ? FemaleHair : MaleHair;
            look.layers.Add("char_hair_" + styles[Mod(a.hairStyle, styles.Length)]);
            if (look.hat.r != 0 || look.hat.g != 0 || look.hat.b != 0)
                look.layers.Add(n.occupation == Occupation.Construction || n.occupation == Occupation.Farmer ? "char_hat_helmet" : "char_hat_cap");
            return look;
        }

        /// <summary>主人公（ユニフォーム姿）。</summary>
        public static CharacterLook Protagonist()
        {
            var look = new CharacterLook
            {
                skin = Skins[1], hair = Hairs[1],
                outfit = new Rgb(40, 120, 220), accent = new Rgb(250, 250, 250), pants = new Rgb(250, 250, 250), shoes = new Rgb(30, 30, 30),
            };
            look.layers.Add("char_body");
            look.layers.Add("char_hair_spiky");
            return look;
        }

        static int Mod(int v, int m) => ((v % m) + m) % m;

        static void Outfit(Occupation o, bool female, int age, CharacterLook l)
        {
            Rgb O, A, P, B = new Rgb(40, 36, 36), H = new Rgb(0, 0, 0);
            bool skirt = false, coat = false;
            switch (o)
            {
                case Occupation.Student:
                    if (female) { O = new Rgb(245, 245, 245); A = new Rgb(40, 60, 120); P = new Rgb(40, 60, 120); skirt = true; }
                    else { O = new Rgb(36, 40, 64); A = new Rgb(220, 190, 70); P = new Rgb(36, 40, 64); }
                    B = new Rgb(110, 60, 40); break;
                case Occupation.Salaryman: O = new Rgb(44, 52, 92); A = new Rgb(190, 40, 40); P = new Rgb(40, 44, 60); skirt = female; break;
                case Occupation.Banker: O = new Rgb(110, 112, 120); A = new Rgb(50, 80, 160); P = new Rgb(80, 82, 90); skirt = female; break;
                case Occupation.Construction: O = new Rgb(232, 132, 40); A = new Rgb(250, 250, 250); P = new Rgb(232, 132, 40); B = new Rgb(90, 64, 40); H = new Rgb(250, 210, 50); break;
                case Occupation.Doctor: O = new Rgb(246, 246, 246); A = new Rgb(90, 140, 200); P = new Rgb(90, 92, 104); coat = true; break;
                case Occupation.Nurse: O = new Rgb(250, 210, 225); A = new Rgb(250, 250, 250); P = new Rgb(250, 250, 250); B = new Rgb(250, 250, 250); skirt = female; break;
                case Occupation.ShopClerk: O = new Rgb(80, 150, 92); A = new Rgb(250, 250, 250); P = new Rgb(70, 64, 58); skirt = female && age < 40; break;
                case Occupation.ConvenienceClerk: O = new Rgb(60, 120, 200); A = new Rgb(250, 250, 250); P = new Rgb(50, 50, 60); break;
                case Occupation.Mafia: O = new Rgb(30, 30, 34); A = new Rgb(240, 240, 240); P = new Rgb(30, 30, 34); B = new Rgb(20, 20, 20); break;
                case Occupation.Police: O = new Rgb(40, 60, 120); A = new Rgb(230, 200, 60); P = new Rgb(40, 50, 90); B = new Rgb(20, 20, 20); H = new Rgb(40, 60, 120); break;
                case Occupation.Firefighter: O = new Rgb(180, 110, 50); A = new Rgb(230, 230, 80); P = new Rgb(180, 110, 50); H = new Rgb(200, 60, 40); break;
                case Occupation.Fisherman: O = new Rgb(240, 200, 60); A = new Rgb(250, 250, 250); P = new Rgb(40, 60, 100); B = new Rgb(30, 30, 30); H = new Rgb(40, 80, 140); break;
                case Occupation.Farmer: O = new Rgb(80, 120, 70); A = new Rgb(230, 230, 210); P = new Rgb(60, 80, 120); B = new Rgb(90, 64, 40); H = new Rgb(230, 200, 120); break;
                case Occupation.Teacher: O = new Rgb(140, 96, 60); A = new Rgb(250, 250, 250); P = new Rgb(70, 60, 50); skirt = female; break;
                case Occupation.Bartender: O = new Rgb(36, 32, 36); A = new Rgb(250, 250, 250); P = new Rgb(36, 32, 36); B = new Rgb(20, 20, 20); break;
                case Occupation.Chef: O = new Rgb(248, 248, 248); A = new Rgb(250, 250, 250); P = new Rgb(60, 60, 70); break;
                case Occupation.Hairdresser: O = new Rgb(40, 40, 46); A = new Rgb(240, 120, 170); P = new Rgb(40, 40, 46); break;
                case Occupation.Reporter: O = new Rgb(196, 170, 120); A = new Rgb(250, 250, 250); P = new Rgb(70, 70, 80); break;
                case Occupation.Lifeguard: O = new Rgb(220, 50, 50); A = new Rgb(250, 250, 250); P = new Rgb(220, 50, 50); B = new Rgb(250, 220, 170); break;
                case Occupation.Idol: O = new Rgb(250, 140, 190); A = new Rgb(250, 250, 250); P = new Rgb(250, 140, 190); B = new Rgb(250, 250, 250); skirt = true; break;
                case Occupation.Retired: O = new Rgb(150, 110, 80); A = new Rgb(220, 200, 170); P = new Rgb(90, 80, 70); break;
                case Occupation.Prisoner: O = new Rgb(120, 130, 150); A = new Rgb(250, 250, 250); P = new Rgb(120, 130, 150); break;
                case Occupation.Castaway: O = new Rgb(230, 225, 210); A = new Rgb(200, 190, 170); P = new Rgb(140, 110, 70); B = new Rgb(200, 160, 120); break;
                default: O = new Rgb(130, 130, 136); A = new Rgb(220, 220, 220); P = new Rgb(70, 70, 80); break; // 無職：スウェット
            }
            l.outfit = O; l.accent = A; l.pants = P; l.shoes = B; l.hat = H;
            if (skirt) l.layers.Add("char_skirt");
            if (coat) l.layers.Add("char_coat");
        }
    }
}
